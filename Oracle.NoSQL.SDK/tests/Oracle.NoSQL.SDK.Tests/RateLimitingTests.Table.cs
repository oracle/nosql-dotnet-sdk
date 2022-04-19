namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Serialization;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using System.Text.Json;
    using static TestSchemas;
    using static TestTables;

    public partial class RateLimitingTests : DataTestBase<RateLimitingTests>
    {
        private class Stats
        {
            private readonly object lockObj = new object();

            public long TotalOps { get; private set; }
            public long TotalReadUnits { get; private set; }
            public TimeSpan TotalReadDelay { get; private set; }
            public int ReadThrottleErrors { get; private set; }
            public long TotalWriteUnits { get; private set; }
            public TimeSpan TotalWriteDelay { get; private set; }
            public int WriteThrottleErrors { get; private set; }
            public TimeSpan TotalTime { get; private set; }
            public double ReadUnitsPerSecond { get; private set; }
            public double WriteUnitsPerSecond { get; private set; }

            internal void Add(ConsumedCapacity consumedCapacity)
            {
                lock (lockObj)
                {
                    Assert.IsNotNull(consumedCapacity);
                    TotalOps++;
                    TotalReadUnits += consumedCapacity.ReadUnits;
                    TotalWriteUnits += consumedCapacity.WriteUnits;
                    TotalReadDelay += consumedCapacity.ReadRateLimitDelay;
                    TotalWriteDelay += consumedCapacity.WriteRateLimitDelay;
                }
            }

            internal void Finish(DateTime startTime)
            {
                TotalTime = DateTime.UtcNow - startTime;
                ReadUnitsPerSecond = TotalReadUnits / TotalTime.TotalSeconds;
                WriteUnitsPerSecond = TotalWriteUnits / TotalTime.TotalSeconds;
            }

            internal void AddException(Exception ex)
            {
                if (ex is ReadThrottlingException)
                {
                    ReadThrottleErrors++;
                }
                else if (ex is WriteThrottlingException)
                {
                    WriteThrottleErrors++;
                }
            }

            public override string ToString() =>
                $"Total time: {TotalTime.TotalMilliseconds} ms\n" +
                $"Total ops: {TotalOps}\n" +
                $"Total read units: {TotalReadUnits}\n" +
                $"Total read delay: {TotalReadDelay.TotalMilliseconds} ms\n" +
                $"Read units per second: {ReadUnitsPerSecond}\n" +
                $"Read throttle errors: {ReadThrottleErrors}\n" +
                $"Total write units: {TotalWriteUnits}\n" +
                $"Total write delay: {TotalWriteDelay.TotalMilliseconds} ms\n" +
                $"Write units per second: {WriteUnitsPerSecond}\n" +
                $"Write throttle errors: {WriteThrottleErrors}";
        }

        public class TestCase
        {
            internal string Description { get; }
            internal double? RateLimiterPercent { get; set; }
            internal Func<IRateLimiter> RateLimiterCreator { get; set; }
            internal int ReadUnits { get; set; }
            internal int WriteUnits { get; set; }
            internal int Seconds { get; set; }
            internal int LoopCount { get; set; } = 1;
            internal bool BasicOnly { get; set; }

            internal TestCase(string description)
            {
                Description = description;
            }

            public override string ToString() => Description;
        }

        private static readonly DataTestFixture Fixture = new DataTestFixture(
            AllTypesTable, new AllTypesRowFactory(), 20);


        private static async Task DoPutAsync(NoSQLClient loopClient, int idx,
            Stats stats, CancellationToken cancellationToken)
        {
            var result = await loopClient.PutAsync(Fixture.Table.Name,
                Fixture.GetRow(idx), null, cancellationToken);
            stats.Add(result.ConsumedCapacity);
        }

        // CheckMinReads = true
        private static async Task GetLoopAsync(NoSQLClient loopClient,
            int seconds, Stats stats, CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());
            do
            {
                var idx = random.Next(Fixture.RowIdStart, Fixture.RowIdEnd);
                var primaryKey = MakePrimaryKey(Fixture.Table,
                    Fixture.GetRow(idx));
                var result = await loopClient.GetAsync(Fixture.Table.Name,
                    primaryKey, null, cancellationToken);
                Assert.IsNotNull(result.Row);
                stats.Add(result.ConsumedCapacity);
            } while (DateTime.UtcNow < endTime);
        }

        // CheckMinWrites = true
        private static async Task PutLoopAsync(NoSQLClient loopClient,
            int seconds, Stats stats, CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());
            do
            {
                var idx = random.Next(Fixture.RowIdStart, Fixture.RowIdEnd);
                await DoPutAsync(loopClient, idx, stats, cancellationToken);
            } while (DateTime.UtcNow < endTime);
        }

        // CheckMinReads = true, CheckMinWrites = true
        private static async Task PutGetLoopAsync(NoSQLClient loopClient,
            int seconds, Stats stats, CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());
            var getOptions = new GetOptions
            {
                Consistency = Consistency.Absolute
            };

            do
            {
                var idx = random.Next(Fixture.RowIdStart, Fixture.RowIdEnd);
                await DoPutAsync(loopClient, idx, stats, cancellationToken);
                var primaryKey = MakePrimaryKey(Fixture.Table,
                    Fixture.GetRow(idx));
                var result = await loopClient.GetAsync(Fixture.Table.Name,
                    primaryKey, getOptions, cancellationToken);
                Assert.IsNotNull(result.Row);
                stats.Add(result.ConsumedCapacity);
            } while (DateTime.UtcNow < endTime);
        }

        // CheckMinWrites = true
        private static async Task DeleteLoopAsync(NoSQLClient loopClient,
            int seconds, Stats stats, CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());
            do
            {
                var idx = random.Next(Fixture.RowIdStart, Fixture.RowIdEnd);
                var primaryKey = MakePrimaryKey(Fixture.Table,
                    Fixture.GetRow(idx));
                var result = await loopClient.DeleteAsync(Fixture.Table.Name,
                    primaryKey, null, cancellationToken);
                stats.Add(result.ConsumedCapacity);
                await DoPutAsync(loopClient, idx, stats, cancellationToken);
            } while (DateTime.UtcNow < endTime);
        }

        // CheckMinWrites = true
        private static async Task DeleteRangeLoopAsync(NoSQLClient loopClient,
            int seconds, Stats stats, CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());
            do
            {
                var count = random.Next(Fixture.RowsPerShard) + 1;
                var startIdx = random.Next(Fixture.RowsPerShard - count + 1);
                var endIdx = startIdx + count - 1;
                var partialPK = new MapValue
                {
                    ["shardId"] = Fixture.GetRow(startIdx)["shardId"]
                };
                var fieldRange = new FieldRange("pkString")
                {
                    StartsWith = Fixture.GetRow(startIdx)["pkString"],
                    EndsWith = Fixture.GetRow(endIdx)["pkString"]
                };

                await foreach (
                    var result in loopClient.GetDeleteRangeAsyncEnumerable(
                        Fixture.Table.Name, partialPK, fieldRange,
                        cancellationToken))
                {
                    stats.Add(result.ConsumedCapacity);
                }

                for (var idx = startIdx; idx <= endIdx; idx++)
                {
                    await DoPutAsync(loopClient, idx, stats, cancellationToken);
                }
            } while (DateTime.UtcNow < endTime);
        }

        // CheckMinWrites = true
        private static async Task WriteManyLoopAsync(NoSQLClient loopClient,
            int seconds, Stats stats, CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());
            do
            {
                var count = random.Next(Fixture.RowsPerShard) + 1;
                var startIdx = random.Next(Fixture.RowsPerShard - count + 1);
                // ReSharper disable once CoVariantArrayConversion
                RecordValue[] rows =
                    Fixture.Rows[startIdx..(startIdx + count)];
                var primaryKeys = (from row in rows
                    select MakePrimaryKey(Fixture.Table, row)).ToArray();
                var result = await loopClient.DeleteManyAsync(
                    Fixture.Table.Name, primaryKeys, null, cancellationToken);
                stats.Add(result.ConsumedCapacity);
                result = await loopClient.PutManyAsync(Fixture.Table.Name,
                    rows, null, cancellationToken);
                stats.Add(result.ConsumedCapacity);
            } while (DateTime.UtcNow < endTime);
        }

        // CheckMinReads = true
        private static async Task PreparedQueryLoopAsync(
            NoSQLClient loopClient, int seconds, Stats stats,
            bool isSinglePartition, bool isAdvanced,
            CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());

            var sql = isSinglePartition
                ? "DECLARE $shardId INTEGER; $pkString STRING; SELECT * FROM " +
                  $"{Fixture.Table.Name} WHERE shardId = $shardId AND " +
                  "pkString = $pkString"
                : "DECLARE $colInteger INTEGER; SELECT * FROM " +
                  $"{Fixture.Table.Name} WHERE colInteger >= $colInteger" +
                  (isAdvanced
                      ? " ORDER BY shardId DESC, pkString DESC LIMIT 20 " +
                        "OFFSET 1"
                      : "");

            var preparedStatement = await loopClient.PrepareAsync(sql, null,
                cancellationToken);
            stats.Add(preparedStatement.ConsumedCapacity);

            do
            {
                var idx = random.Next(Fixture.RowIdStart, Fixture.RowIdEnd);
                var row = Fixture.GetRow(idx);
                if (isSinglePartition)
                {
                    preparedStatement.Variables["$shardId"] = row["shardId"];
                    preparedStatement.Variables["$pkString"] =
                        row["pkString"];
                }
                else
                {
                    preparedStatement.Variables["$colInteger"] =
                        row["colInteger"];
                }

                await foreach (
                    var result in loopClient.GetQueryAsyncEnumerable(
                        preparedStatement, null, cancellationToken))
                {
                    stats.Add(result.ConsumedCapacity);
                }
            } while (DateTime.UtcNow < endTime);
        }

        // CheckMinReads = true
        private static async Task UnpreparedQueryLoopAsync(
            NoSQLClient loopClient, int seconds, Stats stats,
            bool isSinglePartition, bool isAdvanced,
            CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());
            do
            {
                var idx = random.Next(Fixture.RowIdStart, Fixture.RowIdEnd);
                var row = Fixture.GetRow(idx);
                var shardId = row["shardId"];
                var pkString = row["pkString"];
                var colInteger = row["colInteger"];

                var sql = isSinglePartition
                    ? $"SELECT * FROM {Fixture.Table.Name} WHERE shardId = " +
                      $"'{shardId}' AND pkString = '{pkString}'"
                    : $"SELECT * FROM {Fixture.Table.Name} WHERE " +
                      $"colInteger >= {colInteger}" +
                      (isAdvanced
                          ? " ORDER BY shardId DESC, pkString DESC " +
                            "LIMIT 20 OFFSET 1"
                          : "");

                await foreach (
                    var result in loopClient.GetQueryAsyncEnumerable(
                        sql, null, cancellationToken))
                {
                    stats.Add(result.ConsumedCapacity);
                }
            } while (DateTime.UtcNow < endTime);
        }

        // ratio of table units
        private const double MaxUnitsDelta = 0.15;
        // per second, max 15 per 30 seconds
        private const double MaxThrottleRate = 0.5;

        private static void CheckStats(Stats stats, TestCase testCase,
            bool checkMinReadUnits, bool checkMinWriteUnits)
        {
            var readUnits = testCase.RateLimiterPercent.HasValue
                ? testCase.ReadUnits * testCase.RateLimiterPercent.Value / 100
                : testCase.ReadUnits;
            var writeUnits = testCase.RateLimiterPercent.HasValue
                ? testCase.WriteUnits * testCase.RateLimiterPercent.Value / 100
                : testCase.WriteUnits;

            Assert.IsTrue(stats.ReadUnitsPerSecond <=
                          readUnits * (1 + MaxUnitsDelta));
            if (checkMinReadUnits)
            {
                Assert.IsTrue(stats.ReadUnitsPerSecond >=
                              readUnits * (1 - MaxUnitsDelta));
            }

            Assert.IsTrue(stats.WriteUnitsPerSecond <=
                          writeUnits * (1 + MaxUnitsDelta));
            if (checkMinWriteUnits)
            {
                Assert.IsTrue(stats.WriteUnitsPerSecond >=
                              writeUnits * (1 - MaxUnitsDelta));
            }

            if (testCase.LoopCount == 1)
            {
                Assert.IsTrue(stats.ReadThrottleErrors <=
                              MaxThrottleRate * testCase.Seconds);
                Assert.IsTrue(stats.WriteThrottleErrors <=
                              MaxThrottleRate * testCase.Seconds);
            }
        }

        // For now, the only way to access exceptions during retries is to
        // capture them in the retry handler.  In future we may add events for
        // this as well.  Note that in current implementation the exception
        // will be captured even when request times out (see
        // ExecuteValidatedRequestAsync in NoSQLClient.Internal.cs).
        private class StatsRetryHandler : IRetryHandler
        {
            private readonly IRetryHandler retryHandler;
            private readonly Stats stats;

            internal StatsRetryHandler(IRetryHandler retryHandler,
                Stats stats)
            {
                this.retryHandler = retryHandler;
                this.stats = stats;
            }

            public bool ShouldRetry(Request request)
            {
                stats.AddException(request.LastException);
                return retryHandler.ShouldRetry(request);
            }

            public TimeSpan GetRetryDelay(Request request) =>
                retryHandler.GetRetryDelay(request);
        }

        private static NoSQLConfig MakeConfig(TestCase testCase, Stats stats)
        {
            var config = client.Config.Clone();
            
            config.RateLimitingEnabled = true;
            config.RateLimiterPercent = testCase.RateLimiterPercent;
            config.RateLimiterCreator = testCase.RateLimiterCreator;

            if (config.ServiceType == ServiceType.CloudSim)
            {
                config.AuthorizationProvider = null;
            }

            config.RetryHandler = new StatsRetryHandler(config.RetryHandler,
                stats);
            config.Timeout = TimeSpan.FromMinutes(1);

            return config;
        }

        private static async Task TestTableLoopAsync(
            Func<NoSQLClient, int, Stats, CancellationToken, Task> loop,
            TestCase testCase, bool checkMinReadUnits = false,
            bool checkMinWriteUnits = false, bool limiterBackgroundInit = false)
        {
            var tableLimits = testCase.ReadUnits > 0
                ? new TableLimits(testCase.ReadUnits, testCase.WriteUnits,
                    DefaultTableLimits.StorageGB)
                : DefaultOnDemandTableLimits;
            var stats = new Stats();

            using var loopClient = new NoSQLClient(MakeConfig(testCase, stats));

            // If limiterBackgroundInit = true, loopClient will init its rate
            // limiters, in the background while the loop is already running.
            // For some ops that consume a lot of units this will affect the
            // test results, so we don't always use this option.
            await (limiterBackgroundInit ? client : loopClient)
                .SetTableLimitsWithCompletionAsync(Fixture.Table.Name,
                    tableLimits);

            var startTime = DateTime.UtcNow;

            var tasks = from _ in Enumerable.Range(0, testCase.LoopCount)
                // Using loopClient is safe here since we await completion of
                // all tasks before disposing it.
                // ReSharper disable once AccessToDisposedClosure
                select Task.Run(() => loop(loopClient, testCase.Seconds,
                    stats, CancellationToken.None));
            await Task.WhenAll(tasks);

            stats.Finish(startTime);
            Debug.WriteLine("Stats:\n" + stats);
            CheckStats(stats, testCase, checkMinReadUnits,
                checkMinWriteUnits);
        }

        // Note that the rate limiter postpones waiting to the next op.  This
        // means that we could have significant stats deviations if the test
        // run has very few ops, especially if each op consumes fairly large
        // number of units (e.g. multi-row query).  So the run time of the
        // test case should be inversely proportional to the unit limit.

        private static readonly TestCase[] TableTestCases =
        {
            new TestCase("Default test case, 8 concurrent loops")
            {
                ReadUnits = 50,
                WriteUnits = 50,
                Seconds = 30,
                LoopCount = 8
            },
            new TestCase("Percentage 52%, 5 concurrent loops")
            {
                RateLimiterPercent = 52,
                ReadUnits = 50,
                WriteUnits = 50,
                Seconds = 60,
                LoopCount = 5
            },
            new TestCase("RateLimiterCreator for NoSQLRateLimiter, basic")
            {
                RateLimiterCreator = () => new NoSQLRateLimiter(),
                ReadUnits = 200,
                WriteUnits = 200,
                Seconds = 15,
                LoopCount = 1,
                BasicOnly = true
            }
        };

        private static IEnumerable<object[]> TableTestDataSource =>
            from testCase in TableTestCases select new object[] { testCase };

        private static void CheckBasicOnly(TestCase testCase)
        {
            if (testCase.BasicOnly)
            {
                Assert.Inconclusive("Skipped for basic-only test case " +
                                    testCase);
            }
        }

        [ClassInitialize]
        public static async Task ClassInitializeAsync(TestContext testContext)
        {
            ClassInitialize(testContext);
            await DropTableAsync(Fixture.Table);
            await CreateTableAsync(Fixture.Table);
            await PutRowsAsync(Fixture.Table, Fixture.Rows);
        }

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            await DropTableAsync(Fixture.Table);
            ClassCleanup();
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestGetLoopAsync(TestCase testCase)
        {
            CheckBasicOnly(testCase);
            await TestTableLoopAsync(GetLoopAsync, testCase, true, false,
                true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestPutLoopAsync(TestCase testCase)
        {
            CheckBasicOnly(testCase);
            await TestTableLoopAsync(PutLoopAsync, testCase, false, true,
                true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestPutGetLoopAsync(TestCase testCase)
        {
            await TestTableLoopAsync(PutGetLoopAsync, testCase, true, true,
                true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestDeleteLoopAsync(TestCase testCase)
        {
            CheckBasicOnly(testCase);
            await TestTableLoopAsync(DeleteLoopAsync, testCase, false, true,
                true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestDeleteRangeLoopAsync(TestCase testCase)
        {
            CheckBasicOnly(testCase);
            await TestTableLoopAsync(DeleteRangeLoopAsync, testCase, false,
                true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestWriteManyLoopAsync(TestCase testCase)
        {
            CheckBasicOnly(testCase);
            await TestTableLoopAsync(WriteManyLoopAsync, testCase, false,
                true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestPreparedQueryLoopBasicAsync(TestCase testCase)
        {
            await TestTableLoopAsync(
                (loopClient, seconds, stats, cancellationToken) =>
                    PreparedQueryLoopAsync(loopClient, seconds, stats, false,
                        false, cancellationToken),
                testCase, true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestPreparedQueryLoopSinglePartitionAsync(
            TestCase testCase)
        {
            CheckBasicOnly(testCase);
            await TestTableLoopAsync(
                (loopClient, seconds, stats, cancellationToken) =>
                    PreparedQueryLoopAsync(loopClient, seconds, stats, true,
                        false, cancellationToken),
                testCase, true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestPreparedQueryLoopAdvancedAsync(
            TestCase testCase)
        {
            CheckBasicOnly(testCase);
            await TestTableLoopAsync(
                (loopClient, seconds, stats, cancellationToken) =>
                    PreparedQueryLoopAsync(loopClient, seconds, stats, false,
                        true, cancellationToken),
                testCase, true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestUnpreparedQueryLoopBasicAsync(TestCase testCase)
        {
            await TestTableLoopAsync(
                (loopClient, seconds, stats, cancellationToken) =>
                    UnpreparedQueryLoopAsync(loopClient, seconds, stats,
                        false, false, cancellationToken),
                testCase, true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestUnpreparedQueryLoopSinglePartitionAsync(
            TestCase testCase)
        {
            CheckBasicOnly(testCase);
            await TestTableLoopAsync(
                (loopClient, seconds, stats, cancellationToken) =>
                    UnpreparedQueryLoopAsync(loopClient, seconds, stats, true,
                        false, cancellationToken),
                testCase, true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestUnpreparedQueryLoopAdvancedAsync(
            TestCase testCase)
        {
            CheckBasicOnly(testCase);
            await TestTableLoopAsync(
                (loopClient, seconds, stats, cancellationToken) =>
                    UnpreparedQueryLoopAsync(loopClient, seconds, stats, false,
                        true, cancellationToken),
                testCase, true);
        }

    }

}
