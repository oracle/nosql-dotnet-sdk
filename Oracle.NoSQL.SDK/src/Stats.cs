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
        private readonly List<long> values = new List<long>();

        internal void AddValue(long value)
        {
            values.Add(value);
        }

        internal long GetPercentile(double percentile)
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

        internal long Get95thPercentile() => GetPercentile(0.95d);

        internal long Get99thPercentile() => GetPercentile(0.99d);

        internal void Clear()
        {
            values.Clear();
        }
    }

    // Aggregates metrics for one request bucket such as Get, Put, Query, or
    // Table.  Failed requests still count errors/retries, but regular request
    // latency and size stats are collected only for successful requests.
    internal sealed class ReqStats
    {
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

        internal ReqStats(bool collectPercentiles)
        {
            if (collectPercentiles)
            {
                requestLatencyPercentile = new Percentile();
            }
        }

        internal long HttpRequestCount => httpRequestCount;

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

            internal QueryEntryStat(StatsControl.Profile profile,
                QueryRequest queryRequest)
            {
                ReqStats = new ReqStats(profile >= StatsControl.Profile.More);
                UpdatePreparedInfo(queryRequest);
            }

            internal void UpdatePreparedInfo(QueryRequest queryRequest)
            {
                var preparedStatement = queryRequest.PreparedStatement;
                if (preparedStatement == null)
                {
                    return;
                }

                Plan ??= preparedStatement.QueryPlan;
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
                queryStat = new QueryEntryStat(
                    statsControl.GetProfile(), queryRequest);
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
    // concurrent SDK operations while the timer is generating/clearing
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
        private readonly Dictionary<string, ReqStats> requests =
            new Dictionary<string, ReqStats>();
        private readonly ConnectionStats connectionStats =
            new ConnectionStats();
        private ExtraQueryStats extraQueryStats;
        private DateTime startTime;
        private DateTime endTime;

        internal Stats(StatsControlImpl statsControl)
        {
            this.statsControl = statsControl;
            foreach (var key in RequestKeys)
            {
                requests[key] = new ReqStats(CollectPercentiles);
            }

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
                    reqStats = new ReqStats(CollectPercentiles);
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
                // Generate the same high-level fields as Java:
                // startTime/endTime/clientId/connections/queries/requests.
                this.endTime = endTime;
                var root = new MapValue
                {
                    ["startTime"] = TruncateToSecond(startTime),
                    ["endTime"] = TruncateToSecond(this.endTime),
                    ["clientId"] = statsControl.Id
                };

                connectionStats.ToJson(root);
                extraQueryStats?.ToJson(root);

                var reqArray = new ArrayValue();
                root["requests"] = reqArray;
                foreach (var key in RequestKeys)
                {
                    requests[key].ToJson(key, reqArray);
                }

                foreach (var pair in requests
                             .Where(pair => !RequestKeys.Contains(pair.Key)))
                {
                    pair.Value.ToJson(pair.Key, reqArray);
                }

                return root;
            }
        }

        internal void ClearStats()
        {
            lock (lockObj)
            {
                foreach (var reqStats in requests.Values)
                {
                    reqStats.Clear();
                }

                connectionStats.Clear();
                extraQueryStats?.Clear();
                startTime = DateTime.UtcNow;
                endTime = default;
            }
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
