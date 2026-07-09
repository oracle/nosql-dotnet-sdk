/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections.Generic;
    using System.Linq;

    // Internal implementation of Java-compatible client statistics.  The
    // public surface is StatsControl; this file owns the aggregation buckets
    // and JSON-compatible snapshot generation.
    internal sealed class Percentile
    {
        private readonly StatsControl.PercentileMode mode;
        private readonly List<long> values;
        private readonly Dictionary<long, long> buckets;
        private long bucketValueCount;
        private long[] sortedBucketKeys;

        internal Percentile(StatsControl.PercentileMode mode =
            StatsControl.PercentileMode.Exact)
        {
            this.mode = mode;
            if (mode == StatsControl.PercentileMode.Exact)
            {
                values = new List<long>();
            }
            else
            {
                buckets = new Dictionary<long, long>();
            }
        }

        internal void AddValue(long value)
        {
            if (mode == StatsControl.PercentileMode.Exact)
            {
                values.Add(value);
                return;
            }

            /*
             * Latency is already truncated to an integer millisecond before it
             * reaches Stats.  Counting each distinct value therefore reduces
             * storage without introducing approximate percentile boundaries.
             */
            if (buckets.TryGetValue(value, out var bucketCount))
            {
                buckets[value] = bucketCount + 1;
            }
            else
            {
                buckets[value] = 1;
                sortedBucketKeys = null;
            }
            bucketValueCount++;
        }

        internal long GetPercentile(double percentile)
        {
            return mode == StatsControl.PercentileMode.Exact
                ? GetExactPercentile(percentile)
                : GetBucketedPercentile(percentile);
        }

        private long GetExactPercentile(double percentile)
        {
            if (values.Count == 0)
            {
                return -1;
            }

            // Keep the same exact percentile calculation as the Java SDK:
            // sort all samples, then use round(percentile * count - 1).
            values.Sort();
            var index = (int)Math.Round(
                percentile * values.Count - 1,
                MidpointRounding.AwayFromZero);
            index = Math.Max(0, Math.Min(index, values.Count - 1));
            return values[index];
        }

        private long GetBucketedPercentile(double percentile)
        {
            if (bucketValueCount == 0)
            {
                return -1;
            }

            // Use the same rank formula as Java, then walk the frequency
            // buckets as if every original sample had been sorted separately.
            var index = (long)Math.Round(
                percentile * bucketValueCount - 1,
                MidpointRounding.AwayFromZero);
            index = Math.Max(0, Math.Min(index, bucketValueCount - 1));

            sortedBucketKeys ??= buckets.Keys.OrderBy(value => value).ToArray();
            long cumulativeCount = 0;
            foreach (var key in sortedBucketKeys)
            {
                cumulativeCount += buckets[key];
                if (cumulativeCount > index)
                {
                    return key;
                }
            }

            return -1;
        }

        internal long Get95thPercentile() => GetPercentile(0.95d);

        internal long Get99thPercentile() => GetPercentile(0.99d);

        // Used by unit tests to verify that bucketed mode stores distinct
        // millisecond values rather than retaining every request sample.
        internal int StoredValueCount =>
            mode == StatsControl.PercentileMode.Exact ?
                values.Count : buckets.Count;

        internal void Clear()
        {
            if (mode == StatsControl.PercentileMode.Exact)
            {
                values.Clear();
                return;
            }

            buckets.Clear();
            bucketValueCount = 0;
            sortedBucketKeys = null;
        }
    }

    // Aggregates metrics for one request bucket such as Get, Put, Query, or
    // Table.  Failed requests still count errors/retries, but regular request
    // latency and size stats are collected only for successful requests.
    internal sealed class ReqStats
    {
        private readonly bool collectPercentiles;
        private readonly StatsControl.PercentileMode percentileMode;
        private long httpRequestCount;
        private long errors;
        private int reqSizeMin = int.MaxValue;
        private int reqSizeMax;
        private long reqSizeSum;
        private int resSizeMin = int.MaxValue;
        private int resSizeMax;
        private long resSizeSum;
        private int retryAuthCount;
        private int retryThrottleCount;
        private int retryCount;
        private int retryDelayMs;
        private int rateLimitDelayMs;
        private int requestLatencyMin = int.MaxValue;
        private int requestLatencyMax;
        private long requestLatencySum;
        private Percentile requestLatencyPercentile;

        internal ReqStats(bool collectPercentiles,
            StatsControl.PercentileMode percentileMode =
                StatsControl.PercentileMode.Exact)
        {
            this.collectPercentiles = collectPercentiles;
            this.percentileMode = percentileMode;
            if (collectPercentiles)
            {
                requestLatencyPercentile = new Percentile(percentileMode);
            }
        }

        internal long HttpRequestCount => httpRequestCount;

        internal ReqStats CreateEmpty() =>
            new ReqStats(collectPercentiles, percentileMode);

        internal void Observe(bool error, int retries, int retryDelay,
            int rateLimitDelay, int authCount, int throttleCount, int reqSize,
            int resSize, int requestLatency)
        {
            httpRequestCount++;
            retryCount += retries;
            retryDelayMs += retryDelay;
            retryAuthCount += authCount;
            retryThrottleCount += throttleCount;
            rateLimitDelayMs += rateLimitDelay;

            if (error)
            {
                errors++;
                return;
            }

            reqSizeMin = Math.Min(reqSizeMin, reqSize);
            reqSizeMax = Math.Max(reqSizeMax, reqSize);
            reqSizeSum += reqSize;

            resSizeMin = Math.Min(resSizeMin, resSize);
            resSizeMax = Math.Max(resSizeMax, resSize);
            resSizeSum += resSize;

            requestLatencyMin = Math.Min(requestLatencyMin, requestLatency);
            requestLatencyMax = Math.Max(requestLatencyMax, requestLatency);
            requestLatencySum += requestLatency;

            requestLatencyPercentile?.AddValue(requestLatency);
        }

        internal void ObserveQuery(bool error, int retries, int retryDelay,
            int rateLimitDelay, int authCount, int throttleCount, int reqSize,
            int resSize, int requestLatency)
        {
            // Nested query stats follow Java behavior: the HTTP attempt is
            // counted even on error, and missing values are represented by -1.
            httpRequestCount++;
            if (error)
            {
                errors++;
            }

            retryCount += retries;
            retryDelayMs += retryDelay;
            retryAuthCount += authCount;
            retryThrottleCount += throttleCount;
            rateLimitDelayMs += rateLimitDelay;

            reqSizeMin = Math.Min(reqSizeMin, reqSize);
            reqSizeMax = Math.Max(reqSizeMax, reqSize);
            reqSizeSum += reqSize;

            resSizeMin = Math.Min(resSizeMin, resSize);
            resSizeMax = Math.Max(resSizeMax, resSize);
            resSizeSum += resSize;

            requestLatencyMin = Math.Min(requestLatencyMin, requestLatency);
            requestLatencyMax = Math.Max(requestLatencyMax, requestLatency);
            requestLatencySum += requestLatency;

            requestLatencyPercentile?.AddValue(requestLatency);
        }

        internal void ToJson(string requestName, ArrayValue reqArray)
        {
            if (httpRequestCount == 0)
            {
                return;
            }

            var mapValue = new MapValue
            {
                ["name"] = requestName
            };
            ToMapValue(mapValue);
            reqArray.Add(mapValue);
        }

        internal void ToMapValue(MapValue mapValue)
        {
            mapValue["httpRequestCount"] = httpRequestCount;
            mapValue["errors"] = errors;

            mapValue["retry"] = new MapValue
            {
                ["count"] = retryCount,
                ["delayMs"] = retryDelayMs,
                ["authCount"] = retryAuthCount,
                ["throttleCount"] = retryThrottleCount
            };
            mapValue["rateLimitDelayMs"] = rateLimitDelayMs;

            var successCount = httpRequestCount - errors;
            if (successCount <= 0)
            {
                return;
            }

            if (requestLatencyMax > 0)
            {
                var latency = new MapValue
                {
                    ["min"] = requestLatencyMin,
                    ["max"] = requestLatencyMax,
                    ["avg"] = 1.0 * requestLatencySum / successCount
                };

                if (requestLatencyPercentile != null)
                {
                    latency["95th"] =
                        requestLatencyPercentile.Get95thPercentile();
                    latency["99th"] =
                        requestLatencyPercentile.Get99thPercentile();
                }

                mapValue["httpRequestLatencyMs"] = latency;
            }

            if (reqSizeMax > 0)
            {
                mapValue["requestSize"] = new MapValue
                {
                    ["min"] = reqSizeMin,
                    ["max"] = reqSizeMax,
                    ["avg"] = 1.0 * reqSizeSum / successCount
                };
            }

            if (resSizeMax > 0)
            {
                mapValue["resultSize"] = new MapValue
                {
                    ["min"] = resSizeMin,
                    ["max"] = resSizeMax,
                    ["avg"] = 1.0 * resSizeSum / successCount
                };
            }
        }

        internal void Clear()
        {
            httpRequestCount = 0;
            errors = 0;
            reqSizeMin = int.MaxValue;
            reqSizeMax = 0;
            reqSizeSum = 0;
            resSizeMin = int.MaxValue;
            resSizeMax = 0;
            resSizeSum = 0;
            retryAuthCount = 0;
            retryThrottleCount = 0;
            retryCount = 0;
            retryDelayMs = 0;
            rateLimitDelayMs = 0;
            requestLatencyMin = int.MaxValue;
            requestLatencyMax = 0;
            requestLatencySum = 0;
            requestLatencyPercentile?.Clear();
        }
    }

    // Tracks min/avg/max active connection samples.  The internal sample count
    // is used only to compute avg and is intentionally not emitted, matching
    // the Java StatsControl output shape.
    internal sealed class ConnectionStats
    {
        private long count;
        private int min = int.MaxValue;
        private int max;
        private long sum;

        internal void Observe(int connections)
        {
            min = Math.Min(min, connections);
            max = Math.Max(max, connections);
            sum += connections;
            count++;
        }

        internal void ToJson(MapValue root)
        {
            if (count == 0)
            {
                return;
            }

            root["connections"] = new MapValue
            {
                ["min"] = min,
                ["max"] = max,
                ["avg"] = 1.0 * sum / count
            };
        }

        internal void Clear()
        {
            count = 0;
            min = int.MaxValue;
            max = 0;
            sum = 0;
        }
    }

    // Extra per-query information emitted only for the ALL profile.  It keeps
    // a logical query count separate from the number of HTTP Query requests,
    // and may include SQL text and query plans like the Java SDK.
    internal sealed class ExtraQueryStats
    {
        private sealed class QueryEntryStat
        {
            internal long Count { get; set; }

            internal long Unprepared { get; set; }

            internal bool Simple { get; set; }

            internal bool DoesWrites { get; set; }

            internal ReqStats ReqStats { get; }

            internal string Plan { get; set; }

            internal QueryEntryStat(StatsControlImpl statsControl,
                QueryRequest queryRequest)
            {
                ReqStats = new ReqStats(
                    statsControl.GetProfile() >= StatsControl.Profile.More,
                    statsControl.PercentileStorageMode);
                UpdatePreparedInfo(queryRequest);
            }

            internal void UpdatePreparedInfo(QueryRequest queryRequest)
            {
                var preparedStatement = queryRequest.PreparedStatement;
                if (preparedStatement == null)
                {
                    return;
                }

                // Java Stats reports the locally executable driver plan, not
                // the optional server query-plan string.
                Plan ??= Query.PlanFormatter.Format(
                    preparedStatement.DriverQueryPlan);
                DoesWrites =
                    preparedStatement.OperationCode !=
                    QueryRequest.OperationCodeSelect;
            }
        }

        private const string NullQueryKey = "\0";

        private readonly Dictionary<string, QueryEntryStat> queries =
            new Dictionary<string, QueryEntryStat>();
        private readonly StatsControlImpl statsControl;

        internal ExtraQueryStats(StatsControlImpl statsControl)
        {
            this.statsControl = statsControl;
        }

        private QueryEntryStat GetExtraQueryStat(QueryRequest queryRequest)
        {
            var sql = queryRequest.Statement ??
                      queryRequest.PreparedStatement?.SQLText;
            var key = sql ?? NullQueryKey;

            if (!queries.TryGetValue(key, out var queryStat))
            {
                queryStat = new QueryEntryStat(statsControl, queryRequest);
                queries.Add(key, queryStat);
            }
            else
            {
                queryStat.UpdatePreparedInfo(queryRequest);
            }

            return queryStat;
        }

        internal void ObserveQuery(QueryRequest queryRequest)
        {
            var queryStat = GetExtraQueryStat(queryRequest);
            queryStat.Count++;
            if (!queryRequest.IsPreparedQuery)
            {
                queryStat.Unprepared++;
            }
            else
            {
                queryStat.Simple = queryRequest.PreparedStatement
                    .IsSimpleQuery;
            }
        }

        internal void ObserveQuery(Request request, bool error)
        {
            if (request is not QueryRequest queryRequest)
            {
                return;
            }

            var queryStat = GetExtraQueryStat(queryRequest);
            // Keep Java parity for nested query stats: failed query HTTP
            // requests are counted, with missing size/latency represented by
            // -1 before aggregation.
            queryStat.ReqStats.ObserveQuery(error,
                request.StatsRetryCount,
                request.StatsRetryDelayMs,
                request.StatsRateLimitDelayMs,
                request.StatsRetryAuthCount,
                request.StatsRetryThrottleCount,
                error ? -1 : request.StatsRequestSize,
                error ? -1 : request.StatsResponseSize,
                error ? -1 : request.StatsRequestLatencyMs);
        }

        internal void ToJson(MapValue root)
        {
            if (queries.Count == 0)
            {
                return;
            }

            var queryArray = new ArrayValue();
            root["queries"] = queryArray;

            foreach (var pair in queries)
            {
                var queryValue = new MapValue
                {
                    ["query"] = pair.Key == NullQueryKey ? "null" : pair.Key,
                    ["count"] = pair.Value.Count,
                    ["unprepared"] = pair.Value.Unprepared,
                    ["simple"] = pair.Value.Simple,
                    ["doesWrites"] = pair.Value.DoesWrites
                };

                if (pair.Value.Plan != null)
                {
                    queryValue["plan"] = pair.Value.Plan;
                }

                pair.Value.ReqStats.ToMapValue(queryValue);
                queryArray.Add(queryValue);
            }
        }

        internal void Clear()
        {
            queries.Clear();
        }
    }

    // Top-level aggregator.  Request observations may arrive from many
    // concurrent SDK operations while the scheduler is generating/clearing
    // snapshots, so all mutable buckets are protected by lockObj.
    internal sealed class Stats
    {
        private static readonly string[] RequestKeys =
        {
            "Delete", "Get", "GetIndexes", "GetTable", "ListTables",
            "MultiDelete", "Prepare", "Put", "Query", "System",
            "SystemStatus", "Table", "TableUsage", "WriteMultiple", "Write"
        };

        private readonly object lockObj = new object();
        private readonly StatsControlImpl statsControl;
        private Dictionary<string, ReqStats> requests;
        private ConnectionStats connectionStats;
        private ExtraQueryStats extraQueryStats;
        private DateTime startTime;
        private DateTime endTime;

        internal Stats(StatsControlImpl statsControl)
        {
            this.statsControl = statsControl;
            requests = CreateRequestBuckets();
            connectionStats = new ConnectionStats();

            if (statsControl.GetProfile() >= StatsControl.Profile.All)
            {
                extraQueryStats = new ExtraQueryStats(statsControl);
            }

            startTime = DateTime.UtcNow;
        }

        private bool CollectPercentiles =>
            statsControl.GetProfile() >= StatsControl.Profile.More;

        private bool CollectQueryStats =>
            statsControl.GetProfile() >= StatsControl.Profile.All;

        internal void Observe(Request request, bool error)
        {
            lock (lockObj)
            {
                var requestName = GetRequestName(request);
                if (!requests.TryGetValue(requestName, out var reqStats))
                {
                    reqStats = new ReqStats(CollectPercentiles,
                        statsControl.PercentileStorageMode);
                    requests[requestName] = reqStats;
                }

                reqStats.Observe(error,
                    request.StatsRetryCount,
                    request.StatsRetryDelayMs,
                    request.StatsRateLimitDelayMs,
                    request.StatsRetryAuthCount,
                    request.StatsRetryThrottleCount,
                    request.StatsRequestSize,
                    request.StatsResponseSize,
                    request.StatsRequestLatencyMs);

                connectionStats.Observe(request.StatsConnectionCount);

                if (CollectQueryStats)
                {
                    extraQueryStats ??= new ExtraQueryStats(statsControl);
                    extraQueryStats.ObserveQuery(request, error);
                }
            }
        }

        internal void ObserveQuery(QueryRequest queryRequest)
        {
            lock (lockObj)
            {
                if (CollectQueryStats)
                {
                    extraQueryStats ??= new ExtraQueryStats(statsControl);
                    extraQueryStats.ObserveQuery(queryRequest);
                }
            }
        }

        internal MapValue GenerateFieldValueStats(DateTime endTime)
        {
            lock (lockObj)
            {
                this.endTime = endTime;
                return GenerateFieldValueStats(startTime, this.endTime,
                    requests, connectionStats, extraQueryStats);
            }
        }

        internal MapValue GenerateAndClearFieldValueStats(DateTime endTime)
        {
            DateTime completedStartTime;
            Dictionary<string, ReqStats> completedRequests;
            ConnectionStats completedConnections;
            ExtraQueryStats completedQueries;

            lock (lockObj)
            {
                /*
                 * Atomically hand the completed interval to the reporting
                 * thread. New observations can use fresh buckets immediately,
                 * while percentile sorting and JSON generation happen outside
                 * the active request lock.
                 */
                completedStartTime = startTime;
                completedRequests = requests;
                completedConnections = connectionStats;
                completedQueries = extraQueryStats;

                requests = CreateRequestBuckets(completedRequests);
                connectionStats = new ConnectionStats();
                extraQueryStats = null;
                startTime = endTime;
                this.endTime = default;
            }

            return GenerateFieldValueStats(completedStartTime, endTime,
                completedRequests, completedConnections, completedQueries);
        }

        internal void ClearStats()
        {
            lock (lockObj)
            {
                requests = CreateRequestBuckets(requests);
                connectionStats = new ConnectionStats();
                extraQueryStats = null;
                startTime = DateTime.UtcNow;
                endTime = default;
            }
        }

        private Dictionary<string, ReqStats> CreateRequestBuckets(
            IReadOnlyDictionary<string, ReqStats> previous = null)
        {
            var result = new Dictionary<string, ReqStats>();
            if (previous != null)
            {
                foreach (var pair in previous)
                {
                    result[pair.Key] = pair.Value.CreateEmpty();
                }
                return result;
            }

            foreach (var key in RequestKeys)
            {
                result[key] = new ReqStats(CollectPercentiles,
                    statsControl.PercentileStorageMode);
            }
            return result;
        }

        private MapValue GenerateFieldValueStats(DateTime intervalStart,
            DateTime intervalEnd,
            IReadOnlyDictionary<string, ReqStats> intervalRequests,
            ConnectionStats intervalConnections,
            ExtraQueryStats intervalQueries)
        {
            // Generate the same high-level fields as Java:
            // startTime/endTime/clientId/connections/queries/requests.
            var root = new MapValue
            {
                ["startTime"] = TruncateToSecond(intervalStart),
                ["endTime"] = TruncateToSecond(intervalEnd),
                ["clientId"] = statsControl.Id
            };

            intervalConnections.ToJson(root);
            intervalQueries?.ToJson(root);

            var reqArray = new ArrayValue();
            root["requests"] = reqArray;
            foreach (var key in RequestKeys)
            {
                intervalRequests[key].ToJson(key, reqArray);
            }

            foreach (var pair in intervalRequests
                         .Where(pair => !RequestKeys.Contains(pair.Key)))
            {
                pair.Value.ToJson(pair.Key, reqArray);
            }

            return root;
        }

        internal static string GetRequestName(Request request)
        {
            // Normalize .NET request classes into the Java Stats request names
            // so cross-SDK output can be compared directly.
            return request switch
            {
                DeleteRangeRequest => "MultiDelete",
                WriteManyRequest => "WriteMultiple",
                PrepareRequest => "Prepare",
                QueryRequest => "Query",
                GetTableRequest => "GetTable",
                ListTablesRequest => "ListTables",
                GetTableUsageRequest => "TableUsage",
                GetIndexesRequest => "GetIndexes",
                GetReplicaStatsRequest => "ReplicaStats",
                AddReplicaRequest => "AddReplica",
                DropReplicaRequest => "DropReplica",
                AdminStatusRequest => "SystemStatus",
                AdminRequest => "System",
                TableOperationRequest => "Table",
                _ => GetRequestNameByType(request.GetType().Name)
            };
        }

        private static string GetRequestNameByType(string typeName)
        {
            var tickIndex = typeName.IndexOf('`');
            if (tickIndex >= 0)
            {
                typeName = typeName.Substring(0, tickIndex);
            }

            if (typeName.StartsWith("GetRequest",
                StringComparison.Ordinal))
            {
                return "Get";
            }
            if (typeName.StartsWith("Put",
                StringComparison.Ordinal))
            {
                return "Put";
            }
            if (typeName.StartsWith("Delete",
                StringComparison.Ordinal))
            {
                return "Delete";
            }

            return typeName.EndsWith("Request", StringComparison.Ordinal)
                ? typeName.Substring(0, typeName.Length - "Request".Length)
                : typeName;
        }

        private static DateTime TruncateToSecond(DateTime value)
        {
            var utc = value.ToUniversalTime();
            return new DateTime(
                utc.Ticks - utc.Ticks % TimeSpan.TicksPerSecond,
                DateTimeKind.Utc);
        }
    }
}
