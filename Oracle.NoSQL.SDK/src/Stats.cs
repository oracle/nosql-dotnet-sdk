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

    // Internal implementation of Java-compatible client statistics.  The
    // public surface is StatsControl; this file owns aggregation buckets and
    // creates immutable typed snapshots for independent exporters.
    internal sealed class Percentile
    {
        // Latencies are integral milliseconds, so retain one counter per
        // distinct value instead of one list entry per successful request.
        // This preserves exact percentile results while bounding storage by
        // the number of distinct observed latency values.
        private readonly SortedDictionary<long, long> frequencies =
            new SortedDictionary<long, long>();
        private long valueCount;

        internal void AddValue(long value)
        {
            frequencies.TryGetValue(value, out var frequency);
            frequencies[value] = frequency + 1;
            valueCount++;
        }

        internal long GetPercentile(double percentile)
        {
            if (valueCount == 0)
            {
                return -1;
            }

            // Keep the same exact percentile calculation as the Java SDK:
            // use round(percentile * count - 1), then locate that rank in the
            // ordered frequency table.
            var index = (long)Math.Round(
                percentile * valueCount - 1,
                MidpointRounding.AwayFromZero);
            index = Math.Max(0, Math.Min(index, valueCount - 1));

            long cumulativeCount = 0;
            long lastValue = -1;
            foreach (var pair in frequencies)
            {
                lastValue = pair.Key;
                cumulativeCount += pair.Value;
                if (index < cumulativeCount)
                {
                    return pair.Key;
                }
            }

            return lastValue;
        }

        internal long Get95thPercentile() => GetPercentile(0.95d);

        internal long Get99thPercentile() => GetPercentile(0.99d);

        internal void Clear()
        {
            frequencies.Clear();
            valueCount = 0;
        }
    }

    // Aggregates metrics for one request bucket such as Get, Put, Query, or
    // Table.  Failed requests still count errors/retries, but regular request
    // latency and size stats are collected only for successful requests.
    internal sealed class ReqStats
    {
        private readonly bool collectPercentiles;
        private long httpRequestCount;
        private long errors;
        private int reqSizeMin = int.MaxValue;
        private int reqSizeMax;
        private long reqSizeSum;
        private int resSizeMin = int.MaxValue;
        private int resSizeMax;
        private long resSizeSum;
        private long retryAuthCount;
        private long retryThrottleCount;
        private long retryCount;
        private long retryDelayMs;
        private long rateLimitDelayMs;
        private int requestLatencyMin = int.MaxValue;
        private int requestLatencyMax;
        private long requestLatencySum;
        private Percentile requestLatencyPercentile;

        internal ReqStats(bool collectPercentiles)
        {
            this.collectPercentiles = collectPercentiles;
            if (collectPercentiles)
            {
                requestLatencyPercentile = new Percentile();
            }
        }

        internal long HttpRequestCount => httpRequestCount;

        internal ReqStats CreateEmpty() =>
            new ReqStats(collectPercentiles);

        internal void Observe(bool error, long retries, long retryDelay,
            long rateLimitDelay, long authCount, long throttleCount,
            int reqSize, int resSize, int requestLatency)
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

        internal RequestStatsSnapshot CreateSnapshot(string requestName,
            bool includeEmpty = false)
        {
            if (httpRequestCount == 0 && !includeEmpty)
            {
                return null;
            }

            var successCount = httpRequestCount - errors;
            MetricStatsSnapshot? latency = null;
            MetricStatsSnapshot? requestSize = null;
            MetricStatsSnapshot? resultSize = null;
            if (successCount > 0 && requestLatencyMax > 0)
            {
                latency = new MetricStatsSnapshot(requestLatencyMin,
                    requestLatencyMax,
                    1.0 * requestLatencySum / successCount,
                    requestLatencyPercentile?.Get95thPercentile(),
                    requestLatencyPercentile?.Get99thPercentile());
            }
            if (successCount > 0 && reqSizeMax > 0)
            {
                requestSize = new MetricStatsSnapshot(reqSizeMin, reqSizeMax,
                    1.0 * reqSizeSum / successCount);
            }
            if (successCount > 0 && resSizeMax > 0)
            {
                resultSize = new MetricStatsSnapshot(resSizeMin, resSizeMax,
                    1.0 * resSizeSum / successCount);
            }

            return new RequestStatsSnapshot(requestName, httpRequestCount,
                errors, new RetryStatsSnapshot(retryCount, retryDelayMs,
                    retryAuthCount, retryThrottleCount), rateLimitDelayMs,
                latency, requestSize, resultSize);
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

        internal ConnectionStatsSnapshot? CreateSnapshot()
        {
            if (count == 0)
            {
                return null;
            }

            return new ConnectionStatsSnapshot(min, max,
                1.0 * sum / count);
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

            internal QueryEntryStat(bool collectPercentiles,
                in QueryStatsObservation query)
            {
                ReqStats = new ReqStats(collectPercentiles);
                UpdatePreparedInfo(query);
            }

            internal void UpdatePreparedInfo(
                in QueryStatsObservation query)
            {
                if (!query.Prepared)
                {
                    return;
                }

                // Java Stats reports the locally executable driver plan, not
                // the optional server query-plan string.
                Plan ??= query.Plan;
                DoesWrites = query.DoesWrites;
            }
        }

        private const string NullQueryKey = "\0";

        private readonly Dictionary<string, QueryEntryStat> queries =
            new Dictionary<string, QueryEntryStat>();
        private readonly bool collectPercentiles;

        internal ExtraQueryStats(bool collectPercentiles)
        {
            this.collectPercentiles = collectPercentiles;
        }

        private QueryEntryStat GetExtraQueryStat(
            in QueryStatsObservation query)
        {
            var key = query.Query ?? NullQueryKey;

            if (!queries.TryGetValue(key, out var queryStat))
            {
                queryStat = new QueryEntryStat(collectPercentiles, query);
                queries.Add(key, queryStat);
            }
            else
            {
                queryStat.UpdatePreparedInfo(query);
            }

            return queryStat;
        }

        internal void ObserveQuery(in QueryStatsObservation query)
        {
            var queryStat = GetExtraQueryStat(query);
            queryStat.Count++;
            if (!query.Prepared)
            {
                queryStat.Unprepared++;
            }
            else
            {
                queryStat.Simple = query.Simple;
            }
        }

        internal void ObserveRequest(in StatsObservation observation)
        {
            if (!observation.HasQuery)
            {
                return;
            }

            var queryStat = GetExtraQueryStat(observation.Query);
            // Failed query HTTP requests contribute errors and retry fields,
            // but not latency or size measurements, matching request buckets.
            queryStat.ReqStats.Observe(observation.Error,
                observation.RetryCount,
                observation.RetryDelayMs,
                observation.RateLimitDelayMs,
                observation.RetryAuthCount,
                observation.RetryThrottleCount,
                observation.RequestSize,
                observation.ResponseSize,
                observation.LatencyMs);
        }

        internal IList<QueryStatsSnapshot> CreateSnapshot()
        {
            var result = new List<QueryStatsSnapshot>(queries.Count);
            if (queries.Count == 0)
            {
                return result;
            }

            foreach (var pair in queries)
            {
                result.Add(new QueryStatsSnapshot(
                    pair.Key == NullQueryKey ? "null" : pair.Key,
                    pair.Value.Count, pair.Value.Unprepared,
                    pair.Value.Simple, pair.Value.DoesWrites,
                    pair.Value.Plan,
                    pair.Value.ReqStats.CreateSnapshot(null, true)));
            }

            return result;
        }

        internal void Clear()
        {
            queries.Clear();
        }
    }

    // Top-level aggregator.  Request observations may arrive from many
    // concurrent SDK operations while the scheduler is generating/clearing
    // snapshots, so all mutable buckets are protected by lockObj.
    internal sealed class Stats : IStatsCollector
    {
        private static readonly string[] RequestKeys =
        {
            "Delete", "Get", "GetIndexes", "GetTable", "ListTables",
            "MultiDelete", "Prepare", "Put", "Query", "System",
            "SystemStatus", "Table", "TableUsage", "WriteMultiple", "Write"
        };

        private readonly object lockObj = new object();
        private readonly string clientId;
        private readonly bool collectPercentiles;
        private volatile StatsControl.Profile profile;
        private Dictionary<string, ReqStats> requests;
        private ConnectionStats connectionStats;
        private ExtraQueryStats extraQueryStats;
        private DateTime startTime;

        internal Stats(StatsControlImpl statsControl)
            : this(statsControl.Id, statsControl.GetProfile())
        {
        }

        internal Stats(string clientId, StatsControl.Profile profile)
        {
            this.clientId = clientId;
            this.profile = profile;
            collectPercentiles = profile >= StatsControl.Profile.More;
            requests = CreateRequestBuckets();
            connectionStats = new ConnectionStats();

            if (CollectQueryStats)
            {
                extraQueryStats = new ExtraQueryStats(
                    CollectQueryPercentiles);
            }

            startTime = DateTime.UtcNow;
        }

        private bool CollectPercentiles =>
            collectPercentiles;

        private bool CollectQueryStats =>
            profile >= StatsControl.Profile.All;

        // Java evaluates the current profile when creating query statistics.
        // Top-level request buckets keep their original percentile capability,
        // while query buckets created after switching to ALL include them.
        private bool CollectQueryPercentiles =>
            profile >= StatsControl.Profile.More;

        public bool IncludesQueryDetails => CollectQueryStats;

        public void UpdateProfile(StatsControl.Profile profile)
        {
            this.profile = profile;
        }

        public void Record(in StatsObservation observation)
        {
            lock (lockObj)
            {
                if (observation.Kind ==
                    StatsObservation.ObservationKind.Query)
                {
                    if (CollectQueryStats)
                    {
                        extraQueryStats ??=
                            new ExtraQueryStats(CollectQueryPercentiles);
                        extraQueryStats.ObserveQuery(observation.Query);
                    }
                    return;
                }

                if (!requests.TryGetValue(observation.RequestName,
                        out var reqStats))
                {
                    reqStats = new ReqStats(CollectPercentiles);
                    requests[observation.RequestName] = reqStats;
                }

                reqStats.Observe(observation.Error,
                    observation.RetryCount,
                    observation.RetryDelayMs,
                    observation.RateLimitDelayMs,
                    observation.RetryAuthCount,
                    observation.RetryThrottleCount,
                    observation.RequestSize,
                    observation.ResponseSize,
                    observation.LatencyMs);

                connectionStats.Observe(observation.ConnectionCount);

                if (CollectQueryStats && observation.HasQuery)
                {
                    extraQueryStats ??=
                        new ExtraQueryStats(CollectQueryPercentiles);
                    extraQueryStats.ObserveRequest(observation);
                }
            }
        }

        internal void Observe(Request request, bool error)
        {
            var observation = StatsObservation.FromRequest(request, error,
                CollectQueryStats);
            Record(observation);
        }

        internal void ObserveQuery(QueryRequest queryRequest)
        {
            var observation = StatsObservation.FromQuery(queryRequest);
            Record(observation);
        }

        public StatsSnapshot Snapshot(DateTime endTime)
        {
            lock (lockObj)
            {
                return CreateSnapshot(startTime, endTime, requests,
                    connectionStats, extraQueryStats);
            }
        }

        public StatsSnapshot Rotate(DateTime endTime)
        {
            DateTime completedStartTime;
            Dictionary<string, ReqStats> completedRequests;
            ConnectionStats completedConnections;
            ExtraQueryStats completedQueries;

            lock (lockObj)
            {
                // Detach the completed interval under a short lock. New
                // observations immediately use fresh buckets while snapshot
                // materialization proceeds outside the request-path lock.
                completedStartTime = startTime;
                completedRequests = requests;
                completedConnections = connectionStats;
                completedQueries = extraQueryStats;

                requests = CreateRequestBuckets(completedRequests);
                connectionStats = new ConnectionStats();
                extraQueryStats = CollectQueryStats
                    ? new ExtraQueryStats(CollectQueryPercentiles)
                    : null;
                startTime = endTime;
            }

            return CreateSnapshot(completedStartTime, endTime,
                completedRequests, completedConnections, completedQueries);
        }

        internal void ClearStats()
        {
            lock (lockObj)
            {
                requests = CreateRequestBuckets(requests);
                connectionStats = new ConnectionStats();
                extraQueryStats = CollectQueryStats
                    ? new ExtraQueryStats(CollectQueryPercentiles)
                    : null;
                startTime = DateTime.UtcNow;
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
                result[key] = new ReqStats(CollectPercentiles);
            }
            return result;
        }

        private StatsSnapshot CreateSnapshot(DateTime intervalStart,
            DateTime intervalEnd,
            IReadOnlyDictionary<string, ReqStats> intervalRequests,
            ConnectionStats intervalConnections,
            ExtraQueryStats intervalQueries)
        {
            var requestSnapshots = new List<RequestStatsSnapshot>();
            foreach (var key in RequestKeys)
            {
                var request = intervalRequests[key].CreateSnapshot(key);
                if (request != null)
                {
                    requestSnapshots.Add(request);
                }
            }

            foreach (var pair in intervalRequests)
            {
                if (Array.IndexOf(RequestKeys, pair.Key) >= 0)
                {
                    continue;
                }

                var request = pair.Value.CreateSnapshot(pair.Key);
                if (request != null)
                {
                    requestSnapshots.Add(request);
                }
            }

            return new StatsSnapshot(intervalStart, intervalEnd, clientId,
                intervalConnections.CreateSnapshot(),
                intervalQueries?.CreateSnapshot() ??
                new List<QueryStatsSnapshot>(), requestSnapshots);
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

    }
}
