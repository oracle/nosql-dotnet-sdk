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
    using System.Collections.ObjectModel;
    using System.Reflection;
    using Microsoft.Extensions.Logging;
    using Query;

    // Immutable input accepted by the stats collector. Request execution
    // creates an observation only after reaching a terminal outcome, so the
    // collector never depends on subsequently mutable Request state.
    internal readonly struct StatsObservation
    {
        internal enum ObservationKind
        {
            Request,
            Query
        }

        internal StatsObservation(ObservationKind kind, string requestName,
            bool error, long retryCount, long retryDelayMs,
            long rateLimitDelayMs, long retryAuthCount,
            long retryThrottleCount, int requestSize, int responseSize,
            int latencyMs, int connectionCount, bool hasQuery,
            QueryStatsObservation query)
        {
            Kind = kind;
            RequestName = requestName;
            Error = error;
            RetryCount = retryCount;
            RetryDelayMs = retryDelayMs;
            RateLimitDelayMs = rateLimitDelayMs;
            RetryAuthCount = retryAuthCount;
            RetryThrottleCount = retryThrottleCount;
            RequestSize = requestSize;
            ResponseSize = responseSize;
            LatencyMs = latencyMs;
            ConnectionCount = connectionCount;
            HasQuery = hasQuery;
            Query = query;
        }

        internal ObservationKind Kind { get; }

        internal string RequestName { get; }

        internal bool Error { get; }

        internal long RetryCount { get; }

        internal long RetryDelayMs { get; }

        internal long RateLimitDelayMs { get; }

        internal long RetryAuthCount { get; }

        internal long RetryThrottleCount { get; }

        internal int RequestSize { get; }

        internal int ResponseSize { get; }

        internal int LatencyMs { get; }

        internal int ConnectionCount { get; }

        internal bool HasQuery { get; }

        internal QueryStatsObservation Query { get; }

        internal static StatsObservation FromRequest(Request request,
            bool error, bool includeQuery)
        {
            var hasQuery = includeQuery && request is QueryRequest;
            var query = hasQuery
                ? QueryStatsObservation.FromRequest((QueryRequest)request)
                : default;

            return new StatsObservation(ObservationKind.Request,
                Stats.GetRequestName(request), error,
                request.StatsRetryCount, request.StatsRetryDelayMs,
                request.StatsRateLimitDelayMs,
                request.StatsRetryAuthCount,
                request.StatsRetryThrottleCount,
                request.StatsRequestSize, request.StatsResponseSize,
                request.StatsRequestLatencyMs,
                request.StatsConnectionCount, hasQuery, query);
        }

        internal static StatsObservation FromQuery(
            QueryRequest queryRequest)
        {
            return new StatsObservation(ObservationKind.Query, null, false,
                0, 0, 0, 0, 0, 0, 0, 0, 0, true,
                QueryStatsObservation.FromRequest(queryRequest));
        }
    }

    // Query metadata is captured with the observation so aggregation does not
    // retain a QueryRequest or PreparedStatement owned by request execution.
    internal readonly struct QueryStatsObservation
    {
        private QueryStatsObservation(string query, bool prepared,
            bool simple, bool doesWrites, string plan)
        {
            Query = query;
            Prepared = prepared;
            Simple = simple;
            DoesWrites = doesWrites;
            Plan = plan;
        }

        internal string Query { get; }

        internal bool Prepared { get; }

        internal bool Simple { get; }

        internal bool DoesWrites { get; }

        internal string Plan { get; }

        internal static QueryStatsObservation FromRequest(
            QueryRequest request)
        {
            var preparedStatement = request.PreparedStatement;
            return new QueryStatsObservation(
                request.Statement ?? preparedStatement?.SQLText,
                request.IsPreparedQuery,
                preparedStatement?.IsSimpleQuery == true,
                preparedStatement != null &&
                preparedStatement.OperationCode !=
                QueryRequest.OperationCodeSelect,
                FormatPlan(preparedStatement?.DriverQueryPlan));
        }

        private static string FormatPlan(PlanStep plan)
        {
            if (plan == null)
            {
                return null;
            }

            try
            {
                return PlanFormatter.Format(plan);
            }
            catch
            {
                // Plan text is optional diagnostic metadata. A malformed or
                // unsupported plan must not discard the request observation.
                return null;
            }
        }
    }

    // The collector owns mutable interval state. Rotation atomically detaches
    // a completed interval and returns an immutable typed snapshot. It has no
    // dependency on MapValue, JSON, logging, handlers, or scheduling.
    internal interface IStatsCollector
    {
        bool IncludesQueryDetails { get; }

        void UpdateProfile(StatsControl.Profile profile);

        void Record(in StatsObservation observation);

        StatsSnapshot Snapshot(DateTime endTime);

        StatsSnapshot Rotate(DateTime endTime);
    }

    internal readonly struct MetricStatsSnapshot
    {
        internal MetricStatsSnapshot(long min, long max, double average,
            long? percentile95 = null, long? percentile99 = null)
        {
            Min = min;
            Max = max;
            Average = average;
            Percentile95 = percentile95;
            Percentile99 = percentile99;
        }

        internal long Min { get; }

        internal long Max { get; }

        internal double Average { get; }

        internal long? Percentile95 { get; }

        internal long? Percentile99 { get; }
    }

    internal readonly struct RetryStatsSnapshot
    {
        internal RetryStatsSnapshot(long count, long delayMs,
            long authCount, long throttleCount)
        {
            Count = count;
            DelayMs = delayMs;
            AuthCount = authCount;
            ThrottleCount = throttleCount;
        }

        internal long Count { get; }

        internal long DelayMs { get; }

        internal long AuthCount { get; }

        internal long ThrottleCount { get; }
    }

    // Immutable metrics for one request bucket. Name is null when these
    // metrics are nested in a query entry.
    internal sealed class RequestStatsSnapshot
    {
        internal RequestStatsSnapshot(string name, long httpRequestCount,
            long errors, RetryStatsSnapshot retry, long rateLimitDelayMs,
            MetricStatsSnapshot? latency, MetricStatsSnapshot? requestSize,
            MetricStatsSnapshot? resultSize)
        {
            Name = name;
            HttpRequestCount = httpRequestCount;
            Errors = errors;
            Retry = retry;
            RateLimitDelayMs = rateLimitDelayMs;
            Latency = latency;
            RequestSize = requestSize;
            ResultSize = resultSize;
        }

        internal string Name { get; }

        internal long HttpRequestCount { get; }

        internal long Errors { get; }

        internal RetryStatsSnapshot Retry { get; }

        internal long RateLimitDelayMs { get; }

        internal MetricStatsSnapshot? Latency { get; }

        internal MetricStatsSnapshot? RequestSize { get; }

        internal MetricStatsSnapshot? ResultSize { get; }
    }

    internal sealed class QueryStatsSnapshot
    {
        internal QueryStatsSnapshot(string query, long count,
            long unprepared, bool simple, bool doesWrites, string plan,
            RequestStatsSnapshot requestStats)
        {
            Query = query;
            Count = count;
            Unprepared = unprepared;
            Simple = simple;
            DoesWrites = doesWrites;
            Plan = plan;
            RequestStats = requestStats;
        }

        internal string Query { get; }

        internal long Count { get; }

        internal long Unprepared { get; }

        internal bool Simple { get; }

        internal bool DoesWrites { get; }

        internal string Plan { get; }

        internal RequestStatsSnapshot RequestStats { get; }
    }

    internal readonly struct ConnectionStatsSnapshot
    {
        internal ConnectionStatsSnapshot(long min, long max, double average)
        {
            Min = min;
            Max = max;
            Average = average;
        }

        internal long Min { get; }

        internal long Max { get; }

        internal double Average { get; }
    }

    // Immutable data for one completed interval. Exporters can safely process
    // this after the collector has already started accepting the next interval.
    internal sealed class StatsSnapshot
    {
        internal StatsSnapshot(DateTime startTime, DateTime endTime,
            string clientId, ConnectionStatsSnapshot? connections,
            IList<QueryStatsSnapshot> queries,
            IList<RequestStatsSnapshot> requests)
        {
            StartTime = startTime;
            EndTime = endTime;
            ClientId = clientId;
            Connections = connections;
            Queries = new ReadOnlyCollection<QueryStatsSnapshot>(
                new List<QueryStatsSnapshot>(queries));
            Requests = new ReadOnlyCollection<RequestStatsSnapshot>(
                new List<RequestStatsSnapshot>(requests));
        }

        internal DateTime StartTime { get; }

        internal DateTime EndTime { get; }

        internal string ClientId { get; }

        internal ConnectionStatsSnapshot? Connections { get; }

        internal IReadOnlyList<QueryStatsSnapshot> Queries { get; }

        internal IReadOnlyList<RequestStatsSnapshot> Requests { get; }
    }

    // Converts the internal typed snapshot to the Java-compatible MapValue
    // contract. This is the only component that knows the output field names.
    internal sealed class JavaCompatibleMapValueExporter
    {
        internal MapValue Export(StatsSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return null;
            }

            var root = new MapValue
            {
                ["startTime"] = FormatTime(snapshot.StartTime),
                ["endTime"] = FormatTime(snapshot.EndTime),
                ["clientId"] = snapshot.ClientId
            };

            if (snapshot.Connections.HasValue)
            {
                var connections = snapshot.Connections.Value;
                root["connections"] = new MapValue
                {
                    ["min"] = connections.Min,
                    ["max"] = connections.Max,
                    ["avg"] = connections.Average
                };
            }

            if (snapshot.Queries.Count > 0)
            {
                var queries = new ArrayValue(snapshot.Queries.Count);
                root["queries"] = queries;
                foreach (var query in snapshot.Queries)
                {
                    var value = new MapValue
                    {
                        ["query"] = query.Query,
                        ["count"] = query.Count,
                        ["unprepared"] = query.Unprepared,
                        ["simple"] = query.Simple,
                        ["doesWrites"] = query.DoesWrites
                    };
                    if (query.Plan != null)
                    {
                        value["plan"] = query.Plan;
                    }
                    AppendRequestStats(value, query.RequestStats, false);
                    queries.Add(value);
                }
            }

            var requests = new ArrayValue(snapshot.Requests.Count);
            root["requests"] = requests;
            foreach (var request in snapshot.Requests)
            {
                var value = new MapValue();
                AppendRequestStats(value, request, true);
                requests.Add(value);
            }

            return root;
        }

        internal MapValue ExportRequest(RequestStatsSnapshot snapshot)
        {
            var value = new MapValue();
            AppendRequestStats(value, snapshot, snapshot.Name != null);
            return value;
        }

        private static void AppendRequestStats(MapValue value,
            RequestStatsSnapshot snapshot, bool includeName)
        {
            if (includeName)
            {
                value["name"] = snapshot.Name;
            }

            value["httpRequestCount"] = snapshot.HttpRequestCount;
            value["errors"] = snapshot.Errors;
            value["retry"] = new MapValue
            {
                ["count"] = snapshot.Retry.Count,
                ["delayMs"] = snapshot.Retry.DelayMs,
                ["authCount"] = snapshot.Retry.AuthCount,
                ["throttleCount"] = snapshot.Retry.ThrottleCount
            };
            value["rateLimitDelayMs"] = snapshot.RateLimitDelayMs;

            if (snapshot.Latency.HasValue)
            {
                value["httpRequestLatencyMs"] = ToMapValue(
                    snapshot.Latency.Value, true);
            }
            if (snapshot.RequestSize.HasValue)
            {
                value["requestSize"] = ToMapValue(
                    snapshot.RequestSize.Value, false);
            }
            if (snapshot.ResultSize.HasValue)
            {
                value["resultSize"] = ToMapValue(
                    snapshot.ResultSize.Value, false);
            }
        }

        private static MapValue ToMapValue(MetricStatsSnapshot snapshot,
            bool includePercentiles)
        {
            var value = new MapValue
            {
                ["min"] = snapshot.Min,
                ["max"] = snapshot.Max,
                ["avg"] = snapshot.Average
            };
            if (includePercentiles && snapshot.Percentile95.HasValue)
            {
                value["95th"] = snapshot.Percentile95.Value;
                value["99th"] = snapshot.Percentile99.Value;
            }
            return value;
        }

        private static string FormatTime(DateTime value) =>
            value.ToUniversalTime().ToString("yyyy-MM-dd'T'HH:mm:ss'Z'");
    }

    // Exporters consume one Java-compatible view of each immutable snapshot.
    // The logger runs before the handler, so handler mutation cannot change
    // the already-emitted log record.
    internal interface IStatsExporter
    {
        void Export(MapValue snapshot);
    }

    internal sealed class HandlerStatsExporter : IStatsExporter
    {
        private readonly Func<StatsControl.StatsHandler> getHandler;

        internal HandlerStatsExporter(
            Func<StatsControl.StatsHandler> getHandler)
        {
            this.getHandler = getHandler;
        }

        public void Export(MapValue snapshot)
        {
            var handler = getHandler();
            if (handler != null)
            {
                handler(snapshot);
            }
        }
    }

    internal sealed class LoggerStatsExporter : IStatsExporter
    {
        private readonly bool enabled;
        private readonly ILogger logger;
        private readonly Func<bool> getPrettyPrint;

        internal LoggerStatsExporter(bool enabled, ILogger logger,
            Func<bool> getPrettyPrint)
        {
            this.enabled = enabled;
            this.logger = logger;
            this.getPrettyPrint = getPrettyPrint;
        }

        public void Export(MapValue snapshot)
        {
            if (!enabled)
            {
                return;
            }

            logger.LogInformation("{StatsLog}", StatsControl.LogPrefix +
                snapshot.ToJsonString(getPrettyPrint()
                    ? new JsonOutputOptions { Indented = true }
                    : null));
        }

        internal void LogStatsError(Exception exception)
        {
            if (!enabled)
            {
                return;
            }

            try
            {
                // Java reports scheduler/export failures at INFO level.
                logger.LogInformation(exception,
                    "Stats exception: {ErrorMessage}", exception.Message);
            }
            catch
            {
                // A failing application logger must not affect requests or
                // the stats scheduler.
            }
        }

        internal void LogStartup(string clientId,
            StatsControl.Profile profile, TimeSpan interval,
            bool prettyPrint, bool rateLimitingEnabled)
        {
            if (!enabled)
            {
                return;
            }

            var startup = new MapValue
            {
                ["sdkName"] = "Oracle NoSQL SDK for .NET",
                ["sdkVersion"] = GetLibraryVersion(),
                ["clientId"] = clientId,
                ["profile"] = profile.ToString().ToUpperInvariant(),
                ["intervalSec"] = (int)interval.TotalSeconds,
                ["prettyPrint"] = prettyPrint,
                ["rateLimitingEnabled"] = rateLimitingEnabled
            };

            logger.LogInformation("{StatsLog}",
                StatsControl.LogPrefix + startup.ToJsonString());
        }

        private static string GetLibraryVersion()
        {
            return typeof(NoSQLClient).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ??
                typeof(NoSQLClient).Assembly.GetName().Version?.ToString() ??
                string.Empty;
        }
    }
}
