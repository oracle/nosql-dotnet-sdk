/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Utils;
    using static NegativeTestData;
    using static TestSchemas;
    using static TestTables;

    [TestClass]
    public partial class GlobalActiveTableTests :
        TablesTestBase<TableDDLTests>
    {
        private static readonly TableInfo Table = GetSimpleTableWithLimits(
            new TableLimits(5, 5, 1));
        private static readonly DateTime SampleTime = DateTime.UtcNow;

        private static readonly string[] BadRegionIdsNotNull =
            { string.Empty, "no-such-region-1" };
        private static readonly IEnumerable<string> BadRegionIds =
            BadRegionIdsNotNull.Append(null);

        private static readonly IEnumerable<AddReplicaOptions>
            BadAddReplicaOpts =
                (from opt in BadTableCompletionOptions
                    select new AddReplicaOptions
                    {
                        Timeout = opt.Timeout,
                        PollDelay = opt.PollDelay
                    })
                .Concat(from readUnits in BadPositiveInt32
                    select new AddReplicaOptions
                    {
                        ReadUnits = readUnits
                    })
                .Concat(
                    from writeUnits in BadPositiveInt32
                    select new AddReplicaOptions
                    {
                        WriteUnits = writeUnits
                    });

        private static readonly IEnumerable<DropReplicaOptions>
            BadDropReplicaOpts =
                from opt in BadTableCompletionOptions
                select new DropReplicaOptions
                {
                    Timeout = opt.Timeout,
                    PollDelay = opt.PollDelay
                };

        private static readonly IEnumerable<GetReplicaStatsOptions>
            BadGetReplicaStatsOpts =
                (from timeout in BadTimeSpans
                    select new GetReplicaStatsOptions
                    {
                        Timeout = timeout
                    })
                // StartTime may not have kind Unspecified
                .Append(new GetReplicaStatsOptions
                {
                    StartTime = DateTime.SpecifyKind(SampleTime,
                        DateTimeKind.Unspecified)
                })
                .Concat(
                    from limit in BadPositiveInt32
                    select new GetReplicaStatsOptions
                    {
                        StartTime = SampleTime,
                        Limit = limit
                    });

        // We use 2 data sources for each of overloads of AddReplicaAsync,
        // with region parameter as Region and as string respectively. Same
        // for DropReplicaAsync.

        private static IEnumerable<object[]> AddReplica1NegativeDataSource =>
            (from tableName in BadTableNames
                select new object[]
                {
                    tableName,
                    Region.AP_HYDERABAD_1,
                    null
                })
            .Append(new object[] {Table.Name, null, null })
            .Append(new object[] { Table.Name, null, new AddReplicaOptions
                { Timeout = TimeSpan.FromSeconds(10)} })
            .Concat(
                from opt in BadAddReplicaOpts
                select new object[]
                {
                    Table.Name,
                    Region.EU_FRANKFURT_1,
                    opt
                });

        private static IEnumerable<object[]> AddReplica2NegativeDataSource =>
            (from tableName in BadTableNames
                select new object[]
                {
                    tableName,
                    "us-ashburn-1",
                    null
                })
            .Concat(
                from region in BadRegionIds
                select new object[]
                {
                    Table.Name,
                    region,
                    null
                })
            .Concat(
                from region in BadRegionIds
                select new object[]
                {
                    Table.Name,
                    region,
                    new AddReplicaOptions
                    {
                        WriteUnits = 5,
                        ReadUnits = 5
                    }
                })
            .Concat(
                from opt in BadAddReplicaOpts
                select new object[]
                {
                    Table.Name,
                    "ap-mumbai-1",
                    opt
                });

        private static IEnumerable<object[]> DropReplica1NegativeDataSource =>
            (from tableName in BadTableNames
                select new object[]
                {
                    tableName,
                    Region.AP_HYDERABAD_1,
                    null
                })
            .Append(new object[] { Table.Name, null, null })
            .Append(new object[]
            {
                Table.Name,
                null,
                new DropReplicaOptions
                {
                    PollDelay = TimeSpan.FromSeconds(1)
                }
            })
            .Concat(
                from opt in BadDropReplicaOpts
                select new object[]
                {
                    Table.Name,
                    Region.EU_FRANKFURT_1,
                    opt
                });

        private static IEnumerable<object[]> DropReplica2NegativeDataSource =>
            (from tableName in BadTableNames
                select new object[]
                {
                    tableName,
                    "us-ashburn-1",
                    null
                })
            .Concat(
                from region in BadRegionIds
                select new object[]
                {
                    Table.Name,
                    region,
                    null
                })
            .Concat(
                from region in BadRegionIds
                select new object[]
                {
                    Table.Name,
                    region,
                    new DropReplicaOptions
                    {
                        PollDelay = TimeSpan.FromSeconds(1)
                    }
                })
            .Concat(
                from opt in BadDropReplicaOpts
                select new object[]
                {
                    Table.Name,
                    "ap-mumbai-1",
                    opt
                });

        private static IEnumerable<object[]>
            GetReplicaStats1NegativeDataSource =>
            (from tableName in BadTableNames
                select new object[]
                {
                    tableName,
                    null
                })
            .Concat(
                from opt in BadGetReplicaStatsOpts
                select new object[]
                {
                    Table.Name,
                    opt
                });

        private static IEnumerable<object[]>
            GetReplicaStats2NegativeDataSource =>
            (from tableName in BadTableNames
                select new object[]
                {
                    tableName,
                    Region.US_PHOENIX_1.RegionId,
                    null
                })
            .Concat(
                from region in BadRegionIdsNotNull
                select new object[]
                {
                    Table.Name,
                    region,
                    null
                })
            .Concat(
                from opt in BadGetReplicaStatsOpts
                select new object[]
                {
                    Table.Name,
                    Region.US_PHOENIX_1.RegionId,
                    opt
                });

        private static IEnumerable<object[]>
            ForLocalReplicaInitNegativeDataSource =>
            (from tableName in BadTableNames
                select new object[] { tableName, null })
            .Concat(
                from opt in BadTableCompletionOptions
                select new object[] { Table.Name, opt });

        private static readonly Version GATVersion = new Version("24.2");

        // As of now kv 24.1 doesn't have GAT proxy changes, so not enabling
        // this in default case (without KVVersion defined), this can be
        // changed later.
        private static bool ShouldRunNotSupported => KVVersion >= GATVersion;

        [TestInitialize]
        public void TestInitialize()
        {
            var isNotSupportedTest = TestContext.TestName.Contains(
                "NotSupported");

            if (IsCloud)
            {
                if (isNotSupportedTest)
                {
                    Assert.Inconclusive(
                        "This test does not run with cloud service");
                }

                return;
            }

            if (!isNotSupportedTest)
            {
                Assert.Inconclusive(
                    "This test only runs with cloud service");
            }

            if (!ShouldRunNotSupported)
            {
                Assert.Inconclusive(
                    "This test requires KV version >= " + GATVersion);
            }

        }

        [DataTestMethod]
        [DynamicData(nameof(AddReplica1NegativeDataSource))]
        public async Task TestA_AddReplica1NegativeAsync(string tableName,
            Region region, AddReplicaOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.AddReplicaAsync(tableName, region, options));
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.AddReplicaWithCompletionAsync(tableName, region,
                    options));
        }

        [DataTestMethod]
        [DynamicData(nameof(AddReplica2NegativeDataSource))]
        public async Task TestA_AddReplica2NegativeAsync(string tableName,
            string region, AddReplicaOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.AddReplicaAsync(tableName, region, options));
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.AddReplicaWithCompletionAsync(tableName, region,
                    options));
        }

        [TestMethod]
        public async Task TestA_AddReplicaNonExistentAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.AddReplicaAsync("noSuchTable", Region.US_ASHBURN_1));
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.AddReplicaAsync("noSuchTable", "us-ashburn-1"));
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.AddReplicaWithCompletionAsync("noSuchTable",
                    Region.US_ASHBURN_1));
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.AddReplicaWithCompletionAsync("noSuchTable",
                    "us-ashburn-1"));
        }

        [DataTestMethod]
        [DynamicData(nameof(DropReplica1NegativeDataSource))]
        public async Task TestA_DropReplica1NegativeAsync(string tableName,
            Region region, DropReplicaOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.DropReplicaAsync(tableName, region, options));
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.DropReplicaWithCompletionAsync(tableName, region,
                    options));
        }

        [DataTestMethod]
        [DynamicData(nameof(DropReplica2NegativeDataSource))]
        public async Task TestA_DropReplica2NegativeAsync(string tableName,
            string region, DropReplicaOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.DropReplicaAsync(tableName, region, options));
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.DropReplicaWithCompletionAsync(tableName, region,
                    options));
        }

        [TestMethod]
        public async Task TestA_DropReplicaNonExistentAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.DropReplicaAsync("noSuchTable", Region.US_ASHBURN_1));
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.DropReplicaAsync("noSuchTable", "us-ashburn-1"));
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.DropReplicaWithCompletionAsync("noSuchTable",
                    Region.US_ASHBURN_1));
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.DropReplicaWithCompletionAsync("noSuchTable",
                    "us-ashburn-1"));
        }

        [DataTestMethod]
        [DynamicData(nameof(GetReplicaStats1NegativeDataSource))]
        public async Task TestA_GetReplicaStats1NegativeAsync(string tableName,
            GetReplicaStatsOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.GetReplicaStatsAsync(tableName, options));
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.GetReplicaStatsAsync(tableName, Region.US_PHOENIX_1,
                    options));
        }

        [DataTestMethod]
        [DynamicData(nameof(GetReplicaStats2NegativeDataSource))]
        public async Task TestA_GetReplicaStats2NegativeAsync(string tableName,
            string region, GetReplicaStatsOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.GetReplicaStatsAsync(tableName, region, options));
        }

        [TestMethod]
        public async Task TestA_GetReplicaStatsNonExistentAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.GetReplicaStatsAsync("noSuchTable"));
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.GetReplicaStatsAsync("noSuchTable",
                    Region.AP_HYDERABAD_1));
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.GetReplicaStatsAsync("noSuchTable", "ap-hyderabad-1"));
        }

        [DataTestMethod]
        [DynamicData(nameof(ForLocalReplicaInitNegativeDataSource))]
        public async Task TestA_ForLocalReplicaInitNegativeAsync(string tableName,
            TableCompletionOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.WaitForLocalReplicaInitAsync(tableName, options));
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.WaitForLocalReplicaInitAsync(tableName, options?.Timeout,
                    options?.PollDelay));
        }

        [TestMethod]
        public async Task TestA_ForLocalReplicaInitTimeoutAsync()
        {
            await Assert.ThrowsExceptionAsync<TimeoutException>(() =>
                client.WaitForLocalReplicaInitAsync("noSuchTable",
                    new TableCompletionOptions
                    {
                        Timeout = TimeSpan.FromMilliseconds(500),
                        PollDelay = TimeSpan.FromMilliseconds(200)
                    }));
            await Assert.ThrowsExceptionAsync<TimeoutException>(() =>
                client.WaitForLocalReplicaInitAsync("noSuchTable",
                    TimeSpan.FromMilliseconds(500)));
        }

        [TestMethod]
        public async Task TestAddReplicaNotSupportedAsync()
        {
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() =>
                client.AddReplicaAsync(Table.Name, Region.US_ASHBURN_1));
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() =>
                client.AddReplicaAsync(Table.Name, Region.US_ASHBURN_1,
                    new AddReplicaOptions
                    {
                        WriteUnits = 4
                    }));
        }

        [TestMethod]
        public async Task TestDropReplicaNotSupportedAsync()
        {
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() =>
                client.DropReplicaAsync(Table.Name, Region.US_ASHBURN_1));
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() =>
                client.DropReplicaAsync(Table.Name, Region.US_ASHBURN_1,
                    new DropReplicaOptions
                    {
                        Timeout = TimeSpan.FromMinutes(1)
                    }));
        }

        [TestMethod]
        public async Task TestGetReplicaStatsNotSupportedAsync()
        {
            Func<Task>[] testCases =
            {
                () => client.GetReplicaStatsAsync(Table.Name),
                () =>
                    client.GetReplicaStatsAsync(Table.Name,
                        new GetReplicaStatsOptions
                        {
                            StartTime = DateTime.UtcNow
                        }),
                () =>
                    client.GetReplicaStatsAsync(Table.Name,
                        Region.US_ASHBURN_1),
                () =>
                    client.GetReplicaStatsAsync(Table.Name,
                        Region.US_ASHBURN_1,
                        new GetReplicaStatsOptions
                        {
                            Compartment = Compartment
                        })
            };

            // Currently getReplicaStats returns fake record. This will be
            // changed to throw NotSupportedException, at which time we can
            // update this code.
            foreach (var testCase in testCases)
            {
                try
                {
                    await testCase();
                }
                catch (Exception ex)
                {
                    Assert.IsInstanceOfType(ex,
                        typeof(NotSupportedException));
                }
            }
        }

        [TestMethod]
        public async Task TestWaitForLocalReplicaInitNotSupportedAsync()
        {
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() =>
                client.WaitForLocalReplicaInitAsync(Table.Name));
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() =>
                client.WaitForLocalReplicaInitAsync(Table.Name,
                    TimeSpan.FromMinutes(1), TimeSpan.FromSeconds(1)));
            await Assert.ThrowsExceptionAsync<NotSupportedException>(() =>
                client.WaitForLocalReplicaInitAsync(Table.Name,
                    new TableCompletionOptions
                    {
                        Compartment = Compartment
                    }));
        }

    }

}
