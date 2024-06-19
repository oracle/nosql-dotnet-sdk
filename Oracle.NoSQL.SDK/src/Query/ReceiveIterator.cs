/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using static Utils;

    internal partial class ReceiveIterator : PlanAsyncIterator
    {
        private readonly ReceiveStep step;
        private readonly QueryRequest<RecordValue> queryRequest;
        private readonly Duplicates duplicates;
        private readonly SimpleResult simpleResult;
        private readonly SortedSet<PartialResult> partialResults;
        private long totalMemory;
        private int totalRows;
        private byte[] phase1ContinuationKey;
        private int currVScanId = -1;
        private bool inSortPhase1;

        internal ReceiveIterator(QueryRuntime runtime, ReceiveStep step) :
            base(runtime)
        {
            this.step = step;
            queryRequest = new QueryRequest<RecordValue>(runtime.Client,
                runtime.PreparedStatement,
                new QueryOptions
                {
                    Compartment = runtime.Request.Options?.Compartment,
                    Timeout = runtime.Request.Options?.Timeout,
                    Consistency = runtime.Request.Options?.Consistency,
                    MaxReadKB = runtime.Request.Options?.MaxReadKB,
                    MaxWriteKB = runtime.Request.Options?.MaxWriteKB,
                    TraceLevel = runtime.Request.Options?.TraceLevel,
                    QueryLabel = runtime.Request.Options?.QueryLabel
                })
            {
                BaseTopology = runtime.BaseTopology,
                IsInternal = true
            };

            if (step.PrimaryKeyFields != null)
            {
                duplicates = new Duplicates(this);
            }

            if (step.SortSpecs != null)
            {
                if (step.DistributionKind == DistributionKind.AllShards)
                {
                    var topologyInfo = runtime.BaseTopology;
                    
                    if (topologyInfo == null)
                    {
                        throw new InvalidOperationException(
                            "Missing topology information for all-shard query");
                    }

                    Debug.Assert(topologyInfo.ShardIds?.Count != 0);

                    partialResults = new SortedSet<PartialResult>();
                    // Seed empty shard results
                    foreach (var shardId in topologyInfo!.ShardIds!)
                    {
                        partialResults.Add(new PartialResult(this, shardId));
                    }
                }
                else if (step.DistributionKind ==
                         DistributionKind.AllPartitions)
                {
                    partialResults = new SortedSet<PartialResult>();
                    inSortPhase1 = true;
                }
            }
            else
            {
                simpleResult = new SimpleResult(this);
            }

            Trace("Created iterator, distribution kind = " +
                step.DistributionKind, 1);
        }

        private async Task<QueryResult<RecordValue>> FetchAsync(
            byte[] continuationKey, CancellationToken cancellationToken,
            int shardId = -1, int limit = 0, VirtualScan virtualScan = null)
        {
            queryRequest.Options.ContinuationKey =
                continuationKey != null ?
                new QueryContinuationKey(continuationKey) : null;

            if (limit > 0)
            {
                var optionsLimit = runtime.Request.Options?.Limit ?? 0;
                queryRequest.Options.Limit = optionsLimit > 0 ?
                    Math.Min(limit, optionsLimit) : limit;
            }
            else
            {
                queryRequest.Options.Limit = runtime.Request.Options?.Limit;
            }

            queryRequest.ShardId = shardId;
            queryRequest.VirtualScan = virtualScan;

            var result = (QueryResult<RecordValue>)
                await runtime.Client.ExecuteValidatedRequestAsync(
                    queryRequest, cancellationToken);

            runtime.TallyConsumedCapacity(result.ConsumedCapacity);
            runtime.FetchDone = true;

            Trace($"Fetch returned {result.Rows.Count} records");

            if (!inSortPhase1 && !result.ReachedLimit &&
                result.ContinuationKey != null)
            {
                throw new BadProtocolException(
                    "Query: received inconsistent result info: non-null " +
                    "continuation key, reached limit is false and " +
                    "the query is not in sort phase 1");
            }

            // Virtual scans can only be sent for ALL_SHARDS query.
            if (result.VirtualScans != null &&
                step.DistributionKind != DistributionKind.AllShards)
            {
                throw new BadProtocolException(
                    "Received virtual scans for non-all-shard query type " +
                    step.DistributionKind);
            }

            if (result.QueryTraces != null)
            {
                runtime.AddServerQueryTraces(result.QueryTraces);
                queryRequest.Options.BatchNumber++;
            }

            return result;
        }

        private void AddPartitionResults(QueryResult<RecordValue> result)
        {
            var sortPhase1 = result.SortPhase1;
            var numPartitionIds = sortPhase1.PartitionIds?.Length ?? 0;
            var numResultCounts = sortPhase1.ResultCounts?.Length ?? 0;
            
            Trace($"SortPhase1: received results for {numPartitionIds} " +
                "partitions", 3);

            if (numPartitionIds != numResultCounts)
            {
                throw new BadProtocolException(
                    "Query: received mismatched number of partition ids " +
                    $"{numPartitionIds} and per-partition result counts " +
                    numResultCounts);
            }

            // Established during deserialization
            Debug.Assert(numPartitionIds ==
                         (sortPhase1.ContinuationKeys?.Length ?? 0));

            if (numPartitionIds != 0)
            {
                var rowIndex = 0;
                for (var i = 0; i < numPartitionIds; i++)
                {
                    // Avoid ReSharper warnings
                    Debug.Assert(sortPhase1.PartitionIds != null &&
                                 sortPhase1.ResultCounts != null &&
                                 sortPhase1.ContinuationKeys != null);

                    var partitionId = sortPhase1.PartitionIds[i];
                    var count = sortPhase1.ResultCounts[i];

                    if (count <= 0)
                    {
                        throw new BadProtocolException(
                            "Query: partition result row count must be " +
                            $"positive. Received row count {count} for " +
                            $"partition id {partitionId} in phase 1 of " +
                            "all-partition query");
                    }

                    if (rowIndex + count > result.Rows.Count)
                    {
                        throw new BadProtocolException(
                            "Query: exceeded row count " +
                            $"{result.Rows.Count} while getting rows " +
                            $"for partition id {partitionId} in phase 1 of " +
                            "all-partition query");
                    }

                    var partitionResult = new PartialResult(this,
                        partitionId,
                        new ArraySegment<RecordValue>(
                            (RecordValue[])result.Rows, rowIndex, count),
                        sortPhase1.ContinuationKeys[i]);
                    partialResults.Add(partitionResult);
                    rowIndex += count;
                }

                if (rowIndex != result.Rows.Count)
                {
                    throw new BadProtocolException(
                        $"Query: expected row count {rowIndex} in phase 1 " +
                        "of all-partition query does not match received " +
                        $"row count {result.Rows.Count}");
                }
            }
        }

        // Returns true if phase1 is completed.
        private async Task<bool> DoSortPhase1Async(
            CancellationToken cancellationToken)
        {
            Trace("Entered SortPhase1, runtime.FetchDone=" +
                runtime.FetchDone, 3);
            
            if (runtime.FetchDone)
            {
                // Have to postpone phase 1 to the next Query() call
                Debug.Assert(runtime.NeedContinuation);
                return false;
            }

            /*
             * Create and execute a request to get at least one result from
             * the partition whose id is specified in the ContinuationKey and
             * from any other partition that is co-located with that partition.
             */
            var result = await FetchAsync(phase1ContinuationKey,
                cancellationToken);
            if (result.SortPhase1 == null)
            {
                throw new BadProtocolException("Query: first response to " +
                    "all-partitions query is not a phase 1 response");
            }

            inSortPhase1 = result.SortPhase1.ToContinue;
            phase1ContinuationKey = result.ContinuationKey.Bytes;
            Trace("Received SortPhase1 results, ToContinue = " +
                $"{inSortPhase1}, phase1ContinuationKey length = " +
                (phase1ContinuationKey?.Length ?? -1), 2);

            if (inSortPhase1 && phase1ContinuationKey == null)
            {
                throw new BadProtocolException(
                    "Query: missing continuation key to continue " +
                    "phase 1 of all-partitions query");
            }

            AddPartitionResults(result);
            if (inSortPhase1)
            {
                runtime.NeedContinuation = true;
                return false;
            }

            return true;
        }

        private async Task<bool> SimpleNextAsync(
            CancellationToken cancellationToken)
        {
            for (;;)
            {
                var row = simpleResult.Next();
                if (row != null)
                {
                    if (duplicates != null && duplicates.IsDuplicate(row))
                    {
                        continue;
                    }

                    Result = row;
                    return true;
                }

                if (!simpleResult.HasRemoteResults)
                {
                    return false;
                }

                if (runtime.FetchDone)
                {
                    if (simpleResult.ContinuationKey != null)
                    {
                        runtime.NeedContinuation = true;
                    }

                    return false;
                }

                simpleResult.SetQueryResult(await FetchAsync(
                    simpleResult.ContinuationKey, cancellationToken));
            }
        }

        private int GetLimitFromMemory()
        {
            Debug.Assert(totalRows > 0);
            var memoryPerRow = totalMemory / totalRows;
            Debug.Assert(memoryPerRow > 0);
            var limit = (runtime.MaxMemory - (duplicates?.Memory ?? 0)) /
                        memoryPerRow;

            if (limit > 2048)
            {
                limit = 2048;
            }

            if (limit <= 0)
            {
                throw new InvalidOperationException(
                    "Query: cannot make another request because set memory " +
                    $"limit of {runtime.MaxMemoryStr} will be exceeded");
            }

            return (int)limit;
        }

        private void HandleVirtualScans(
            IReadOnlyList<VirtualScan> virtualScans)
        {
            if (currVScanId == -1)
            {
                var topologyInfo = runtime.BaseTopology;
                Debug.Assert(topologyInfo?.ShardIds?.Count != 0);
                // ShardIds are sorted.
                currVScanId = topologyInfo!.ShardIds![^1] + 1;
            }

            foreach (var virtualScan in virtualScans)
            {
                partialResults.Add(new PartialResult(this, currVScanId++,
                    virtualScan));
            }
        }

        private async Task SortingFetchAsync(PartialResult result,
            CancellationToken cancellationToken)
        {
            var limit = 0;
            var shardId = -1;
            VirtualScan virtualScan = null;

            if (step.DistributionKind == DistributionKind.AllPartitions)
            {
                // We only limit number of rows for ALL_PARTITIONS query
                limit = GetLimitFromMemory();
                // For ALL_PARTITIONS query, decrement memory from previous
                // result.
                runtime.TotalMemory -= result.Memory;
            }
            else
            {
                Debug.Assert(step.DistributionKind ==
                             DistributionKind.AllShards);
                shardId = result.Id;
                virtualScan = result.VirtualScan;
            }

            QueryResult<RecordValue> queryResult;

            try
            {
                queryResult = await FetchAsync(result.ContinuationKey,
                    cancellationToken, shardId, limit, virtualScan);
            }
            catch (Exception)
            {
                // Add original result in case this exception is retryable
                partialResults.Add(result);
                throw;
            }

            result.SetQueryResult(queryResult);
            partialResults.Add(result);

            Trace($"SortingFetch: received {queryResult.Rows.Count} rows " +
                $"for partial result id {result.Id}");

            if (step.DistributionKind == DistributionKind.AllPartitions)
            {
                result.SetMemoryStats();
            }
            else
            {
                if (virtualScan != null)
                {
                    virtualScan.IsInfoSent = true;
                }

                if (queryResult.VirtualScans != null)
                {
                    HandleVirtualScans(queryResult.VirtualScans);
                    queryResult.VirtualScans = null;
                }
            }
        }

        private async Task<bool> SortingNextAsync(
            CancellationToken cancellationToken)
        {
            if (inSortPhase1 && !await DoSortPhase1Async(cancellationToken))
            {
                return false;
            }

            PartialResult result;
            while ((result = partialResults.Min) != null)
            {
                partialResults.Remove(result);
                var row = result.Next();
                if (row != null)
                {
                    if (result.HasResults)
                    {
                        partialResults.Add(result);
                    }

                    if (duplicates != null && duplicates.IsDuplicate(row))
                    {
                        continue;
                    }

                    ConvertEmptyToNull(row);
                    Result = row;
                    return true;
                }

                Trace("In SortingNext, no more local results for id " +
                    $"{result.Id}, HasRemoteResults = " +
                    $"{result.HasRemoteResults}, FetchDone = " +
                    runtime.FetchDone, 3);

                if (!result.HasRemoteResults)
                {
                    // No more results for this shard or partition
                    continue;
                }

                // At this point remote fetch is needed
                if (runtime.FetchDone)
                {
                    // Release array memory
                    result.Rows = null;
                    partialResults.Add(result);
                    // We limit to 1 fetch per Query() call
                    runtime.NeedContinuation = true;
                    return false;
                }

                await SortingFetchAsync(result, cancellationToken);
            }

            return false;
        }

        internal override PlanStep Step => step;

        internal override Task<bool> NextAsync(
            CancellationToken cancellationToken)
        {
            return partialResults != null ?
                SortingNextAsync(cancellationToken) :
                SimpleNextAsync(cancellationToken);
        }
    }
}
