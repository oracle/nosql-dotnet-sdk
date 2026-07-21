/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using SDK;
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    // Unit tests for in-memory stats aggregation and StatsControl lifecycle.
    // These do not require CloudSim; they seed request stats fields directly
    // and verify Java-compatible snapshot structure and calculations.
    [TestClass]
    [DoNotParallelize]
    public class StatsTests
    {
        private static readonly JavaCompatibleMapValueExporter
            MapValueExporter = new JavaCompatibleMapValueExporter();

        private static readonly NoSQLConfig TestConfig = new NoSQLConfig
        {
            Endpoint = "localhost:8080"
        };

        private static MapValue FindRequest(MapValue stats, string name)
        {
            var requests = stats["requests"].AsArrayValue;
            return requests
                .Select(value => value.AsMapValue)
                .SingleOrDefault(value =>
                    value["name"].AsString == name);
        }

        private static long AsLong(FieldValue value) => value.ToInt64();

        private static MapValue ToMapValue(StatsSnapshot snapshot) =>
            MapValueExporter.Export(snapshot);

        private static MapValue ToMapValue(ReqStats stats) =>
            MapValueExporter.ExportRequest(
                stats.CreateSnapshot(null, true));

        private static MapValue GenerateStats(Stats stats) =>
            ToMapValue(stats.Snapshot(DateTime.UtcNow));

        private static MapValue RotateStats(Stats stats) =>
            ToMapValue(stats.Rotate(DateTime.UtcNow));

        private static long GetRequestCount(MapValue stats, string name)
        {
            var request = FindRequest(stats, name);
            return request == null ? 0 :
                AsLong(request["httpRequestCount"]);
        }

        private static void AssertKeys(MapValue map, params string[] keys)
        {
            CollectionAssert.AreEqual(keys, map.Keys.ToArray());
        }

        private static void AssertMinAvgMax(MapValue map, int min,
            double avg, int max)
        {
            Assert.AreEqual(min, AsLong(map["min"]));
            Assert.AreEqual(avg, map["avg"].AsDouble);
            Assert.AreEqual(max, AsLong(map["max"]));
        }

        private static GetRequest<RecordValue> MakeGetRequest(
            NoSQLClient client)
        {
            return new GetRequest<RecordValue>(client, "Users",
                new MapValue
                {
                    ["id"] = 1
                }, null);
        }

        private static QueryRequest<RecordValue> MakeQueryRequest(
            NoSQLClient client)
        {
            return new QueryRequest<RecordValue>(client,
                "SELECT * FROM Users", null);
        }

        private static MapValue MakeKey() => new MapValue
        {
            ["id"] = 1
        };

        private static MapValue MakeRow() => new MapValue
        {
            ["id"] = 1,
            ["name"] = "user-1"
        };

        private static void SetSuccessStats(Request request,
            int requestSize = 50, int responseSize = 40, int latency = 10,
            int connections = 1)
        {
            request.StatsRequestSize = requestSize;
            request.StatsResponseSize = responseSize;
            request.StatsRequestLatencyMs = latency;
            request.StatsConnectionCount = connections;
        }

        private static void AssertRequestName(string expected,
            Request request)
        {
            Assert.AreEqual(expected, Stats.GetRequestName(request));
        }

        [TestMethod]
        public void TestPercentileFrequencyAggregationPreservesExactRanks()
        {
            var percentile = new Percentile();

            for (var i = 0; i < 94; i++)
            {
                percentile.AddValue(1);
            }
            for (var i = 0; i < 4; i++)
            {
                percentile.AddValue(10);
            }
            for (var i = 0; i < 2; i++)
            {
                percentile.AddValue(20);
            }

            Assert.AreEqual(10, percentile.Get95thPercentile());
            Assert.AreEqual(20, percentile.Get99thPercentile());

            percentile.Clear();
            Assert.AreEqual(-1, percentile.Get95thPercentile());
        }

        [TestMethod]
        public void TestReqStatsSuccessAggregationAndPercentiles()
        {
            var reqStats = new ReqStats(true);

            reqStats.Observe(false, 1, 25, 5, 0, 1, 50, 40, 1);
            reqStats.Observe(false, 0, 0, 0, 0, 0, 70, 90, 2);
            reqStats.Observe(false, 0, 0, 0, 0, 0, 80, 120, 3);
            reqStats.Observe(false, 0, 0, 0, 0, 0, 90, 130, 4);
            reqStats.Observe(false, 0, 0, 0, 0, 0, 100, 140, 100);

            var map = ToMapValue(reqStats);

            Assert.AreEqual(5, AsLong(map["httpRequestCount"]));
            Assert.AreEqual(0, AsLong(map["errors"]));
            Assert.AreEqual(1, AsLong(map["retry"].AsMapValue["count"]));
            Assert.AreEqual(25,
                AsLong(map["retry"].AsMapValue["delayMs"]));
            Assert.AreEqual(1,
                AsLong(map["retry"].AsMapValue["throttleCount"]));
            Assert.AreEqual(5, AsLong(map["rateLimitDelayMs"]));

            var latency = map["httpRequestLatencyMs"].AsMapValue;
            Assert.AreEqual(1, AsLong(latency["min"]));
            Assert.AreEqual(100, AsLong(latency["max"]));
            Assert.AreEqual(22.0, latency["avg"].AsDouble);
            Assert.AreEqual(100, AsLong(latency["95th"]));
            Assert.AreEqual(100, AsLong(latency["99th"]));

            Assert.AreEqual(50,
                AsLong(map["requestSize"].AsMapValue["min"]));
            Assert.AreEqual(100,
                AsLong(map["requestSize"].AsMapValue["max"]));
            Assert.AreEqual(78.0,
                map["requestSize"].AsMapValue["avg"].AsDouble);
            Assert.AreEqual(40,
                AsLong(map["resultSize"].AsMapValue["min"]));
            Assert.AreEqual(140,
                AsLong(map["resultSize"].AsMapValue["max"]));
        }

        [TestMethod]
        public void TestReqStatsErrorsDoNotAffectLatencyOrSizes()
        {
            var reqStats = new ReqStats(false);

            reqStats.Observe(true, 2, 100, 0, 1, 1, 500, 600, 700);
            reqStats.Observe(false, 0, 0, 0, 0, 0, 50, 40, 10);

            var map = ToMapValue(reqStats);

            Assert.AreEqual(2, AsLong(map["httpRequestCount"]));
            Assert.AreEqual(1, AsLong(map["errors"]));
            Assert.AreEqual(2, AsLong(map["retry"].AsMapValue["count"]));
            Assert.AreEqual(100,
                AsLong(map["retry"].AsMapValue["delayMs"]));
            Assert.AreEqual(1,
                AsLong(map["retry"].AsMapValue["authCount"]));
            Assert.AreEqual(1,
                AsLong(map["retry"].AsMapValue["throttleCount"]));

            Assert.AreEqual(10,
                map["httpRequestLatencyMs"].AsMapValue["avg"].AsDouble);
            Assert.AreEqual(50,
                map["requestSize"].AsMapValue["avg"].AsDouble);
            Assert.AreEqual(40,
                map["resultSize"].AsMapValue["avg"].AsDouble);
        }

        [TestMethod]
        public void TestReqStatsAllErrorBucketOmitsLatencyAndSizes()
        {
            var reqStats = new ReqStats(true);

            reqStats.Observe(true, 1, 50, 0, 0, 1, 100, 100, 100);

            var map = ToMapValue(reqStats);

            Assert.AreEqual(1, AsLong(map["httpRequestCount"]));
            Assert.AreEqual(1, AsLong(map["errors"]));
            Assert.IsFalse(map.ContainsKey("httpRequestLatencyMs"));
            Assert.IsFalse(map.ContainsKey("requestSize"));
            Assert.IsFalse(map.ContainsKey("resultSize"));
        }

        [TestMethod]
        public void TestReqStatsRetryAggregatesDoNotOverflowIntBoundary()
        {
            var reqStats = new ReqStats(false);

            reqStats.Observe(true, int.MaxValue, int.MaxValue, 0,
                int.MaxValue, int.MaxValue, 0, 0, 0);
            reqStats.Observe(true, int.MaxValue, int.MaxValue, 0,
                int.MaxValue, int.MaxValue, 0, 0, 0);

            var map = ToMapValue(reqStats);
            var retry = map["retry"].AsMapValue;
            var expected = 2L * int.MaxValue;

            Assert.AreEqual(expected, AsLong(retry["count"]));
            Assert.AreEqual(expected, AsLong(retry["delayMs"]));
            Assert.AreEqual(expected, AsLong(retry["authCount"]));
            Assert.AreEqual(expected, AsLong(retry["throttleCount"]));
        }

        [TestMethod]
        public void TestStatsMillisecondsUsesJavaStyleTruncation()
        {
            Assert.AreEqual(0, Request.ToStatsMilliseconds(
                TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond - 1)));
            Assert.AreEqual(1, Request.ToStatsMilliseconds(
                TimeSpan.FromTicks(TimeSpan.TicksPerMillisecond +
                                   TimeSpan.TicksPerMillisecond / 2)));
            Assert.AreEqual(int.MaxValue, Request.ToStatsMilliseconds(
                TimeSpan.FromMilliseconds((double)int.MaxValue + 1)));
        }

        [TestMethod]
        public void TestStatsConfigJsonParsing()
        {
            var config = NoSQLConfig.FromJsonString(
                @"{
                    ""Endpoint"": ""localhost:8080"",
                    ""StatsProfile"": ""all"",
                    ""StatsInterval"": 5000,
                    ""StatsPrettyPrint"": true,
                    ""StatsEnableLog"": false
                }");

            Assert.AreEqual("localhost:8080", config.Endpoint);
            Assert.AreEqual(StatsControl.Profile.All, config.StatsProfile);
            Assert.AreEqual(TimeSpan.FromSeconds(5), config.StatsInterval);
            Assert.IsTrue(config.StatsPrettyPrint);
            Assert.IsFalse(config.StatsEnableLog);

            var numericProfile = NoSQLConfig.FromJsonString(
                @"{
                    ""Endpoint"": ""localhost:8080"",
                    ""StatsProfile"": 2
                }");

            Assert.AreEqual(StatsControl.Profile.More,
                numericProfile.StatsProfile);
        }

        [TestMethod]
        public void TestRequestInitClearsStatsFields()
        {
            using var client = new NoSQLClient(TestConfig);
            var request = MakeGetRequest(client);

            request.RecordStatsRetry(new InvalidAuthorizationException(),
                TimeSpan.FromMilliseconds(7));
            request.RecordStatsRetry(new ReadThrottlingException(),
                TimeSpan.FromMilliseconds(9));
            request.AddStatsServerRateLimitDelay(11);
            SetSuccessStats(request, 100, 200, 30, 4);

            request.Init();

            Assert.AreEqual(0, request.StatsRetryCount);
            Assert.AreEqual(0, request.StatsRetryDelayMs);
            Assert.AreEqual(0, request.StatsRetryAuthCount);
            Assert.AreEqual(0, request.StatsRetryThrottleCount);
            Assert.AreEqual(0, request.StatsRateLimitDelayMs);
            Assert.AreEqual(0, request.StatsRequestSize);
            Assert.AreEqual(0, request.StatsResponseSize);
            Assert.AreEqual(0, request.StatsRequestLatencyMs);
            Assert.AreEqual(0, request.StatsConnectionCount);
        }

        [TestMethod]
        public void TestServerAndLocalRateLimitDelayAreCombined()
        {
            using var client = new NoSQLClient(TestConfig);
            var request = MakeGetRequest(client);
            using var response = new HttpResponseMessage();
            response.Headers.TryAddWithoutValidation(
                "x-nosql-rl-delay-ms", "37");

            var serverDelay = Http.Client
                .GetRateLimitDelayFromHeader(response);
            Assert.AreEqual(37, serverDelay);

            request.AddStatsServerRateLimitDelay(serverDelay);
            request.SetStatsLocalRateLimitDelay(5);
            Assert.AreEqual(42, request.StatsRateLimitDelayMs);

            // A later server value and cumulative local value must replace
            // neither component nor count the earlier local delay twice.
            request.AddStatsServerRateLimitDelay(3);
            request.SetStatsLocalRateLimitDelay(8);
            Assert.AreEqual(48, request.StatsRateLimitDelayMs);

            SetSuccessStats(request);
            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.Regular,
                StatsEnableLog = false
            }, false);
            control.Observe(request);
            var getStats = FindRequest(
                control.LogClientStatsForTest(), "Get");
            Assert.AreEqual(48, AsLong(getStats["rateLimitDelayMs"]));

            using var malformedResponse = new HttpResponseMessage();
            malformedResponse.Headers.TryAddWithoutValidation(
                HttpConstants.RateLimitDelay, "not-a-number");
            Assert.AreEqual(0, Http.Client.GetRateLimitDelayFromHeader(
                malformedResponse));

            using var negativeResponse = new HttpResponseMessage();
            negativeResponse.Headers.TryAddWithoutValidation(
                HttpConstants.RateLimitDelay, "-1");
            Assert.AreEqual(0, Http.Client.GetRateLimitDelayFromHeader(
                negativeResponse));
        }

        [TestMethod]
        public void TestRateLimitDelayDoesNotOverflowIntBoundary()
        {
            using var client = new NoSQLClient(TestConfig);
            var request = MakeGetRequest(client);

            request.AddStatsServerRateLimitDelay(int.MaxValue);
            request.AddStatsServerRateLimitDelay(int.MaxValue);
            request.SetStatsLocalRateLimitDelay(int.MaxValue);

            var expectedDelay = 3L * int.MaxValue;
            Assert.AreEqual(expectedDelay, request.StatsRateLimitDelayMs);

            SetSuccessStats(request);
            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.Regular,
                StatsEnableLog = false
            }, false);
            control.Observe(request);

            var getStats = FindRequest(
                control.LogClientStatsForTest(), "Get");
            Assert.AreEqual(expectedDelay,
                AsLong(getStats["rateLimitDelayMs"]));
        }

        [TestMethod]
        public void TestStatsRequestNameMappingForRequestTypes()
        {
            using var client = new NoSQLClient(TestConfig);
            var operations = new WriteOperationCollection().AddPut(MakeRow());

            AssertRequestName("Get", MakeGetRequest(client));
            AssertRequestName("Put", new PutRequest<RecordValue>(
                client, "Users", MakeRow(), null));
            AssertRequestName("Put", new PutIfAbsentRequest<RecordValue>(
                client, "Users", MakeRow(), null));
            AssertRequestName("Delete", new DeleteRequest<RecordValue>(
                client, "Users", MakeKey(), null));
            AssertRequestName("MultiDelete", new DeleteRangeRequest(
                client, "Users", MakeKey(), (DeleteRangeOptions)null));
            AssertRequestName("WriteMultiple",
                new WriteManyRequest<RecordValue>(client, "Users",
                    operations, null));
            AssertRequestName("Prepare", new PrepareRequest(
                client, "SELECT * FROM Users", null));
            AssertRequestName("Query", MakeQueryRequest(client));
            AssertRequestName("GetTable", new GetTableRequest(
                client, "Users", (GetTableOptions)null));
            AssertRequestName("ListTables", new ListTablesRequest(
                client, null));
            AssertRequestName("TableUsage", new GetTableUsageRequest(
                client, "Users", null));
            AssertRequestName("GetIndexes", new GetIndexesRequest(
                client, "Users", null));
            AssertRequestName("ReplicaStats", new GetReplicaStatsRequest(
                client, "Users", "region1", null));
            AssertRequestName("AddReplica", new AddReplicaRequest(
                client, "Users", "region1", null));
            AssertRequestName("DropReplica", new DropReplicaRequest(
                client, "Users", "region1", null));
            AssertRequestName("Table", new TableDDLRequest(client,
                "CREATE TABLE Users(id INTEGER, PRIMARY KEY(id))", null));
            AssertRequestName("System", new AdminRequest(client,
                "show".ToCharArray(), null));
            AssertRequestName("SystemStatus", new AdminStatusRequest(client,
                new AdminResult(client), null));
        }

        [TestMethod]
        public void TestStatsControlObserveErrorRecordsOnlyErrorMetrics()
        {
            using var client = new NoSQLClient(TestConfig);
            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.Regular,
                StatsEnableLog = false
            }, false);
            var request = MakeGetRequest(client);

            request.RecordStatsRetry(new ReadThrottlingException(),
                TimeSpan.FromMilliseconds(12));
            SetSuccessStats(request, 500, 600, 700, 2);

            control.ObserveError(request);

            var getStats = FindRequest(control.LogClientStatsForTest(), "Get");
            Assert.AreEqual(1, AsLong(getStats["httpRequestCount"]));
            Assert.AreEqual(1, AsLong(getStats["errors"]));
            Assert.AreEqual(1, AsLong(getStats["retry"]
                .AsMapValue["count"]));
            Assert.AreEqual(12, AsLong(getStats["retry"]
                .AsMapValue["delayMs"]));
            Assert.AreEqual(1, AsLong(getStats["retry"]
                .AsMapValue["throttleCount"]));
            Assert.IsFalse(getStats.ContainsKey("httpRequestLatencyMs"));
            Assert.IsFalse(getStats.ContainsKey("requestSize"));
            Assert.IsFalse(getStats.ContainsKey("resultSize"));
        }

        [TestMethod]
        public void TestStatsBucketMappingAndConnectionShape()
        {
            using var client = new NoSQLClient(TestConfig);
            var control = new StatsControlImpl(TestConfig, false)
                .SetProfile(StatsControl.Profile.More);
            var stats = new Stats((StatsControlImpl)control);

            var getRequest = MakeGetRequest(client);
            SetSuccessStats(getRequest, 50, 60, 10, 2);
            stats.Observe(getRequest, false);

            var tableRequest = new TableDDLRequest(client,
                "CREATE TABLE Users(id INTEGER, PRIMARY KEY(id))", null);
            SetSuccessStats(tableRequest, 120, 80, 20, 4);
            stats.Observe(tableRequest, false);

            var generated = GenerateStats(stats);

            Assert.IsNotNull(FindRequest(generated, "Get"));
            Assert.IsNotNull(FindRequest(generated, "Table"));

            var connections = generated["connections"].AsMapValue;
            Assert.AreEqual(2, AsLong(connections["min"]));
            Assert.AreEqual(4, AsLong(connections["max"]));
            Assert.AreEqual(3.0, connections["avg"].AsDouble);
            Assert.IsFalse(connections.ContainsKey("count"));
        }

        [TestMethod]
        public void TestHttpConnectionMetricsTracksOnlyActiveConnections()
        {
            using var metrics = new Http.HttpConnectionMetrics();
            var meter = metrics.MeterFactory.Create(
                new MeterOptions("System.Net.Http"));
            var counter = meter.CreateUpDownCounter<long>(
                "http.client.open_connections");
            var activeTag = new KeyValuePair<string, object>(
                "http.connection.state", "active");
            var idleTag = new KeyValuePair<string, object>(
                "http.connection.state", "idle");

            counter.Add(3, activeTag);
            counter.Add(5, idleTag);

            Assert.AreEqual(3, metrics.ActiveConnectionCount);

            counter.Add(-1, activeTag);

            Assert.AreEqual(2, metrics.ActiveConnectionCount);
        }

        [TestMethod]
        public void TestHttpConnectionMetricsPreservesMeasuredZero()
        {
            using var metrics = new Http.HttpConnectionMetrics();

            Assert.IsFalse(metrics.TryGetActiveConnectionCount(
                out var unavailableCount));
            Assert.AreEqual(0, unavailableCount);

            var meter = metrics.MeterFactory.Create(
                new MeterOptions("System.Net.Http"));
            var counter = meter.CreateUpDownCounter<long>(
                "http.client.open_connections");
            var activeTag = new KeyValuePair<string, object>(
                "http.connection.state", "active");

            Assert.IsTrue(metrics.TryGetActiveConnectionCount(
                out var initialCount));
            Assert.AreEqual(0, initialCount);

            counter.Add(1, activeTag);
            counter.Add(-1, activeTag);

            Assert.IsTrue(metrics.TryGetActiveConnectionCount(
                out var measuredCount));
            Assert.AreEqual(0, measuredCount);
        }

        [TestMethod]
        public async Task TestExecuteWithTimeoutCoversCompleteOperation()
        {
            var exception = await Assert.ThrowsExceptionAsync<TimeoutException>(
                async () => await HttpRequestUtils.ExecuteWithTimeoutAsync(
                    async token =>
                    {
                        await Task.Delay(TimeSpan.FromSeconds(5), token);
                        return true;
                    }, 50, CancellationToken.None));

            StringAssert.Contains(exception.Message,
                "HTTP Request timed out after 50 ms");
        }

        [TestMethod]
        public void TestHttpConnectionMetricsPreservesMeterIdentityAndCaches()
        {
            using var metrics = new Http.HttpConnectionMetrics();
            var tags = new[]
            {
                new KeyValuePair<string, object>("source", "test")
            };
            var first = metrics.MeterFactory.Create(new MeterOptions(
                "System.Net.Http")
            {
                Version = "test-version",
                Tags = tags
            });
            var second = metrics.MeterFactory.Create(new MeterOptions(
                "System.Net.Http")
            {
                Version = "test-version",
                Tags = new[]
                {
                    new KeyValuePair<string, object>("source", "test")
                }
            });

            Assert.AreEqual("System.Net.Http", first.Name);
            Assert.AreEqual("test-version", first.Version);
            Assert.AreSame(metrics.MeterFactory, first.Scope);
            Assert.AreSame(first, second);
        }

        [TestMethod]
        public void TestHttpConnectionMetricsIsolatesClients()
        {
            using var firstMetrics = new Http.HttpConnectionMetrics();
            using var secondMetrics = new Http.HttpConnectionMetrics();
            var firstMeter = firstMetrics.MeterFactory.Create(
                new MeterOptions("System.Net.Http"));
            var secondMeter = secondMetrics.MeterFactory.Create(
                new MeterOptions("System.Net.Http"));
            var activeTag = new KeyValuePair<string, object>(
                "http.connection.state", "active");

            Assert.AreEqual("System.Net.Http", firstMeter.Name);
            Assert.AreEqual("System.Net.Http", secondMeter.Name);
            Assert.AreNotSame(firstMeter, secondMeter);

            firstMeter.CreateUpDownCounter<long>(
                "http.client.open_connections").Add(2, activeTag);
            secondMeter.CreateUpDownCounter<long>(
                "http.client.open_connections").Add(5, activeTag);

            Assert.AreEqual(2, firstMetrics.ActiveConnectionCount);
            Assert.AreEqual(5, secondMetrics.ActiveConnectionCount);
        }

        [TestMethod]
        public void TestStatsClearRemovesRequestsQueriesAndConnections()
        {
            using var client = new NoSQLClient(TestConfig);
            var control = new StatsControlImpl(TestConfig, false)
                .SetProfile(StatsControl.Profile.All);
            var stats = new Stats((StatsControlImpl)control);

            var getRequest = MakeGetRequest(client);
            SetSuccessStats(getRequest, connections: 2);
            stats.Observe(getRequest, false);

            var queryRequest = MakeQueryRequest(client);
            SetSuccessStats(queryRequest, connections: 3);
            stats.ObserveQuery(queryRequest);
            stats.Observe(queryRequest, false);

            var firstSnapshot = GenerateStats(stats);
            Assert.IsTrue(firstSnapshot.ContainsKey("connections"));
            Assert.IsTrue(firstSnapshot.ContainsKey("queries"));
            Assert.AreEqual(2, firstSnapshot["requests"].AsArrayValue.Count);

            stats.ClearStats();
            var secondSnapshot = GenerateStats(stats);
            Assert.IsFalse(secondSnapshot.ContainsKey("connections"));
            Assert.IsFalse(secondSnapshot.ContainsKey("queries"));
            Assert.AreEqual(0, secondSnapshot["requests"].AsArrayValue.Count);
        }

        [TestMethod]
        public async Task TestStatsAtomicIntervalRolloverLosesNoRequests()
        {
            using var client = new NoSQLClient(TestConfig);
            var control = new StatsControlImpl(TestConfig, false)
                .SetProfile(StatsControl.Profile.More);
            var stats = new Stats((StatsControlImpl)control);
            const int requestCount = 5000;
            long snapshotCount = 0;

            var observing = Task.Run(() => Parallel.For(0, requestCount,
                index =>
                {
                    var request = MakeGetRequest(client);
                    SetSuccessStats(request, latency: index % 10 + 1);
                    stats.Observe(request, false);
                }));

            while (!observing.IsCompleted)
            {
                snapshotCount += GetRequestCount(
                    RotateStats(stats),
                    "Get");
                await Task.Yield();
            }

            await observing;
            snapshotCount += GetRequestCount(
                RotateStats(stats),
                "Get");

            Assert.AreEqual(requestCount, snapshotCount);
        }

        [TestMethod]
        public void TestStatsJavaLikeRootOutputOrder()
        {
            using var client = new NoSQLClient(TestConfig);
            var control = new StatsControlImpl(TestConfig, false)
                .SetProfile(StatsControl.Profile.All);
            var stats = new Stats((StatsControlImpl)control);

            var getRequest = MakeGetRequest(client);
            SetSuccessStats(getRequest, 52, 120, 1, 1);
            stats.Observe(getRequest, false);

            var queryRequest = MakeQueryRequest(client);
            SetSuccessStats(queryRequest, 100, 300, 2, 1);
            stats.ObserveQuery(queryRequest);
            stats.Observe(queryRequest, false);

            var generated = GenerateStats(stats);

            AssertKeys(generated, "startTime", "endTime", "clientId",
                "connections", "queries", "requests");

            var retry = FindRequest(generated, "Get")["retry"].AsMapValue;
            AssertKeys(retry, "count", "delayMs", "authCount",
                "throttleCount");
        }

        [TestMethod]
        public void TestPreparedQueryStatsFields()
        {
            using var client = new NoSQLClient(TestConfig);
            var control = new StatsControlImpl(TestConfig, false)
                .SetProfile(StatsControl.Profile.All);
            var stats = new Stats((StatsControlImpl)control);
            var preparedStatement = new PreparedStatement
            {
                SQLText = "SELECT * FROM Users",
                QueryPlan = "server plan",
                OperationCode = QueryRequest.OperationCodeSelect
            };
            var queryRequest = new QueryRequest<RecordValue>(
                client, preparedStatement, null);

            stats.ObserveQuery(queryRequest);
            SetSuccessStats(queryRequest, 100, 300, 10, 1);
            stats.Observe(queryRequest, false);

            var query = GenerateStats(stats)
                ["queries"].AsArrayValue[0].AsMapValue;

            Assert.AreEqual("SELECT * FROM Users",
                query["query"].AsString);
            Assert.AreEqual(1, AsLong(query["count"]));
            Assert.AreEqual(0, AsLong(query["unprepared"]));
            Assert.IsTrue(query["simple"].AsBoolean);
            Assert.IsFalse(query["doesWrites"].AsBoolean);
            Assert.IsFalse(query.ContainsKey("plan"));
            Assert.AreEqual(1, AsLong(query["httpRequestCount"]));
        }

        [TestMethod]
        public void TestPreparedQueryStatsFormatsExistingDriverPlan()
        {
            using var client = new NoSQLClient(TestConfig);
            var control = new StatsControlImpl(TestConfig, false)
                .SetProfile(StatsControl.Profile.All);
            var stats = new Stats((StatsControlImpl)control);
            var preparedStatement = new PreparedStatement
            {
                SQLText = "SELECT * FROM Users",
                QueryPlan = "server plan",
                OperationCode = QueryRequest.OperationCodeSelect,
                DriverQueryPlan = new Query.ReceiveStep
                {
                    ResultPosition = 2,
                    DistributionKind = Query.DistributionKind.AllPartitions,
                    PrimaryKeyFields = new[] { "id" }
                }
            };
            var queryRequest = new QueryRequest<RecordValue>(
                client, preparedStatement, null);

            stats.ObserveQuery(queryRequest);
            SetSuccessStats(queryRequest);
            stats.Observe(queryRequest, false);

            var plan = GenerateStats(stats)
                ["queries"].AsArrayValue[0].AsMapValue["plan"].AsString;

            Assert.AreEqual(
                "RECV([2])\n[\n" +
                "  DistributionKind : ALL_PARTITIONS,\n" +
                "  Primary Key Fields : id,\n\n" +
                "]", plan);
            Assert.AreNotEqual("server plan", plan);
        }

        [TestMethod]
        public void TestQueryPlanFormattingFailureDoesNotDropRequestStats()
        {
            using var client = new NoSQLClient(TestConfig);
            var stats = new Stats("client", StatsControl.Profile.All);
            var preparedStatement = new PreparedStatement
            {
                SQLText = "SELECT * FROM Users",
                OperationCode = QueryRequest.OperationCodeSelect,
                // A missing constant value makes this synthetic plan invalid
                // for formatting while leaving the request itself observable.
                DriverQueryPlan = new Query.ConstStep
                {
                    ResultPosition = 1
                }
            };
            var queryRequest = new QueryRequest<RecordValue>(
                client, preparedStatement, null);

            stats.ObserveQuery(queryRequest);
            SetSuccessStats(queryRequest);
            stats.Observe(queryRequest, false);

            var snapshot = GenerateStats(stats);
            var requestStats = FindRequest(snapshot, "Query");
            var queryStats = snapshot["queries"].AsArrayValue[0].AsMapValue;

            Assert.AreEqual(1,
                AsLong(requestStats["httpRequestCount"]));
            Assert.AreEqual(1,
                AsLong(queryStats["httpRequestCount"]));
            Assert.IsFalse(queryStats.ContainsKey("plan"));
        }

        [TestMethod]
        public void TestPlanFormatterDistinguishesSFWGrouping()
        {
            static string FormatSFW(int groupColumnCount) =>
                Query.PlanFormatter.Format(new Query.SFWStep
                {
                    GroupColumnCount = groupColumnCount,
                    FromVarName = "$t",
                    FromStep = new Query.VarRefStep { VarName = "$t" },
                    ColumnSteps = new Query.PlanStep[]
                    {
                        new Query.VarRefStep { VarName = "$column" }
                    }
                });

            Assert.IsFalse(FormatSFW(-1).Contains("GROUP BY:"));
            StringAssert.Contains(FormatSFW(0),
                "GROUP BY:\n  No grouping expressions");
            StringAssert.Contains(FormatSFW(1),
                "Grouping by the first expression in the SELECT list");
            StringAssert.Contains(FormatSFW(2),
                "Grouping by the first 2 expressions in the SELECT list");
        }

        [TestMethod]
        public void TestPlanFormatterUsesGroupColumnNames()
        {
            var plan = Query.PlanFormatter.Format(new Query.GroupStep
            {
                GroupingColumnCount = 2,
                ColumnNames = new[]
                {
                    "department", "location", "employeeCount"
                },
                AggregateFuncCodes = new[] { Query.SQLFuncCode.CountStar },
                InputStep = new Query.VarRefStep { VarName = "$input" }
            });

            const string grouping =
                "Grouping Columns : department, location";
            const string aggregate = "Aggregate Functions : FN_COUNT_STAR";
            var groupingIndex = plan.IndexOf(grouping,
                StringComparison.Ordinal);
            var aggregateIndex = plan.IndexOf(aggregate,
                StringComparison.Ordinal);
            var inputIndex = plan.IndexOf("VAR_REF",
                StringComparison.Ordinal);

            Assert.IsTrue(groupingIndex >= 0);
            Assert.IsTrue(aggregateIndex > groupingIndex);
            Assert.IsTrue(inputIndex > aggregateIndex);
            Assert.IsFalse(plan.Contains("Grouping Columns : 2"));
            Assert.IsFalse(plan.Contains("Column Names :"));
        }

        [TestMethod]
        public void TestPlanFormatterSeparatesSortMetadataFromInput()
        {
            var plan = Query.PlanFormatter.Format(new Query.SortStep
            {
                InputStep = new Query.VarRefStep { VarName = "$input" },
                SortSpecs = new[]
                {
                    new Query.SortSpec("name", false, false)
                }
            });

            Assert.AreEqual(
                "SORT([0])\n[\n" +
                "  VAR_REF($input)([0])\n" +
                "  Sort Fields : name,\n\n" +
                "]", plan);
        }

        [TestMethod]
        public void TestPlanFormatterUsesJavaExpressionLayout()
        {
            var arithmetic = Query.PlanFormatter.Format(
                new Query.ArithmeticOpStep
                {
                    ResultPosition = 3,
                    Opcode = Query.ArithmeticOpcode.AddSubtract,
                    OpSequence = "+-",
                    ArgSteps = new Query.PlanStep[]
                    {
                        new Query.ConstStep
                        {
                            ResultPosition = 1,
                            Value = new IntegerValue(2)
                        },
                        new Query.VarRefStep
                        {
                            ResultPosition = 2,
                            VarName = "$value"
                        }
                    }
                });

            Assert.AreEqual(
                "OP_ADD_SUB([3])\n[\n" +
                "  +,\n" +
                "  CONST([1])\n" +
                "  [\n" +
                "    2\n" +
                "  ],\n" +
                "  -,\n" +
                "  VAR_REF($value)([2])\n" +
                "]", arithmetic);

            var field = Query.PlanFormatter.Format(new Query.FieldStep
            {
                ResultPosition = 4,
                InputStep = new Query.VarRefStep
                {
                    ResultPosition = 1,
                    VarName = "$row"
                },
                FieldName = "name"
            });

            Assert.AreEqual(
                "FIELD_STEP([4])\n[\n" +
                "  VAR_REF($row)([1]),\n" +
                "  name\n" +
                "]", field);
        }

        [TestMethod]
        public void TestPlanFormatterUsesJavaFunctionAndExternalVarLayout()
        {
            var collect = Query.PlanFormatter.Format(
                new Query.FuncCollectStep
                {
                    ResultPosition = 5,
                    IsDistinct = true,
                    InputStep = new Query.ExtVarRefStep
                    {
                        ResultPosition = 2,
                        VarName = "$external",
                        VarPosition = 7
                    }
                });

            Assert.AreEqual(
                "FN_COLLECT([5])\n[\n" +
                "  \"distinct\" : true,\n" +
                "  EXTENAL_VAR_REF($external, 7)([2])\n" +
                "]", collect);
        }

        [TestMethod]
        public void TestDeferredLogicalQueryIsObservedOnceAfterPreflight()
        {
            using var client = new NoSQLClient(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.All,
                StatsEnableLog = false
            });
            var logicalRequest = new QueryRequest<RecordValue>(client,
                "SELECT * FROM Users",
                new QueryOptions { LastWriteMetadata = "{}" });
            logicalRequest.DeferStatsLogicalQuery();

            // An advanced query reaches the HTTP layer through an internal
            // request, which must admit the outer logical query only once.
            var internalRequest = new QueryRequest<RecordValue>(client,
                "SELECT * FROM Users", null)
            {
                IsInternal = true,
                StatsLogicalQueryRequest = logicalRequest
            };
            internalRequest.Init();

            client.ObserveDeferredQueryStats(internalRequest);
            client.ObserveDeferredQueryStats(internalRequest);

            var snapshot = ((StatsControlImpl)client.GetStatsControl())
                .LogClientStatsForTest();
            var queries = snapshot["queries"].AsArrayValue;
            Assert.AreEqual(1, queries.Count);
            Assert.AreEqual(1,
                AsLong(queries[0].AsMapValue["count"]));
            Assert.AreEqual(0, snapshot["requests"].AsArrayValue.Count);
        }

        [TestMethod]
        public void TestValidatedMetadataContinuationIsObservedImmediately()
        {
            using var client = new NoSQLClient(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.All,
                StatsEnableLog = false
            });
            var firstRequest = new QueryRequest<RecordValue>(client,
                "SELECT * FROM Users",
                new QueryOptions { LastWriteMetadata = "{}" });
            firstRequest.DeferStatsLogicalQuery();
            client.ObserveDeferredQueryStats(firstRequest);

            var continuationResult = new QueryResult<RecordValue>
            {
                ContinuationKey = new QueryContinuationKey()
            };
            firstRequest.ApplyResult(continuationResult);
            var continuationRequest = new QueryRequest<RecordValue>(client,
                "SELECT * FROM Users", new QueryOptions
                {
                    ContinuationKey = continuationResult.ContinuationKey,
                    LastWriteMetadata = "{}"
                });

            // This represents a continuation satisfied from buffered rows: no
            // HTTP path is available to consume another deferred observation.
            client.ObserveOrDeferLogicalQuery(continuationRequest);

            var snapshot = ((StatsControlImpl)client.GetStatsControl())
                .LogClientStatsForTest();
            var queries = snapshot["queries"].AsArrayValue;
            Assert.AreEqual(1, queries.Count);
            Assert.AreEqual(2,
                AsLong(queries[0].AsMapValue["count"]));
            Assert.IsFalse(continuationRequest.TryConsumeStatsLogicalQuery());
        }

        [TestMethod]
        public void TestPreparedDmlQueryStatsShowsDoesWrites()
        {
            using var client = new NoSQLClient(TestConfig);
            var control = new StatsControlImpl(TestConfig, false)
                .SetProfile(StatsControl.Profile.All);
            var stats = new Stats((StatsControlImpl)control);
            var preparedStatement = new PreparedStatement
            {
                SQLText = "INSERT INTO Users VALUES(1, \"user-1\")",
                OperationCode = QueryRequest.OperationCodeSelect + 1
            };
            var queryRequest = new QueryRequest<RecordValue>(
                client, preparedStatement, null);

            stats.ObserveQuery(queryRequest);
            SetSuccessStats(queryRequest);
            stats.Observe(queryRequest, false);

            var query = GenerateStats(stats)
                ["queries"].AsArrayValue[0].AsMapValue;

            Assert.AreEqual("INSERT INTO Users VALUES(1, \"user-1\")",
                query["query"].AsString);
            Assert.AreEqual(0, AsLong(query["unprepared"]));
            Assert.IsTrue(query["simple"].AsBoolean);
            Assert.IsTrue(query["doesWrites"].AsBoolean);
            Assert.AreEqual(1, AsLong(query["httpRequestCount"]));
        }

        [TestMethod]
        public void TestStatsRepeatedQueryTextUsesSingleQueryBucket()
        {
            using var client = new NoSQLClient(TestConfig);
            var control = new StatsControlImpl(TestConfig, false)
                .SetProfile(StatsControl.Profile.All);
            var stats = new Stats((StatsControlImpl)control);
            var queryRequest = MakeQueryRequest(client);

            stats.ObserveQuery(queryRequest);
            SetSuccessStats(queryRequest, 100, 300, 10, 1);
            stats.Observe(queryRequest, false);

            // Model a continuation call after the first server response has
            // supplied the prepared statement.
            queryRequest.PreparedStatement = new PreparedStatement
            {
                SQLText = queryRequest.Statement,
                OperationCode = QueryRequest.OperationCodeSelect
            };

            stats.ObserveQuery(queryRequest);
            SetSuccessStats(queryRequest, 120, 360, 20, 1);
            stats.Observe(queryRequest, false);

            stats.ObserveQuery(queryRequest);
            SetSuccessStats(queryRequest, 140, 420, 30, 1);
            stats.Observe(queryRequest, false);

            var queries = GenerateStats(stats)
                ["queries"].AsArrayValue;
            Assert.AreEqual(1, queries.Count);

            var query = queries[0].AsMapValue;
            Assert.AreEqual("SELECT * FROM Users", query["query"].AsString);
            Assert.AreEqual(3, AsLong(query["count"]));
            Assert.AreEqual(1, AsLong(query["unprepared"]));
            Assert.IsTrue(query["simple"].AsBoolean);
            Assert.AreEqual(3, AsLong(query["httpRequestCount"]));
            AssertMinAvgMax(query["requestSize"].AsMapValue, 100, 120, 140);
            AssertMinAvgMax(query["resultSize"].AsMapValue, 300, 360, 420);
            AssertMinAvgMax(query["httpRequestLatencyMs"].AsMapValue,
                10, 20, 30);
        }

        [TestMethod]
        public void TestQueryErrorsDoNotAffectLatencyOrSizes()
        {
            using var client = new NoSQLClient(TestConfig);
            var control = new StatsControlImpl(TestConfig, false)
                .SetProfile(StatsControl.Profile.All);
            var stats = new Stats((StatsControlImpl)control);
            var queryRequest = MakeQueryRequest(client);

            stats.ObserveQuery(queryRequest);
            SetSuccessStats(queryRequest, 100, 300, 10, 1);
            stats.Observe(queryRequest, false);

            stats.ObserveQuery(queryRequest);
            SetSuccessStats(queryRequest, 500, 600, 700, 1);
            stats.Observe(queryRequest, true);

            var generated = GenerateStats(stats);
            var requestStats = FindRequest(generated, "Query");
            Assert.AreEqual(2, AsLong(requestStats["httpRequestCount"]));
            Assert.AreEqual(1, AsLong(requestStats["errors"]));
            AssertMinAvgMax(requestStats["requestSize"].AsMapValue,
                100, 100, 100);
            AssertMinAvgMax(requestStats["resultSize"].AsMapValue,
                300, 300, 300);
            AssertMinAvgMax(requestStats["httpRequestLatencyMs"].AsMapValue,
                10, 10, 10);

            var queryStats = generated["queries"].AsArrayValue[0].AsMapValue;
            Assert.AreEqual(2, AsLong(queryStats["httpRequestCount"]));
            Assert.AreEqual(1, AsLong(queryStats["errors"]));
            AssertMinAvgMax(queryStats["requestSize"].AsMapValue,
                100, 100, 100);
            AssertMinAvgMax(queryStats["resultSize"].AsMapValue,
                300, 300, 300);
            var latency = queryStats["httpRequestLatencyMs"].AsMapValue;
            AssertMinAvgMax(latency, 10, 10, 10);
            Assert.AreEqual(10, AsLong(latency["95th"]));
            Assert.AreEqual(10, AsLong(latency["99th"]));
        }

        [TestMethod]
        public void TestStatsControlProfileTransitionsKeepExistingStatsAndEnableQueryStats()
        {
            using var client = new NoSQLClient(TestConfig);
            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.Regular,
                StatsEnableLog = false
            }, false);

            var firstGet = MakeGetRequest(client);
            SetSuccessStats(firstGet, latency: 10);
            control.Observe(firstGet);

            control.SetProfile(StatsControl.Profile.None);
            var secondGet = MakeGetRequest(client);
            SetSuccessStats(secondGet, latency: 20);
            control.Observe(secondGet);

            control.SetProfile(StatsControl.Profile.All);
            var queryRequest = MakeQueryRequest(client);
            SetSuccessStats(queryRequest, latency: 30);
            control.ObserveQuery(queryRequest);
            control.Observe(queryRequest);

            var snapshot = control.LogClientStatsForTest();
            Assert.AreEqual(2, AsLong(FindRequest(snapshot, "Get")
                ["httpRequestCount"]));
            Assert.AreEqual(1, AsLong(FindRequest(snapshot, "Query")
                ["httpRequestCount"]));
            Assert.AreEqual(1, snapshot["queries"].AsArrayValue.Count);

            var queryLatency = snapshot["queries"].AsArrayValue[0]
                .AsMapValue["httpRequestLatencyMs"].AsMapValue;
            Assert.AreEqual(30, AsLong(queryLatency["95th"]));
            Assert.AreEqual(30, AsLong(queryLatency["99th"]));

            // Java keeps top-level request buckets at the capabilities they
            // had when the collector was created.
            var requestLatency = FindRequest(snapshot, "Query")
                ["httpRequestLatencyMs"].AsMapValue;
            Assert.IsFalse(requestLatency.ContainsKey("95th"));
            Assert.IsFalse(requestLatency.ContainsKey("99th"));
        }

        [TestMethod]
        public void TestStatsObserveIsThreadSafe()
        {
            using var client = new NoSQLClient(TestConfig);
            var control = new StatsControlImpl(TestConfig, false)
                .SetProfile(StatsControl.Profile.More);
            var stats = new Stats((StatsControlImpl)control);

            Parallel.For(0, 1000, idx =>
            {
                var request = MakeGetRequest(client);
                SetSuccessStats(request, latency: idx % 10 + 1);
                stats.Observe(request, false);
            });

            var getStats = FindRequest(
                GenerateStats(stats), "Get");

            Assert.AreEqual(1000, AsLong(getStats["httpRequestCount"]));
            Assert.AreEqual(0, AsLong(getStats["errors"]));
            Assert.AreEqual(1, AsLong(getStats["httpRequestLatencyMs"]
                .AsMapValue["min"]));
            Assert.AreEqual(10, AsLong(getStats["httpRequestLatencyMs"]
                .AsMapValue["max"]));
        }

        [TestMethod]
        public void TestStatsObservationCapturesRequestState()
        {
            using var client = new NoSQLClient(TestConfig);
            var request = MakeGetRequest(client);
            SetSuccessStats(request, 50, 60, 10, 2);

            var observation = StatsObservation.FromRequest(request,
                false, false);

            // Request instances remain mutable during execution. Once the
            // terminal observation is created, later mutations must not alter
            // what the collector records.
            SetSuccessStats(request, 500, 600, 100, 20);

            var collector = new Stats("client", StatsControl.Profile.More);
            collector.Record(observation);
            var getStats = FindRequest(
                GenerateStats(collector), "Get");

            AssertMinAvgMax(getStats["requestSize"].AsMapValue,
                50, 50, 50);
            AssertMinAvgMax(getStats["resultSize"].AsMapValue,
                60, 60, 60);
            AssertMinAvgMax(getStats["httpRequestLatencyMs"].AsMapValue,
                10, 10, 10);
            AssertMinAvgMax(
                GenerateStats(collector)
                    ["connections"].AsMapValue,
                2, 2, 2);
        }

        [TestMethod]
        public void TestStatsRotationIsolatesCompletedInterval()
        {
            using var client = new NoSQLClient(TestConfig);
            var collector = new Stats("client",
                StatsControl.Profile.Regular);
            var firstRequest = MakeGetRequest(client);
            SetSuccessStats(firstRequest);
            collector.Observe(firstRequest, false);

            var completed = collector.Rotate(DateTime.UtcNow);
            var firstView = ToMapValue(completed);
            var secondView = ToMapValue(completed);

            // Every consumer receives an independent mutable compatibility
            // view of the immutable completed interval.
            firstView.Clear();
            Assert.AreEqual(1, GetRequestCount(secondView, "Get"));

            var activeInterval = GenerateStats(collector);
            Assert.AreEqual(0, GetRequestCount(activeInterval, "Get"));

            var secondRequest = MakeGetRequest(client);
            SetSuccessStats(secondRequest);
            collector.Observe(secondRequest, false);
            Assert.AreEqual(1, GetRequestCount(
                GenerateStats(collector), "Get"));
            Assert.AreEqual(1, GetRequestCount(secondView, "Get"));
        }

        [TestMethod]
        public void TestStatsRotationDoesNotOverwriteProfile()
        {
            var collector = new Stats("client",
                StatsControl.Profile.Regular);

            collector.UpdateProfile(StatsControl.Profile.All);
            collector.Rotate(DateTime.UtcNow);

            // Rotation owns interval state only. SetProfile/UpdateProfile is
            // the sole owner of the collector's current profile.
            Assert.IsTrue(collector.IncludesQueryDetails);
        }

        [TestMethod]
        public void TestRequestStatsRetryClassification()
        {
            using var client = new NoSQLClient(TestConfig);
            var request = MakeGetRequest(client);

            request.RecordStatsRetry(new InvalidAuthorizationException(),
                TimeSpan.FromMilliseconds(7.9));
            request.RecordStatsRetry(new SecurityInfoNotReadyException(),
                TimeSpan.FromMilliseconds(8.1));
            request.RecordStatsRetry(new ReadThrottlingException(),
                TimeSpan.FromMilliseconds(9.9));
            request.RecordStatsRetry(new WriteThrottlingException(),
                TimeSpan.FromMilliseconds(10.1));
            request.RecordStatsRetry(new ControlOperationThrottlingException(),
                TimeSpan.FromMilliseconds(11.9));

            Assert.AreEqual(5, request.StatsRetryCount);
            Assert.AreEqual(45, request.StatsRetryDelayMs);
            Assert.AreEqual(2, request.StatsRetryAuthCount);
            Assert.AreEqual(3, request.StatsRetryThrottleCount);
        }

        [TestMethod]
        public void TestStatsControlStopAndRestartResumesCollection()
        {
            using var client = new NoSQLClient(TestConfig);
            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.Regular,
                StatsEnableLog = false
            }, false);

            var firstRequest = MakeGetRequest(client);
            SetSuccessStats(firstRequest);
            control.Observe(firstRequest);

            control.Stop();
            var stoppedRequest = MakeGetRequest(client);
            SetSuccessStats(stoppedRequest);
            control.Observe(stoppedRequest);

            control.Start();
            var restartedRequest = MakeGetRequest(client);
            SetSuccessStats(restartedRequest);
            control.Observe(restartedRequest);

            var getStats = FindRequest(control.LogClientStatsForTest(), "Get");
            Assert.AreEqual(2, AsLong(getStats["httpRequestCount"]));
        }

        [TestMethod]
        public void TestStatsControlConstructorStartsWhenProfileEnabled()
        {
            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.More,
                StatsEnableLog = false
            }, false);

            Assert.IsTrue(control.IsStarted());
            Assert.AreEqual(StatsControl.Profile.More,
                control.GetProfile());

            var snapshot = control.GenerateStats();
            Assert.IsNotNull(snapshot);
            Assert.AreEqual(0, snapshot["requests"].AsArrayValue.Count);
        }

        [TestMethod]
        public void TestStatsControlStartupLogMetadata()
        {
            var logger = new TestLogger();

            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.All,
                StatsInterval = TimeSpan.FromHours(1),
                StatsPrettyPrint = true,
                StatsEnableLog = true,
                StatsLogger = logger
            }, true);

            var output = logger.SingleMessage;
            Assert.IsTrue(output.StartsWith("Client stats|"));
            Assert.IsTrue(output.Contains(
                "\"sdkName\":\"Oracle NoSQL SDK for .NET\""));
            Assert.IsTrue(output.Contains("\"profile\":\"ALL\""));
            Assert.IsTrue(output.Contains("\"intervalSec\":3600"));
            Assert.IsTrue(output.Contains("\"prettyPrint\":true"));
            Assert.IsTrue(output.Contains(
                "\"rateLimitingEnabled\":true"));
        }

        [TestMethod]
        public void TestStatsControlLogsStartupWhenEnabledAtRuntime()
        {
            var logger = new TestLogger();

            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.None,
                StatsInterval = TimeSpan.FromHours(1),
                StatsEnableLog = true,
                StatsLogger = logger
            }, false);

            Assert.AreEqual(0, logger.Messages.Count);

            control.SetProfile(StatsControl.Profile.Regular);
            control.Start();

            var output = logger.SingleMessage;
            Assert.IsTrue(output.StartsWith(StatsControl.LogPrefix));
            Assert.IsTrue(output.Contains("\"profile\":\"REGULAR\""));

            // Repeated starts must not emit duplicate startup metadata.
            control.Start();
            Assert.AreEqual(1, logger.Messages.Count);
        }

        [TestMethod]
        public void TestThrowingStartupLoggerDoesNotPreventConstruction()
        {
            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.Regular,
                StatsEnableLog = true,
                StatsLogger = new ThrowingLogger()
            }, false);

            Assert.IsTrue(control.IsStarted());
            Assert.IsNotNull(control.GenerateStats());
        }

        [TestMethod]
        public void TestStatsEnableLogFalseStillInvokesHandler()
        {
            using var client = new NoSQLClient(TestConfig);
            MapValue handledStats = null;
            var handlerCount = 0;
            var logger = new TestLogger();

            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.Regular,
                StatsEnableLog = false,
                StatsLogger = logger,
                StatsHandler = stats =>
                {
                    handledStats = stats;
                    handlerCount++;
                }
            }, false);

            var request = MakeGetRequest(client);
            SetSuccessStats(request);
            control.Observe(request);

            var snapshot = control.LogClientStatsForTest();

            Assert.AreEqual(1, handlerCount);
            // The immutable internal snapshot is converted only once; the
            // logger and handler consume the same Java-compatible view.
            Assert.AreSame(snapshot, handledStats);
            Assert.AreEqual(snapshot.ToJsonString(),
                handledStats.ToJsonString());
            Assert.AreEqual(0, logger.Messages.Count);
        }

        [TestMethod]
        public void TestStatsHandlerFailureDoesNotSuppressLogging()
        {
            using var client = new NoSQLClient(TestConfig);
            var logger = new TestLogger();

            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.Regular,
                StatsEnableLog = true,
                StatsLogger = logger,
                StatsHandler = _ => throw new InvalidOperationException(
                    "handler failure")
            }, false);

            var request = MakeGetRequest(client);
            SetSuccessStats(request);
            control.Observe(request);

            var snapshot = control.LogClientStatsForTest();

            Assert.IsNotNull(snapshot);
            Assert.IsTrue(logger.Messages.Any(output =>
                output.Contains(StatsControl.LogPrefix) &&
                output.Contains("\"name\":\"Get\"")));
            Assert.IsTrue(logger.Messages.Any(output =>
                output.Contains("Stats exception") &&
                output.Contains("handler failure")));
        }

        [TestMethod]
        public void TestStatsHandlerMutationDoesNotChangeLogOutput()
        {
            using var client = new NoSQLClient(TestConfig);
            var logger = new TestLogger();

            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.Regular,
                StatsEnableLog = true,
                StatsLogger = logger,
                StatsHandler = snapshot => snapshot.Clear()
            }, false);

            var request = MakeGetRequest(client);
            SetSuccessStats(request);
            control.Observe(request);

            control.LogClientStatsForTest();
            var output = logger.Messages.Last();

            Assert.IsTrue(output.Contains("\"name\":\"Get\""));
            Assert.IsTrue(output.Contains("\"httpRequestCount\":1"));
        }

        [TestMethod]
        public async Task TestStatsControlSerializesSnapshotsLikeJava()
        {
            using var handlerEntered = new ManualResetEventSlim(false);
            using var releaseHandler = new ManualResetEventSlim(false);
            var handlerCount = 0;
            var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.Regular,
                StatsInterval = TimeSpan.FromHours(1),
                StatsEnableLog = false,
                StatsHandler = _ =>
                {
                    Interlocked.Increment(ref handlerCount);
                    handlerEntered.Set();
                    releaseHandler.Wait(TimeSpan.FromSeconds(5));
                }
            }, false);

            try
            {
                var firstSnapshotTask = Task.Run(
                    () => control.LogClientStatsForTest());
                Assert.IsTrue(handlerEntered.Wait(TimeSpan.FromSeconds(5)));

                var secondSnapshotTask = Task.Run(
                    () => control.LogClientStatsForTest());
                await Task.Delay(100);

                Assert.IsFalse(secondSnapshotTask.IsCompleted);
                Assert.AreEqual(1, Volatile.Read(ref handlerCount));

                releaseHandler.Set();
                var firstSnapshot = await firstSnapshotTask;
                var secondSnapshot = await secondSnapshotTask;

                Assert.IsNotNull(firstSnapshot);
                Assert.IsNotNull(secondSnapshot);
                Assert.AreEqual(2, Volatile.Read(ref handlerCount));
            }
            finally
            {
                releaseHandler.Set();
                control.SetStatsHandler(null);
                control.Shutdown();
            }
        }

        [TestMethod]
        public async Task TestStatsControlShutdownFlushesFinalPartialInterval()
        {
            using var client = new NoSQLClient(TestConfig);
            using var handlerEntered = new ManualResetEventSlim(false);
            using var releaseHandler = new ManualResetEventSlim(false);
            var snapshots = new List<MapValue>();
            var handlerCount = 0;
            var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.Regular,
                StatsInterval = TimeSpan.FromHours(1),
                StatsEnableLog = false,
                StatsHandler = snapshot =>
                {
                    lock (snapshots)
                    {
                        snapshots.Add(snapshot);
                    }

                    if (Interlocked.Increment(ref handlerCount) == 1)
                    {
                        handlerEntered.Set();
                        releaseHandler.Wait(TimeSpan.FromSeconds(5));
                    }
                }
            }, false);

            try
            {
                var firstRequest = MakeGetRequest(client);
                SetSuccessStats(firstRequest);
                control.Observe(firstRequest);

                var reportTask = Task.Run(
                    () => control.LogClientStatsForTest());
                Assert.IsTrue(handlerEntered.Wait(TimeSpan.FromSeconds(5)));

                // The first report already swapped intervals. This request
                // must therefore be emitted by the final shutdown snapshot.
                var finalRequest = MakeGetRequest(client);
                SetSuccessStats(finalRequest);
                control.Observe(finalRequest);

                var shutdownTask = Task.Run(control.Shutdown);
                await Task.Delay(100);
                Assert.IsFalse(shutdownTask.IsCompleted);

                releaseHandler.Set();
                await Task.WhenAll(reportTask, shutdownTask)
                    .WaitAsync(TimeSpan.FromSeconds(5));

                Assert.AreEqual(2, Volatile.Read(ref handlerCount));
                lock (snapshots)
                {
                    Assert.AreEqual(2, snapshots.Count);
                    Assert.AreEqual(1, AsLong(FindRequest(
                        snapshots[0], "Get")["httpRequestCount"]));
                    Assert.AreEqual(1, AsLong(FindRequest(
                        snapshots[1], "Get")["httpRequestCount"]));
                }
            }
            finally
            {
                releaseHandler.Set();
                control.SetStatsHandler(null);
                control.Shutdown();
            }
        }

        [TestMethod]
        public async Task TestShutdownFromExpiredInheritedReportScopeFlushes()
        {
            using var client = new NoSQLClient(TestConfig);
            using var releaseShutdown = new ManualResetEventSlim(false);
            var snapshots = new List<MapValue>();
            Task shutdownTask = null;
            StatsControlImpl control = null;
            var handlerCount = 0;

            control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.Regular,
                StatsInterval = TimeSpan.FromHours(1),
                StatsEnableLog = false,
                StatsHandler = snapshot =>
                {
                    lock (snapshots)
                    {
                        snapshots.Add(snapshot);
                    }

                    if (Interlocked.Increment(ref handlerCount) == 1)
                    {
                        // Task.Run inherits the current ExecutionContext. It
                        // must not retain an active reporting marker after
                        // this handler and report have completed.
                        shutdownTask = Task.Run(() =>
                        {
                            releaseShutdown.Wait();
                            control.Shutdown();
                        });
                    }
                }
            }, false);

            try
            {
                var firstRequest = MakeGetRequest(client);
                SetSuccessStats(firstRequest);
                control.Observe(firstRequest);
                control.LogClientStatsForTest();

                var finalRequest = MakeGetRequest(client);
                SetSuccessStats(finalRequest);
                control.Observe(finalRequest);

                releaseShutdown.Set();
                await shutdownTask.WaitAsync(TimeSpan.FromSeconds(5));

                Assert.AreEqual(2, Volatile.Read(ref handlerCount));
                lock (snapshots)
                {
                    Assert.AreEqual(2, snapshots.Count);
                    Assert.AreEqual(1, AsLong(FindRequest(
                        snapshots[1], "Get")["httpRequestCount"]));
                }
            }
            finally
            {
                releaseShutdown.Set();
                control.SetStatsHandler(null);
                control.Shutdown();
            }
        }

        [TestMethod]
        public void TestStatsControlShutdownIsIdempotent()
        {
            using var client = new NoSQLClient(TestConfig);
            var handlerCount = 0;
            var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.Regular,
                StatsEnableLog = false,
                StatsHandler = _ => handlerCount++
            }, false);

            var request = MakeGetRequest(client);
            SetSuccessStats(request);
            control.Observe(request);

            control.Shutdown();
            control.Shutdown();

            Assert.AreEqual(1, handlerCount);
            Assert.IsFalse(control.IsStarted());

            control.Start();
            Assert.IsFalse(control.IsStarted());

            var ignoredRequest = MakeGetRequest(client);
            SetSuccessStats(ignoredRequest);
            control.Observe(ignoredRequest);

            Assert.AreEqual(0, control.GenerateStats()
                ["requests"].AsArrayValue.Count);
        }

        [TestMethod]
        public void TestStatsControlDefaultsAndPublicClientAccessor()
        {
            using var client = new NoSQLClient(TestConfig);
            var statsControl = client.GetStatsControl();

            Assert.IsNotNull(statsControl);
            Assert.AreEqual(TimeSpan.FromSeconds(600),
                statsControl.GetInterval());
            Assert.AreEqual(StatsControl.Profile.None,
                statsControl.GetProfile());
            Assert.IsFalse(statsControl.GetPrettyPrint());
            Assert.IsNull(statsControl.GetStatsHandler());
            Assert.IsFalse(statsControl.IsStarted());
        }

        [TestMethod]
        public void TestStatsControlSettersReturnSameInstance()
        {
            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsEnableLog = false
            }, false);
            StatsControl.StatsHandler handler = _ => { };

            Assert.AreSame(control, control.SetProfile(
                StatsControl.Profile.More));
            Assert.AreSame(control, control.SetPrettyPrint(true));
            Assert.AreSame(control, control.SetStatsHandler(handler));
            Assert.AreEqual(StatsControl.Profile.More, control.GetProfile());
            Assert.IsTrue(control.GetPrettyPrint());
            Assert.AreSame(handler, control.GetStatsHandler());
        }

        [TestMethod]
        public void TestStatsControlProfileNoneDoesNotStopExistingStats()
        {
            using var client = new NoSQLClient(TestConfig);
            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.Regular,
                StatsEnableLog = false
            }, false);

            var getRequest = MakeGetRequest(client);
            SetSuccessStats(getRequest);
            control.Observe(getRequest);

            control.SetProfile(StatsControl.Profile.None);

            var secondRequest = MakeGetRequest(client);
            SetSuccessStats(secondRequest);
            control.Observe(secondRequest);

            var snapshot = control.LogClientStatsForTest();
            var getStats = FindRequest(snapshot, "Get");

            Assert.IsNotNull(getStats);
            Assert.AreEqual(2, AsLong(getStats["httpRequestCount"]));
        }

        [TestMethod]
        public void TestStatsControlProfileUpgradeDoesNotRetrofitPercentiles()
        {
            using var client = new NoSQLClient(TestConfig);
            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.Regular,
                StatsEnableLog = false
            }, false);

            var firstRequest = MakeGetRequest(client);
            SetSuccessStats(firstRequest, latency: 10);
            control.Observe(firstRequest);

            control.SetProfile(StatsControl.Profile.More);

            var secondRequest = MakeGetRequest(client);
            SetSuccessStats(secondRequest, latency: 20);
            control.Observe(secondRequest);

            var snapshot = control.LogClientStatsForTest();
            var latency =
                FindRequest(snapshot, "Get")["httpRequestLatencyMs"]
                .AsMapValue;

            Assert.AreEqual(15.0, latency["avg"].AsDouble);
            Assert.IsFalse(latency.ContainsKey("95th"));
            Assert.IsFalse(latency.ContainsKey("99th"));
        }

        [TestMethod]
        public void TestStatsControlStartBeforeProfileActivatesOnProfileChange()
        {
            using var client = new NoSQLClient(TestConfig);
            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsEnableLog = false
            }, false);

            control.Start();
            Assert.IsFalse(control.IsStarted());
            Assert.IsNull(control.GenerateStats());

            control.SetProfile(StatsControl.Profile.Regular);
            Assert.IsTrue(control.IsStarted());

            var getRequest = MakeGetRequest(client);
            SetSuccessStats(getRequest);
            control.Observe(getRequest);

            var snapshot = control.LogClientStatsForTest();

            Assert.IsNotNull(FindRequest(snapshot, "Get"));
        }

        [TestMethod]
        public void TestQueryStatsOnlyInAllProfile()
        {
            using var client = new NoSQLClient(TestConfig);

            var moreControl = new StatsControlImpl(TestConfig, false)
                .SetProfile(StatsControl.Profile.More);
            var moreStats = new Stats((StatsControlImpl)moreControl);
            var moreQuery = MakeQueryRequest(client);
            SetSuccessStats(moreQuery);
            moreStats.ObserveQuery(moreQuery);
            moreStats.Observe(moreQuery, false);

            Assert.IsFalse(GenerateStats(moreStats)
                .ContainsKey("queries"));

            var allControl = new StatsControlImpl(TestConfig, false)
                .SetProfile(StatsControl.Profile.All);
            var allStats = new Stats((StatsControlImpl)allControl);
            var allQuery = MakeQueryRequest(client);
            SetSuccessStats(allQuery);
            allStats.ObserveQuery(allQuery);
            allStats.Observe(allQuery, false);

            var queries = GenerateStats(allStats)
                ["queries"].AsArrayValue;
            Assert.AreEqual(1, queries.Count);
            var query = queries[0].AsMapValue;
            Assert.AreEqual("SELECT * FROM Users", query["query"].AsString);
            Assert.AreEqual(1, AsLong(query["count"]));
            Assert.AreEqual(1, AsLong(query["unprepared"]));
            Assert.AreEqual(1, AsLong(query["httpRequestCount"]));
        }

        [TestMethod]
        public void TestStatsControlStartStopHandlerAndClear()
        {
            using var client = new NoSQLClient(TestConfig);
            MapValue handledStats = null;
            var handlerCount = 0;
            var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsEnableLog = false,
                StatsHandler = stats =>
                {
                    handledStats = stats;
                    handlerCount++;
                }
            }, false);

            control.SetProfile(StatsControl.Profile.Regular);
            control.Start();
            Assert.IsTrue(control.IsStarted());

            var getRequest = MakeGetRequest(client);
            SetSuccessStats(getRequest);
            control.Observe(getRequest);

            var firstSnapshot = control.LogClientStatsForTest();
            Assert.AreEqual(1, handlerCount);
            Assert.AreSame(firstSnapshot, handledStats);
            Assert.AreEqual(firstSnapshot.ToJsonString(),
                handledStats.ToJsonString());
            Assert.IsNotNull(FindRequest(firstSnapshot, "Get"));

            var secondSnapshot = control.LogClientStatsForTest();
            Assert.AreEqual(2, handlerCount);
            Assert.AreEqual(0, secondSnapshot["requests"].AsArrayValue.Count);

            control.Stop();
            Assert.IsFalse(control.IsStarted());
            var stoppedRequest = MakeGetRequest(client);
            SetSuccessStats(stoppedRequest);
            control.Observe(stoppedRequest);

            var stoppedSnapshot = control.LogClientStatsForTest();
            Assert.AreEqual(0,
                stoppedSnapshot["requests"].AsArrayValue.Count);

            control.SetStatsHandler(null);
            control.Shutdown();
        }

        [TestMethod]
        public void TestClientDisposeIsSafeFromStatsHandler()
        {
            var authorizationProvider =
                new SingleDisposeAuthorizationProvider();
            NoSQLClient client = null;
            client = new NoSQLClient(new NoSQLConfig
            {
                ServiceType = ServiceType.KVStore,
                Endpoint = "localhost:8080",
                AuthorizationProvider = authorizationProvider,
                StatsProfile = StatsControl.Profile.Regular,
                StatsEnableLog = false,
                StatsHandler = _ => client.Dispose()
            });

            client.Dispose();
            client.Dispose();

            Assert.AreEqual(1, authorizationProvider.DisposeCount);
        }

        private sealed class TestLogger : ILogger
        {
            internal List<string> Messages { get; } = new List<string>();

            internal string SingleMessage
            {
                get
                {
                    Assert.AreEqual(1, Messages.Count);
                    return Messages[0];
                }
            }

            public IDisposable BeginScope<TState>(TState state) =>
                NullScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId,
                TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                if (formatter != null)
                {
                    Messages.Add(formatter(state, exception));
                }
            }

            private sealed class NullScope : IDisposable
            {
                internal static readonly NullScope Instance = new();

                public void Dispose()
                {
                }
            }
        }

        private sealed class ThrowingLogger : ILogger
        {
            public IDisposable BeginScope<TState>(TState state) =>
                NoopScope.Instance;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId,
                TState state, Exception exception,
                Func<TState, Exception, string> formatter) =>
                throw new InvalidOperationException("logger failure");

            private sealed class NoopScope : IDisposable
            {
                internal static readonly NoopScope Instance = new();

                public void Dispose()
                {
                }
            }
        }

        private sealed class SingleDisposeAuthorizationProvider :
            IAuthorizationProvider, IDisposable
        {
            internal int DisposeCount { get; private set; }

            public void ConfigureAuthorization(NoSQLConfig config)
            {
            }

            public Task ApplyAuthorizationAsync(Request request,
                HttpRequestMessage message,
                CancellationToken cancellationToken) => Task.CompletedTask;

            public void Dispose()
            {
                if (++DisposeCount > 1)
                {
                    throw new InvalidOperationException(
                        "Authorization provider disposed more than once.");
                }
            }
        }
    }
}
