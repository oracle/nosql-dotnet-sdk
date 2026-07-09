/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Net.Sockets;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static TestSchemas;
    using static TestTables;

    // Integration tests for the real request execution path.  These prove that
    // Http.Client populates stats fields and NoSQLClient routes final
    // success/error outcomes into StatsControl.
    [TestClass]
    [TestCategory("Integration")]
    public class StatsExecutionPathTests :
        TablesTestBase<StatsExecutionPathTests>
    {
        /*
         * These tests exercise the real HTTP execution path. Most use a
         * running Cloud Simulator; the ListTables test runs against on-prem
         * KVStore and needs its HTTP proxy at the configured endpoint.
         *
         * Start CloudSim, for example:
         *
         *   ./runCloudSim -root ./cloudsim-root -httpPort 8080 -storePort 5010
         *
         * Then run:
         *
         *   dotnet test Oracle.NoSQL.SDK/tests/Oracle.NoSQL.SDK.Tests/Oracle.NoSQL.SDK.Tests.csproj --framework net10.0 --filter StatsExecutionPathTests
         *
         * To run the on-prem ListTables path, start KVLite and its HTTP
         * proxy, then pass Oracle.NoSQL.SDK.Samples/kvlite.json as the
         * noSQLConfigFile test parameter (see README-DEV.md).
         *
         * Tests that do not match the configured service type are marked
         * inconclusive instead of failing normal test runs.
         */
        private static readonly TableInfo Table = GetSimpleTableWithName(
            TableNamePrefix + "StatsExec" + Environment.ProcessId);

        private static readonly MapValue Row = new MapValue
        {
            ["id"] = 1,
            ["lastName"] = "Stats",
            ["firstName"] = "Execution",
            ["info"] = new MapValue
            {
                ["source"] = "execution-path-test"
            },
            ["startDate"] = DateTime.UtcNow
        };

        private static readonly MapValue PrimaryKey =
            MakePrimaryKey(Table, Row);

        private static bool cloudSimAvailable;
        private static bool tableCreated;

        [ClassInitialize]
        public static async Task ClassInitializeAsync(
            TestContext testContext)
        {
            ClassInitialize(testContext);

            cloudSimAvailable = IsCloudSim &&
                                await IsEndpointAvailableAsync();
            if (!cloudSimAvailable)
            {
                return;
            }

            try
            {
                await DropTableAsync(Table);
                await CreateTableAsync(Table);
                var result = await client.PutAsync(Table.Name, Row);
                Assert.IsTrue(result.Success);
                tableCreated = true;
            }
            catch (Exception ex)
            {
                testContext.WriteLine(
                    "CloudSim stats setup did not complete: " +
                    ex.Message);
                cloudSimAvailable = false;
            }
        }

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            if (tableCreated)
            {
                await DropTableAsync(Table);
            }

            ClassCleanup();
        }

        [TestMethod]
        [TestCategory("CloudSim")]
        public async Task TestSuccessfulRequestPopulatesStatsFromHttpPath()
        {
            CheckCloudSimAvailable();

            using var statsClient = new NoSQLClient(MakeStatsConfig());

            for (var idx = 0; idx < 5; idx++)
            {
                var result = await statsClient.GetAsync(Table.Name,
                    PrimaryKey);
                Assert.IsNotNull(result.Row);
            }

            var snapshot = ((StatsControlImpl)statsClient.GetStatsControl())
                .LogClientStatsForTest();
            var getStats = FindRequest(snapshot, "Get");

            Assert.AreEqual(5, AsLong(getStats["httpRequestCount"]));
            Assert.AreEqual(0, AsLong(getStats["errors"]));
            AssertPositiveMinAvgMax(getStats["requestSize"].AsMapValue);
            AssertPositiveMinAvgMax(getStats["resultSize"].AsMapValue);
            AssertPositiveMinAvgMax(
                getStats["httpRequestLatencyMs"].AsMapValue,
                allowZeroMin: true);
            AssertConnectionStats(snapshot["connections"].AsMapValue);
        }

        [TestMethod]
        [TestCategory("CloudSim")]
        public async Task TestFailedRequestRoutesThroughObserveError()
        {
            CheckCloudSimAvailable();

            using var statsClient = new NoSQLClient(MakeStatsConfig());

            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                statsClient.GetAsync(
                    Table.Name + "Missing",
                    PrimaryKey));

            var snapshot = ((StatsControlImpl)statsClient.GetStatsControl())
                .LogClientStatsForTest();
            var getStats = FindRequest(snapshot, "Get");

            Assert.AreEqual(1, AsLong(getStats["httpRequestCount"]));
            Assert.AreEqual(1, AsLong(getStats["errors"]));
            Assert.IsFalse(getStats.ContainsKey("requestSize"));
            Assert.IsFalse(getStats.ContainsKey("resultSize"));
            Assert.IsFalse(getStats.ContainsKey("httpRequestLatencyMs"));
            AssertConnectionStats(snapshot["connections"].AsMapValue);
        }

        [TestMethod]
        [TestCategory("CloudSim")]
        public async Task TestQueryStatsArePopulatedFromHttpPath()
        {
            CheckCloudSimAvailable();

            using var statsClient = new NoSQLClient(
                MakeStatsConfig(StatsControl.Profile.All));
            var sql = $"SELECT * FROM {Table.Name} WHERE id = 1";

            var result = await statsClient.QueryAsync(sql);
            Assert.AreEqual(1, result.Rows.Count);

            var snapshot = ((StatsControlImpl)statsClient.GetStatsControl())
                .LogClientStatsForTest();
            var queryRequestStats = FindRequest(snapshot, "Query");

            Assert.IsTrue(AsLong(queryRequestStats["httpRequestCount"]) >= 1);
            Assert.AreEqual(0, AsLong(queryRequestStats["errors"]));
            AssertPositiveMinAvgMax(
                queryRequestStats["requestSize"].AsMapValue);
            AssertPositiveMinAvgMax(
                queryRequestStats["resultSize"].AsMapValue);
            AssertPositiveMinAvgMax(
                queryRequestStats["httpRequestLatencyMs"].AsMapValue,
                allowZeroMin: true);

            var queryStats = FindQuery(snapshot, sql);
            Assert.IsTrue(AsLong(queryStats["count"]) >= 1);
            Assert.IsTrue(AsLong(queryStats["unprepared"]) >= 1);
            Assert.IsTrue(AsLong(queryStats["httpRequestCount"]) >= 1);
            AssertConnectionStats(snapshot["connections"].AsMapValue);
        }

        [TestMethod]
        [TestCategory("CloudSim")]
        public async Task TestPreparedQueryStatsCountLogicalQuery()
        {
            CheckCloudSimAvailable();

            using var statsClient = new NoSQLClient(
                MakeStatsConfig(StatsControl.Profile.All));
            var sql = $"SELECT * FROM {Table.Name} WHERE id = 1";
            var preparedStatement = await statsClient.PrepareAsync(sql);

            // Clear the Prepare request stats so this test only verifies the
            // direct prepared-query execution path.
            ((StatsControlImpl)statsClient.GetStatsControl())
                .LogClientStatsForTest();

            var result = await statsClient.QueryAsync(preparedStatement);
            Assert.AreEqual(1, result.Rows.Count);

            var snapshot = ((StatsControlImpl)statsClient.GetStatsControl())
                .LogClientStatsForTest();
            var queryRequestStats = FindRequest(snapshot, "Query");
            var queryStats = FindQuery(snapshot, sql);

            Assert.IsTrue(AsLong(queryRequestStats["httpRequestCount"]) >= 1);
            Assert.AreEqual(1, AsLong(queryStats["count"]));
            Assert.AreEqual(0, AsLong(queryStats["unprepared"]));
            Assert.IsTrue(AsLong(queryStats["httpRequestCount"]) >= 1);
        }

        [TestMethod]
        [TestCategory("CloudSim")]
        public async Task TestAsyncEnumerableCountsOneLogicalQuery()
        {
            CheckCloudSimAvailable();

            using var statsClient = new NoSQLClient(
                MakeStatsConfig(StatsControl.Profile.All));
            for (var id = 2; id <= 3; id++)
            {
                var row = new MapValue
                {
                    ["id"] = id,
                    ["lastName"] = "Stats",
                    ["firstName"] = "Execution",
                    ["info"] = new MapValue { ["source"] = "enumerable" },
                    ["startDate"] = DateTime.UtcNow
                };
                Assert.IsTrue((await statsClient.PutAsync(Table.Name, row))
                    .Success);
            }

            // Exclude setup writes from the query snapshot.
            ((StatsControlImpl)statsClient.GetStatsControl())
                .LogClientStatsForTest();

            var sql = $"SELECT * FROM {Table.Name}";
            var batchCount = 0;
            var rowCount = 0;
            await foreach (var result in statsClient
                               .GetQueryAsyncEnumerable(sql,
                                   new QueryOptions { Limit = 1 }))
            {
                batchCount++;
                rowCount += result.Rows.Count;
            }

            Assert.IsTrue(batchCount > 1);
            Assert.IsTrue(rowCount >= 3);

            var snapshot = ((StatsControlImpl)statsClient.GetStatsControl())
                .LogClientStatsForTest();
            var queryStats = FindQuery(snapshot, sql);

            Assert.AreEqual(1, AsLong(queryStats["count"]));
            Assert.IsTrue(AsLong(queryStats["httpRequestCount"]) > 1);
        }

        [TestMethod]
        [TestCategory("CloudSim")]
        public async Task TestPeriodicHandlerReceivesAndClearsSnapshot()
        {
            CheckCloudSimAvailable();

            var snapshotTask =
                new TaskCompletionSource<MapValue>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
            var statsConfig = MakeStatsConfig(StatsControl.Profile.Regular);
            statsConfig.StatsInterval = TimeSpan.FromSeconds(1);
            statsConfig.StatsHandler = stats =>
            {
                var getStats = TryFindRequest(stats, "Get");
                if (getStats != null &&
                    AsLong(getStats["httpRequestCount"]) > 0)
                {
                    snapshotTask.TrySetResult(stats);
                }
            };

            using var statsClient = new NoSQLClient(statsConfig);
            var result = await statsClient.GetAsync(Table.Name, PrimaryKey);
            Assert.IsNotNull(result.Row);

            var completedTask = await Task.WhenAny(snapshotTask.Task,
                Task.Delay(TimeSpan.FromSeconds(3)));
            Assert.AreSame(snapshotTask.Task, completedTask);

            var snapshot = await snapshotTask.Task;
            var getStats = FindRequest(snapshot, "Get");
            Assert.AreEqual(1, AsLong(getStats["httpRequestCount"]));
            Assert.AreEqual(0, AsLong(getStats["errors"]));

            var clearedSnapshot =
                ((StatsControlImpl)statsClient.GetStatsControl())
                .LogClientStatsForTest();
            Assert.AreEqual(0, clearedSnapshot["requests"].AsArrayValue.Count);
        }

        [TestMethod]
        [TestCategory("KVStore")]
        public async Task TestOnPremListTablesPopulatesStatsFromHttpPath()
        {
            CheckOnPrem();

            using var statsClient = new NoSQLClient(MakeStatsConfig());
            var result = await statsClient.ListTablesAsync();
            Assert.IsNotNull(result);

            var snapshot = ((StatsControlImpl)statsClient.GetStatsControl())
                .LogClientStatsForTest();
            var listTablesStats = FindRequest(snapshot, "ListTables");

            Assert.AreEqual(1,
                AsLong(listTablesStats["httpRequestCount"]));
            Assert.AreEqual(0, AsLong(listTablesStats["errors"]));
            AssertPositiveMinAvgMax(
                listTablesStats["requestSize"].AsMapValue);
            AssertPositiveMinAvgMax(
                listTablesStats["resultSize"].AsMapValue);
            AssertPositiveMinAvgMax(
                listTablesStats["httpRequestLatencyMs"].AsMapValue,
                allowZeroMin: true);
            AssertConnectionStats(snapshot["connections"].AsMapValue);
        }

        private static NoSQLConfig MakeStatsConfig(
            StatsControl.Profile profile = StatsControl.Profile.More)
        {
            var statsConfig = CopyConfig();
            statsConfig.StatsProfile = profile;
            statsConfig.StatsEnableLog = false;
            return statsConfig;
        }

        private static async Task<bool> IsEndpointAvailableAsync()
        {
            try
            {
                using var tcpClient = new TcpClient();
                var connectTask = tcpClient.ConnectAsync(
                    client.Config.Uri.Host, client.Config.Uri.Port);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(1));
                return await Task.WhenAny(connectTask, timeoutTask) ==
                       connectTask && tcpClient.Connected;
            }
            catch
            {
                return false;
            }
        }

        private static void CheckCloudSimAvailable()
        {
            if (!cloudSimAvailable)
            {
                Assert.Inconclusive(
                    "Stats execution-path tests require CloudSim at the " +
                    "configured endpoint");
            }
        }

        private static MapValue FindRequest(MapValue stats, string name)
        {
            var request = TryFindRequest(stats, name);
            if (request != null)
            {
                return request;
            }

            Assert.Fail($"Request stats for {name} were not generated");
            return null;
        }

        private static MapValue TryFindRequest(MapValue stats, string name)
        {
            if (!stats.ContainsKey("requests"))
            {
                return null;
            }

            foreach (var value in stats["requests"].AsArrayValue)
            {
                var request = value.AsMapValue;
                if (request["name"].AsString == name)
                {
                    return request;
                }
            }

            return null;
        }

        private static MapValue FindQuery(MapValue stats, string sql)
        {
            foreach (var value in stats["queries"].AsArrayValue)
            {
                var query = value.AsMapValue;
                if (query["query"].AsString == sql)
                {
                    return query;
                }
            }

            Assert.Fail($"Query stats for {sql} were not generated");
            return null;
        }

        private static long AsLong(FieldValue value) => value.ToInt64();

        private static void AssertPositiveMinAvgMax(MapValue stats,
            bool allowZeroMin = false)
        {
            if (allowZeroMin)
            {
                Assert.IsTrue(AsLong(stats["min"]) >= 0);
            }
            else
            {
                Assert.IsTrue(AsLong(stats["min"]) > 0);
            }

            Assert.IsTrue(stats["avg"].AsDouble > 0);
            Assert.IsTrue(AsLong(stats["max"]) > 0);
        }

        private static void AssertConnectionStats(MapValue connections)
        {
            Assert.IsTrue(AsLong(connections["min"]) >= 1);
            Assert.IsTrue(connections["avg"].AsDouble >= 1);
            Assert.IsTrue(AsLong(connections["max"]) >=
                          AsLong(connections["min"]));
            Assert.IsFalse(connections.ContainsKey("count"));
        }
    }
}
