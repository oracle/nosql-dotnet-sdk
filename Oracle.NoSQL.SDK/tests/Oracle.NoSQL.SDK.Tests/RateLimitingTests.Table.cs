/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Linq;
    using System.Runtime.InteropServices;
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

        private static readonly DataTestFixture ParentFixture =
            new DataTestFixture(AllTypesTable, new AllTypesRowFactory(), 20);

        private static readonly DataTestFixture ChildFixture =
            new DataTestFixture(AllTypesChildTable,
                new AllTypesChildRowFactory(ParentFixture.RowFactory), 20);

        private static async Task DoPutAsync(DataTestFixture fixture,
            NoSQLClient loopClient, int idx, Stats stats,
            CancellationToken cancellationToken)
        {
            var result = await loopClient.PutAsync(fixture.Table.Name,
                fixture.GetRow(idx), null, cancellationToken);
            stats.Add(result.ConsumedCapacity);
        }

        // CheckMinReads = true
        private static async Task GetLoopAsync(DataTestFixture fixture,
            NoSQLClient loopClient, int seconds, Stats stats,
            CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());
            do
            {
                var idx = random.Next(fixture.RowIdStart, fixture.RowIdEnd);
                var primaryKey = MakePrimaryKey(fixture.Table,
                    fixture.GetRow(idx));
                var result = await loopClient.GetAsync(fixture.Table.Name,
                    primaryKey, null, cancellationToken);
                Assert.IsNotNull(result.Row);
                stats.Add(result.ConsumedCapacity);
            } while (DateTime.UtcNow < endTime);
        }

        // CheckMinWrites = true
        private static async Task PutLoopAsync(DataTestFixture fixture,
            NoSQLClient loopClient, int seconds, Stats stats,
            CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());
            do
            {
                var idx = random.Next(fixture.RowIdStart, fixture.RowIdEnd);
                await DoPutAsync(fixture, loopClient, idx, stats,
                    cancellationToken);
            } while (DateTime.UtcNow < endTime);
        }

        // CheckMinReads = true, CheckMinWrites = true
        private static async Task PutGetLoopAsync(DataTestFixture fixture,
            NoSQLClient loopClient, int seconds, Stats stats,
            CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());
            var getOptions = new GetOptions
            {
                Consistency = Consistency.Absolute
            };

            do
            {
                var idx = random.Next(fixture.RowIdStart, fixture.RowIdEnd);
                await DoPutAsync(fixture, loopClient, idx, stats,
                    cancellationToken);
                var primaryKey = MakePrimaryKey(fixture.Table,
                    fixture.GetRow(idx));
                var result = await loopClient.GetAsync(fixture.Table.Name,
                    primaryKey, getOptions, cancellationToken);
                Assert.IsNotNull(result.Row);
                stats.Add(result.ConsumedCapacity);
            } while (DateTime.UtcNow < endTime);
        }

        // CheckMinWrites = true
        private static async Task DeleteLoopAsync(DataTestFixture fixture,
            NoSQLClient loopClient, int seconds, Stats stats,
            CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());
            do
            {
                var idx = random.Next(fixture.RowIdStart, fixture.RowIdEnd);
                var primaryKey = MakePrimaryKey(fixture.Table,
                    fixture.GetRow(idx));
                var result = await loopClient.DeleteAsync(fixture.Table.Name,
                    primaryKey, null, cancellationToken);
                stats.Add(result.ConsumedCapacity);
                await DoPutAsync(fixture, loopClient, idx, stats,
                    cancellationToken);
            } while (DateTime.UtcNow < endTime);
        }

        // CheckMinWrites = true
        private static async Task DeleteRangeLoopAsync(DataTestFixture fixture,
            NoSQLClient loopClient, int seconds, Stats stats,
            CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());

            // For child table, use all rows for each parent key, to make sure
            // we put back all rows we deleted.
            var childRowsPerParent =
                fixture.RowFactory is AllTypesChildRowFactory crf
                    ? crf.ChildRowsPerParent
                    : 1;
            var parentCount = fixture.FirstShardCount / childRowsPerParent;
            // test self-check
            Assert.IsTrue(parentCount > 0);

            do
            {
                var count = random.Next(parentCount) + 1;
                var startIdx = random.Next(parentCount - count + 1);
                count *= childRowsPerParent;
                startIdx *= childRowsPerParent;
                var endIdx = startIdx + count - 1;

                var partialPK = new MapValue
                {
                    ["shardId"] = fixture.GetRow(startIdx)["shardId"]
                };
                var fieldRange = new FieldRange("pkString")
                {
                    StartsWith = fixture.GetRow(startIdx)["pkString"],
                    EndsWith = fixture.GetRow(endIdx)["pkString"]
                };

                await foreach (
                    var result in loopClient.GetDeleteRangeAsyncEnumerable(
                        fixture.Table.Name, partialPK, fieldRange,
                        cancellationToken))
                {
                    stats.Add(result.ConsumedCapacity);
                }

                for (var idx = startIdx; idx <= endIdx; idx++)
                {
                    await DoPutAsync(fixture, loopClient, idx, stats,
                        cancellationToken);
                }
            } while (DateTime.UtcNow < endTime);
        }

        // CheckMinWrites = true
        private static async Task WriteManyLoopAsync(DataTestFixture fixture,
            NoSQLClient loopClient, int seconds, Stats stats,
            CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());
            do
            {
                var count = random.Next(fixture.FirstShardCount) + 1;
                var startIdx = random.Next(fixture.FirstShardCount - count + 1);
                // ReSharper disable once CoVariantArrayConversion
                RecordValue[] rows =
                    fixture.Rows[startIdx..(startIdx + count)];
                var primaryKeys = (from row in rows
                    select MakePrimaryKey(fixture.Table, row)).ToArray();
                var result = await loopClient.DeleteManyAsync(
                    fixture.Table.Name, primaryKeys, null, cancellationToken);
                stats.Add(result.ConsumedCapacity);
                result = await loopClient.PutManyAsync(fixture.Table.Name,
                    rows, null, cancellationToken);
                stats.Add(result.ConsumedCapacity);
            } while (DateTime.UtcNow < endTime);
        }

        // CheckMinWrites = true
        private static async Task MultiTableWriteManyLoopAsync(
            DataTestFixture fixture, NoSQLClient loopClient, int seconds,
            Stats stats, CancellationToken cancellationToken)
        {
            // We ignore "fixture" parameter here.
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());
            var woc = new WriteOperationCollection();

            do
            {
                var count = random.Next(5) + 1;
                var startIdx = random.Next(ParentFixture.FirstShardCount - count + 1);
                // ReSharper disable once CoVariantArrayConversion
                var parentRows = ParentFixture.Rows[startIdx..(startIdx + count)];

                count = random.Next(5) + 1;
                startIdx = random.Next(ChildFixture.FirstShardCount - count + 1);
                var childRows = ChildFixture.Rows[startIdx..(startIdx + count)];

                woc.Clear();
                
                foreach (var row in parentRows)
                {
                    woc.AddPutIfPresent(ParentFixture.Table.Name, row);
                }

                foreach (var row in childRows)
                {
                    woc.AddPutIfPresent(ChildFixture.Table.Name, row);
                }

                var result = await loopClient.WriteManyAsync(woc, null,
                    cancellationToken);
                stats.Add(result.ConsumedCapacity);
            } while (DateTime.UtcNow < endTime);
        }

        // CheckMinReads = true
        private static async Task PreparedQueryLoopAsync(
            DataTestFixture fixture, NoSQLClient loopClient, int seconds,
            Stats stats, bool isSinglePartition, bool isAdvanced,
            CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());

            var sql = isSinglePartition
                ? "DECLARE $shardId INTEGER; $pkString STRING; SELECT * FROM " +
                  $"{fixture.Table.Name} WHERE shardId = $shardId AND " +
                  "pkString = $pkString"
                : "DECLARE $colInteger INTEGER; SELECT * FROM " +
                  $"{fixture.Table.Name} WHERE colInteger >= $colInteger" +
                  (isAdvanced
                      ? " ORDER BY shardId DESC, pkString DESC LIMIT 20 " +
                        "OFFSET 1"
                      : "");

            var preparedStatement = await loopClient.PrepareAsync(sql, null,
                cancellationToken);
            stats.Add(preparedStatement.ConsumedCapacity);

            do
            {
                var idx = random.Next(fixture.RowIdStart, fixture.RowIdEnd);
                var row = fixture.GetRow(idx);
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
            DataTestFixture fixture, NoSQLClient loopClient, int seconds,
            Stats stats, bool isSinglePartition, bool isAdvanced,
            CancellationToken cancellationToken)
        {
            var endTime = DateTime.UtcNow + TimeSpan.FromSeconds(seconds);
            var random = new Random(GetRandomSeed());
            do
            {
                var idx = random.Next(fixture.RowIdStart, fixture.RowIdEnd);
                var row = fixture.GetRow(idx);
                var shardId = row["shardId"];
                var pkString = row["pkString"];
                var colInteger = row["colInteger"];

                var sql = isSinglePartition
                    ? $"SELECT * FROM {fixture.Table.Name} WHERE shardId = " +
                      $"'{shardId}' AND pkString = '{pkString}'"
                    : $"SELECT * FROM {fixture.Table.Name} WHERE " +
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
        private const double MaxUnitsDelta = 0.2;
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
                this.retryHandler = retryHandler ?? new NoSQLRetryHandler();
                        ;
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
            var configCopy = CopyConfig();

            configCopy.RateLimitingEnabled = true;
            configCopy.RateLimiterPercent = testCase.RateLimiterPercent;
            configCopy.RateLimiterCreator = testCase.RateLimiterCreator;

            if (configCopy.ServiceType == ServiceType.CloudSim)
            {
                configCopy.AuthorizationProvider = null;
            }

            configCopy.RetryHandler = new StatsRetryHandler(
                configCopy.RetryHandler, stats);
            configCopy.Timeout = TimeSpan.FromMinutes(1);

            return configCopy;
        }

        private static async Task TestTableLoopAsync(
            Func<DataTestFixture, NoSQLClient, int, Stats, CancellationToken,
                Task> loop,
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
                .SetTableLimitsWithCompletionAsync(ParentFixture.Table.Name,
                    tableLimits);

            var startTime = DateTime.UtcNow;

            var tasks = from idx in Enumerable.Range(0, testCase.LoopCount)
                // Using loopClient is safe here since we await completion of
                // all tasks before disposing it.
                // ReSharper disable once AccessToDisposedClosure
                // Use odd-numbered loops with the child table.
                select Task.Run(() => loop(
                    !SupportsChildTables || idx % 2 == 0
                        ? ParentFixture
                        : ChildFixture, loopClient,
                    testCase.Seconds, stats, CancellationToken.None));
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
            if (SupportsChildTables)
            {
                await DropTableAsync(ChildFixture.Table);
            }

            await DropTableAsync(ParentFixture.Table);
            await CreateTableAsync(ParentFixture.Table);
            await PutRowsAsync(ParentFixture.Table, ParentFixture.Rows);

            if (SupportsChildTables)
            {
                await CreateTableAsync(ChildFixture.Table);
                await PutRowsAsync(ChildFixture.Table, ChildFixture.Rows);
            }
        }

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            if (SupportsChildTables)
            {
                await DropTableAsync(ChildFixture.Table);
            }

            await DropTableAsync(ParentFixture.Table);
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
        public async Task TestMultiTableWriteManyLoopAsync(TestCase testCase)
        {
            CheckBasicOnly(testCase);
            CheckSupportsChildTables();
            await TestTableLoopAsync(MultiTableWriteManyLoopAsync, testCase,
                false, true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestPreparedQueryLoopBasicAsync(TestCase testCase)
        {
            await TestTableLoopAsync(
                (fixture, loopClient, seconds, stats, cancellationToken) =>
                    PreparedQueryLoopAsync(fixture, loopClient, seconds,
                        stats, false, false, cancellationToken),
                testCase, true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestPreparedQueryLoopSinglePartitionAsync(
            TestCase testCase)
        {
            CheckBasicOnly(testCase);
            await TestTableLoopAsync(
                (fixture, loopClient, seconds, stats, cancellationToken) =>
                    PreparedQueryLoopAsync(fixture, loopClient, seconds,
                        stats, true, false, cancellationToken),
                testCase, true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestPreparedQueryLoopAdvancedAsync(
            TestCase testCase)
        {
            CheckBasicOnly(testCase);
            await TestTableLoopAsync(
                (fixture, loopClient, seconds, stats, cancellationToken) =>
                    PreparedQueryLoopAsync(fixture, loopClient, seconds,
                        stats, false, true, cancellationToken),
                testCase, true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestUnpreparedQueryLoopBasicAsync(TestCase testCase)
        {
            // Currently min read units check fails here and in the same test for
            // Node.js driver.  This needs to be investigated.
            await TestTableLoopAsync(
                (fixture, loopClient, seconds, stats, cancellationToken) =>
                    UnpreparedQueryLoopAsync(fixture, loopClient, seconds,
                        stats, false, false, cancellationToken),
                testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestUnpreparedQueryLoopSinglePartitionAsync(
            TestCase testCase)
        {
            CheckBasicOnly(testCase);
            await TestTableLoopAsync(
                (fixture, loopClient, seconds, stats, cancellationToken) =>
                    UnpreparedQueryLoopAsync(fixture, loopClient, seconds,
                        stats, true, false, cancellationToken),
                testCase, true);
        }

        [DataTestMethod]
        [DynamicData(nameof(TableTestDataSource))]
        public async Task TestUnpreparedQueryLoopAdvancedAsync(
            TestCase testCase)
        {
            CheckBasicOnly(testCase);
            await TestTableLoopAsync(
                (fixture, loopClient, seconds, stats, cancellationToken) =>
                    UnpreparedQueryLoopAsync(fixture, loopClient, seconds,
                        stats, false, true, cancellationToken),
                testCase, true);
        }

    }

}
