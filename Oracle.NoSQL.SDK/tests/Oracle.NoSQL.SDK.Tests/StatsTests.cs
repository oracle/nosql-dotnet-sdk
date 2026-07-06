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
        public void TestReqStatsSuccessAggregationAndPercentiles()
        {
            var reqStats = new ReqStats(true);

            reqStats.Observe(false, 1, 25, 5, 0, 1, 50, 40, 1);
            reqStats.Observe(false, 0, 0, 0, 0, 0, 70, 90, 2);
            reqStats.Observe(false, 0, 0, 0, 0, 0, 80, 120, 3);
            reqStats.Observe(false, 0, 0, 0, 0, 0, 90, 130, 4);
            reqStats.Observe(false, 0, 0, 0, 0, 0, 100, 140, 100);

            var map = new MapValue();
            reqStats.ToMapValue(map);

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

            var map = new MapValue();
            reqStats.ToMapValue(map);

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

            var map = new MapValue();
            reqStats.ToMapValue(map);

            Assert.AreEqual(1, AsLong(map["httpRequestCount"]));
            Assert.AreEqual(1, AsLong(map["errors"]));
            Assert.IsFalse(map.ContainsKey("httpRequestLatencyMs"));
            Assert.IsFalse(map.ContainsKey("requestSize"));
            Assert.IsFalse(map.ContainsKey("resultSize"));
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
            request.StatsRateLimitDelayMs = 11;
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

            var generated = stats.GenerateFieldValueStats(DateTime.UtcNow);

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
            using var meter = metrics.MeterFactory.Create(
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

            var firstSnapshot = stats.GenerateFieldValueStats(DateTime.UtcNow);
            Assert.IsTrue(firstSnapshot.ContainsKey("connections"));
            Assert.IsTrue(firstSnapshot.ContainsKey("queries"));
            Assert.AreEqual(2, firstSnapshot["requests"].AsArrayValue.Count);

            stats.ClearStats();
            var secondSnapshot = stats.GenerateFieldValueStats(DateTime.UtcNow);
            Assert.IsFalse(secondSnapshot.ContainsKey("connections"));
            Assert.IsFalse(secondSnapshot.ContainsKey("queries"));
            Assert.AreEqual(0, secondSnapshot["requests"].AsArrayValue.Count);
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

            var generated = stats.GenerateFieldValueStats(DateTime.UtcNow);

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
                QueryPlan = "driver plan",
                OperationCode = QueryRequest.OperationCodeSelect
            };
            var queryRequest = new QueryRequest<RecordValue>(
                client, preparedStatement, null);

            stats.ObserveQuery(queryRequest);
            SetSuccessStats(queryRequest, 100, 300, 10, 1);
            stats.Observe(queryRequest, false);

            var query = stats.GenerateFieldValueStats(DateTime.UtcNow)
                ["queries"].AsArrayValue[0].AsMapValue;

            Assert.AreEqual("SELECT * FROM Users",
                query["query"].AsString);
            Assert.AreEqual(1, AsLong(query["count"]));
            Assert.AreEqual(0, AsLong(query["unprepared"]));
            Assert.IsTrue(query["simple"].AsBoolean);
            Assert.IsFalse(query["doesWrites"].AsBoolean);
            Assert.AreEqual("driver plan", query["plan"].AsString);
            Assert.AreEqual(1, AsLong(query["httpRequestCount"]));
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

            var query = stats.GenerateFieldValueStats(DateTime.UtcNow)
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

            stats.ObserveQuery(queryRequest);
            SetSuccessStats(queryRequest, 120, 360, 20, 1);
            stats.Observe(queryRequest, false);

            stats.ObserveQuery(queryRequest);
            SetSuccessStats(queryRequest, 140, 420, 30, 1);
            stats.Observe(queryRequest, false);

            var queries = stats.GenerateFieldValueStats(DateTime.UtcNow)
                ["queries"].AsArrayValue;
            Assert.AreEqual(1, queries.Count);

            var query = queries[0].AsMapValue;
            Assert.AreEqual("SELECT * FROM Users", query["query"].AsString);
            Assert.AreEqual(3, AsLong(query["count"]));
            Assert.AreEqual(3, AsLong(query["unprepared"]));
            Assert.AreEqual(3, AsLong(query["httpRequestCount"]));
            AssertMinAvgMax(query["requestSize"].AsMapValue, 100, 120, 140);
            AssertMinAvgMax(query["resultSize"].AsMapValue, 300, 360, 420);
            AssertMinAvgMax(query["httpRequestLatencyMs"].AsMapValue,
                10, 20, 30);
        }

        [TestMethod]
        public void TestQueryErrorStatsFollowJavaNestedQueryBehavior()
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

            var generated = stats.GenerateFieldValueStats(DateTime.UtcNow);
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
            Assert.AreEqual(99.0, queryStats["requestSize"]
                .AsMapValue["avg"].AsDouble);
            Assert.AreEqual(299.0, queryStats["resultSize"]
                .AsMapValue["avg"].AsDouble);
            Assert.AreEqual(9.0, queryStats["httpRequestLatencyMs"]
                .AsMapValue["avg"].AsDouble);
        }

        [TestMethod]
        public void TestStatsControlProfileTransitionsKeepExistingStatsAndEnableQueryStats()
        {
            using var client = new NoSQLClient(TestConfig);
            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsProfile = StatsControl.Profile.More,
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
                stats.GenerateFieldValueStats(DateTime.UtcNow), "Get");

            Assert.AreEqual(1000, AsLong(getStats["httpRequestCount"]));
            Assert.AreEqual(0, AsLong(getStats["errors"]));
            Assert.AreEqual(1, AsLong(getStats["httpRequestLatencyMs"]
                .AsMapValue["min"]));
            Assert.AreEqual(10, AsLong(getStats["httpRequestLatencyMs"]
                .AsMapValue["max"]));
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
            Assert.AreSame(snapshot, handledStats);
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
            var output = logger.Messages.Last();

            Assert.IsNotNull(snapshot);
            Assert.IsTrue(output.Contains(StatsControl.LogPrefix));
            Assert.IsTrue(output.Contains("\"name\":\"Get\""));
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
        public void TestStatsControlStartBeforeProfileNeedsRestart()
        {
            using var client = new NoSQLClient(TestConfig);
            using var control = new StatsControlImpl(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                StatsEnableLog = false
            }, false);

            control.Start();
            control.SetProfile(StatsControl.Profile.Regular);

            var getRequest = MakeGetRequest(client);
            SetSuccessStats(getRequest);
            control.Observe(getRequest);

            Assert.IsNull(control.GenerateStats());

            control.Start();
            var restartedRequest = MakeGetRequest(client);
            SetSuccessStats(restartedRequest);
            control.Observe(restartedRequest);

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

            Assert.IsFalse(moreStats.GenerateFieldValueStats(DateTime.UtcNow)
                .ContainsKey("queries"));

            var allControl = new StatsControlImpl(TestConfig, false)
                .SetProfile(StatsControl.Profile.All);
            var allStats = new Stats((StatsControlImpl)allControl);
            var allQuery = MakeQueryRequest(client);
            SetSuccessStats(allQuery);
            allStats.ObserveQuery(allQuery);
            allStats.Observe(allQuery, false);

            var queries = allStats.GenerateFieldValueStats(DateTime.UtcNow)
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
    }
}
