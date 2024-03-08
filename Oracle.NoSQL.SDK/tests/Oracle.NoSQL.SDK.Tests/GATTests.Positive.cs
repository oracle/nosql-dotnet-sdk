/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading.Tasks;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    public partial class GlobalActiveTableTests
    {
        public class ReplicaTestData
        {
            private readonly bool useReplicaName;

            internal Region Region { get; set; }

            internal string ReplicaName =>
                useReplicaName ? Region.RegionId : null;

            internal AddReplicaOptions AddOpt { get; }
            internal DropReplicaOptions DropOpt { get; }
            internal bool IsAddWithCompletion { get; }
            internal bool IsDropWithCompletion { get; }
            internal bool ToOnDemand { get; }

            internal ReplicaTestData(
                Region region,
                bool useReplicaName = false,
                AddReplicaOptions addOpt = null,
                DropReplicaOptions dropOpt = null,
                bool isAddWithCompletion = false,
                bool isDropWithCompletion = false,
                bool toOnDemand = false)
            {
                Region = region;
                this.useReplicaName = useReplicaName;

                AddOpt = addOpt;
                DropOpt = dropOpt;
                IsAddWithCompletion = isAddWithCompletion;
                IsDropWithCompletion = isDropWithCompletion;
                ToOnDemand = toOnDemand;
            }

        }

        private const int NumRows = 100;

        private static readonly Region LocalRegion = Region.US_PHOENIX_1;

        private static readonly ReplicaTestData[] Replicas =
        {
            new ReplicaTestData(Region.US_ASHBURN_1),
            new ReplicaTestData(Region.AP_MUMBAI_1, true,
                new AddReplicaOptions
                {
                    Timeout = TimeSpan.FromSeconds(20),
                    WriteUnits = 6
                }, null, false, true),
            new ReplicaTestData(Region.UK_LONDON_1, false,
                new AddReplicaOptions
                {
                    WriteUnits = 4,
                    ReadUnits = 8
                }, new DropReplicaOptions
                {
                    Timeout = TimeSpan.FromSeconds(8)
                }),
            new ReplicaTestData(Region.EU_FRANKFURT_1, true,
                new AddReplicaOptions
                {
                    Compartment = Compartment,
                    PollDelay = TimeSpan.FromMilliseconds(1200)
                }, null, true, false, true),
            new ReplicaTestData(Region.AP_HYDERABAD_1, false, null,
                new DropReplicaOptions
                {
                    Compartment = Compartment,
                    Timeout = TimeSpan.FromMinutes(4),
                    PollDelay = TimeSpan.FromSeconds(2)
                }, true, true, true)
        };

        private static readonly Dictionary<string, NoSQLClient> Clients =
            new Dictionary<string, NoSQLClient>();

        private static bool noInit;
        private static bool noCleanup;

        private static IEnumerable<object[]> AddReplicaDataSource =>
            from idx in Enumerable.Range(0, Replicas.Length)
            select new object[] { idx };

        private static IEnumerable<object[]> DropReplicaDataSource =>
            AddReplicaDataSource.Reverse();

        private static readonly DateTime StatsStartTime =
            DateTime.UtcNow - TimeSpan.FromMinutes(5);
        private static readonly DateTime EmptyStatsStartTime =
            StatsStartTime + TimeSpan.FromHours(1);

        private static readonly GetReplicaStatsOptions[] ReplicaStatsOpts =
        {
            null,
            new GetReplicaStatsOptions
            {
                Timeout = TimeSpan.FromSeconds(10),
                Compartment = Compartment
            },
            new GetReplicaStatsOptions
            {
                StartTime = StatsStartTime
            },
            new GetReplicaStatsOptions
            {
                StartTime = EmptyStatsStartTime
            },
            new GetReplicaStatsOptions
            {
                Limit = 2
            },
            new GetReplicaStatsOptions
            {
                StartTime = StatsStartTime,
                Limit = 2
            }
        };

        private static IEnumerable<object[]> GetReplicaStatsDataSource =>
            from opt in ReplicaStatsOpts
            select new object[]
                { opt, opt != null && opt.StartTime == EmptyStatsStartTime };

        // Use for testing overloads with nulls, note that the 1st element contains
        // null GetReplicaStatsOptions.
        private static IEnumerable<object[]> GetReplicaStatsShortDataSource =>
            GetReplicaStatsDataSource.Take(2);

        private static IReadOnlyList<ReplicaInfo> testReplicaInfos;

        private static NoSQLConfig GetConfigForRegion(Region region)
        {
            var result = CopyConfig();
            result.Region = region;
            return result;
        }

        private static NoSQLConfig GetConfigForRegion(string regionId) =>
            GetConfigForRegion(Region.FromRegionId(regionId));

        private static void VerifyReplicaInfo(ReplicaInfo info,
            ReplicaTestData data, bool isOnDemand = false)
        {
            Assert.IsNotNull(info);
            Assert.AreEqual(data.Region, info.Region);
            var region = Region.FromRegionId(info.ReplicaName);
            Assert.IsNotNull(region);
            Assert.AreEqual(data.Region, region);
            
            Assert.IsNotNull(info.ReplicaOCID);

            // Whether the mode was changed to ON_DEMAND after replica
            // creation.
            Assert.AreEqual(
                isOnDemand ? CapacityMode.OnDemand : CapacityMode.Provisioned,
                info.CapacityMode);

            if (isOnDemand)
            {
                Assert.IsTrue(info.WriteUnits > 0);
            }
            else
            {
                // Whether specified different read/write units when creating
                // replica.
                Assert.AreEqual(data.AddOpt?.WriteUnits.HasValue ?? false
                    ? data.AddOpt.WriteUnits
                    : Table.TableLimits.WriteUnits,
                    info.WriteUnits);
            }

            Assert.AreEqual(TableState.Active, info.TableState);
        }

        private static void VerifyActiveReplica(TableResult result,
            int? repCount = null, TableLimits tableLimits = null,
            string tableOCID = null)
        {
            VerifyActiveTable(result, Table, tableLimits);

            Assert.AreEqual(true, result.IsSchemaFrozen);
            Assert.AreEqual(true, result.IsReplicated);
            Assert.AreEqual(true, result.IsLocalReplicaInitialized);
            Assert.IsNotNull(result.Replicas);
            Assert.IsTrue(result.Replicas.Count > 0);

            if (repCount.HasValue)
            {
                Assert.AreEqual(repCount, result.Replicas.Count);
            }

            if (tableOCID != null)
            {
                Assert.AreEqual(tableOCID, result.TableOCID);
            }
        }

        private static void VerifyReplicaStatsResult(
            ReplicaStatsResult result, string regionId = null,
            GetReplicaStatsOptions options = null,
            bool expectEmptyResult = false)
        {
            Assert.IsNotNull(result);
            Assert.AreEqual(Table.Name, result.TableName);
            Assert.IsNotNull(result.StatsRecords);

            if (expectEmptyResult)
            {
                Assert.IsTrue(result.StatsRecords.Count == 0);
                return;
            }

            // only check approximate bounds to verify the protocol
            Assert.IsTrue(result.NextStartTime <=
                          DateTime.UtcNow + TimeSpan.FromSeconds(61));
            Assert.IsTrue(result.NextStartTime >
                          DateTime.UtcNow - TimeSpan.FromMinutes(10));

            if (regionId != null)
            {
                // This is only true if some time elapsed after the replica
                // was added, don't use for last elements of Replicas array.
                Assert.IsTrue(result.StatsRecords.Count == 1);
                Assert.IsTrue(result.StatsRecords.ContainsKey(regionId));
            }
            else
            {
                // It is possible that the replicas added last might not yet
                // have any stats entries, or when limit is specified, newer
                // replicas may not have entries for same times as older
                // replicas. For simplicity, we only tests that entries for
                // some regions should be present.
                Assert.IsTrue(result.StatsRecords.Count > 0);
                Assert.IsTrue(result.StatsRecords.Keys.All(key =>
                    Replicas.Any(data => data.Region.RegionId == key)));
            }

            foreach (var kv in result.StatsRecords)
            {
                Assert.IsTrue(kv.Value.Count > 0);
                if (options?.Limit.HasValue ?? false)
                {
                    Assert.IsTrue(kv.Value.Count <= options.Limit);
                }

                var collectionTime = DateTime.MinValue;
                foreach (var record in kv.Value)
                {
                    Assert.IsNotNull(record);
                    if (options?.StartTime.HasValue ?? false)
                    {
                        Assert.IsTrue(record.CollectionTime >= options.StartTime);
                    }
                    Assert.IsTrue(record.CollectionTime >= collectionTime);
                    collectionTime = record.CollectionTime;

                    if (record.ReplicaLag != ReplicaStatsRecord.UnknownLag)
                    {
                        Assert.IsTrue(record.ReplicaLag >= TimeSpan.Zero);
                        // Hopefully replica lag will not exceed this value.
                        Assert.IsTrue(record.ReplicaLag < TimeSpan.FromHours(1));
                    }
                }

            }
        }

        private static async Task PrepareTestReplicaInfoAsync()
        {
            TableResult result;

            // Convert some replicas to on-demand capacity, which should
            // not affect the original table. This is needed for replica
            // info test.
            foreach (var data in Replicas)
            {
                if (data.ToOnDemand)
                {
                    var repClient = Clients[data.Region.RegionId];
                    var tableLimits = new TableLimits(
                        Table.TableLimits.StorageGB);
                    result =
                        await repClient.SetTableLimitsWithCompletionAsync(
                            Table.Name, tableLimits);
                    VerifyActiveTable(result, Table, tableLimits);
                }
            }

            result = await client.GetTableAsync(Table.Name);
            VerifyActiveReplica(result, Replicas.Length);
            testReplicaInfos = result.Replicas;
        }

        private static async Task DropAllReplicasAsync()
        {
            TableResult result;
            try
            {
                result = await client.GetTableAsync(Table.Name);
                if (result.TableState != TableState.Active)
                {
                    result = await client.WaitForTableStateAsync(Table.Name,
                        TableState.Active, new TableCompletionOptions
                        {
                            Timeout = TimeSpan.FromSeconds(30)
                        });
                }
            }
            catch (TableNotFoundException)
            {
                return;
            }

            Assert.IsNotNull(result);
            Assert.IsTrue(result.TableState == TableState.Active);

            if (result.Replicas == null)
            {
                return;
            }

            // Dropping any replicas is not allowed if initialization for one
            // of the replicas is not complete. Here we ensure initialization
            // process is complete for all replicas before dropping them. Note
            // that this is executed only if some replicas are leftover from
            // previous run.
            foreach (var repInfo in result.Replicas)
            {
                using var repClient = new NoSQLClient(
                    GetConfigForRegion(repInfo.ReplicaName));
                await repClient.WaitForLocalReplicaInitAsync(Table.Name);
            }

            foreach (var repInfo in result.Replicas)
            {
                await client.DropReplicaWithCompletionAsync(Table.Name,
                    repInfo.ReplicaName);
            }
        }

        private static async Task AddRowsAsync()
        {
            for (var i = 0; i < NumRows; i++)
            {
                var result = await client.PutAsync(Table.Name, new MapValue
                {
                    ["id"] = i,
                    ["lastName"] = "Last Name",
                    ["firstName"] = "First Name",
                    ["info"] = new MapValue
                    {
                        ["fld"] = "Some Field"
                    },
                    ["startDate"] = DateTime.UtcNow
                });

                Assert.IsTrue(result.Success);
            }
        }

        private static TableLimits GetReplicaTableLimits(AddReplicaOptions options,
            bool isOnDemand = false) =>
            isOnDemand
                ? new TableLimits(Table.TableLimits.StorageGB)
                : new TableLimits(
                    options?.ReadUnits.HasValue ?? false
                        ? options.ReadUnits.Value
                        : Table.TableLimits.ReadUnits,
                    options?.WriteUnits.HasValue ?? false
                        ? options.WriteUnits.Value
                        : Table.TableLimits.WriteUnits,
                    Table.TableLimits.StorageGB);

        [ClassInitialize]
        public static async Task ClassInitializeAsync(TestContext testContext)
        {
            ClassInitialize(testContext);

            if (!IsCloud)
            {
                if (ShouldRunNotSupported)
                {
                    await CreateTableAsync(Table);
                }

                return;
            }

            Assert.IsNotNull(config.Region,
                "The configuration for this test must use Region property");

            noInit = Utils.GetBooleanProperty(testContext, "GATTests.noInit");
            noCleanup = Utils.GetBooleanProperty(testContext,
                "GATTests.noCleanup");

            if (!noInit)
            {
                await DropAllReplicasAsync();
                await DropTableAsync(Table);
                await CreateTableAsync(Table, null, true);
                await AddRowsAsync();
            }

            // In case test config specifies the region used in one of our
            // replicas, switch the region for that replica.
            if (Replicas.FirstOrDefault(
                    data => data.Region == config.Region) is { } repData)
            {
                repData.Region = LocalRegion;
            }

            foreach (var data in Replicas)
            {
                Clients[data.Region.RegionId] =
                    new NoSQLClient(GetConfigForRegion(data.Region));
            }
        }

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            try
            {
                if (!noCleanup)
                {
                    await DropAllReplicasAsync();
                    await DropTableAsync(Table);
                }
            }
            finally
            {
                foreach (var repClient in Clients.Values)
                {
                    repClient.Dispose();
                }
            }

            ClassCleanup();
        }

        // Normally, the unit tests should not depend on the order of
        // execution. However, because these operations are executed by the
        // Cloud Service, to minimize resources and time required, we enforce
        // an order of execution below, such that addReplica tests are
        // executed first, dropReplica tests last, and all other tests in
        // between (so that they can assume the replicas already exist).
        // In MSTest, we have to order the tests alphabetically for this, thus
        // TestA, TestB, TestC, etc. below.

        [DataTestMethod]
        [DynamicData(nameof(AddReplicaDataSource))]
        public async Task TestB_AddReplicaAsync(int replicaIdx)
        {
            var data = Replicas[replicaIdx];
            
            var result = data.ReplicaName != null
                ? data.IsAddWithCompletion
                    ? await client.AddReplicaWithCompletionAsync(Table.Name,
                        data.ReplicaName, data.AddOpt)
                    : await client.AddReplicaAsync(Table.Name,
                        data.ReplicaName, data.AddOpt)
                : data.IsAddWithCompletion
                    ? await client.AddReplicaWithCompletionAsync(Table.Name,
                        data.Region, data.AddOpt)
                    : await client.AddReplicaAsync(Table.Name, data.Region,
                        data.AddOpt);

            if (!data.IsAddWithCompletion)
            {
                VerifyTableResult(result, Table);
                await result.WaitForCompletionAsync();
            }

            VerifyActiveReplica(result, replicaIdx + 1);
            var repInfo = result.Replicas.FirstOrDefault(repInfo =>
                repInfo.ReplicaName == data.Region.RegionId);
            VerifyReplicaInfo(repInfo, data);

            var repClient = Clients[data.Region.RegionId];

            // this is somewhat tenuous
            try
            {
                await repClient.GetAsync(Table.Name,
                    new MapValue { ["id"] = 0 },
                    new GetOptions { Timeout = TimeSpan.FromSeconds(3) });
                Assert.Fail(
                    "GetAsync should fail when replica is not initialized");
            }
            catch (TimeoutException ex)
            {
                // TimeoutException is possible if the request was retried on
                // TableNotReadyException (perhaps multiple times) and the
                // remaining timeout was too short for the HTTP request. It
                // would be better to check the exceptions during retries. For
                // this we will need to add the request information to
                // TimeoutException (using Exception.Data dictionary), to be
                // done.
                Assert.IsTrue(ex.InnerException is TableNotReadyException ||
                              ex.InnerException is TimeoutException);
            }

            result = await repClient.WaitForLocalReplicaInitAsync(Table.Name);
            // We haven't done onDemand conversion yet, so data.ToOnDemand is
            // not passed to GetReplicaTableLimits.
            VerifyActiveReplica(result, replicaIdx + 1,
                GetReplicaTableLimits(data.AddOpt));

            // Verify that can read data after replica table is initialized.
            var getResult = await repClient.GetAsync(Table.Name,
                new MapValue { ["id"] = NumRows - 1 });
            Assert.IsNotNull(getResult);
            Assert.IsNotNull(getResult.Row);
        }

        [DataTestMethod]
        [DynamicData(nameof(DropReplicaDataSource))]
        public async Task TestZ_DropReplicaAsync(int replicaIdx)
        {
            var data = Replicas[replicaIdx];

            var result = data.ReplicaName != null
                ? data.IsDropWithCompletion
                    ? await client.DropReplicaWithCompletionAsync(Table.Name,
                        data.ReplicaName, data.DropOpt)
                    : await client.DropReplicaAsync(Table.Name,
                        data.ReplicaName, data.DropOpt)
                : data.IsDropWithCompletion
                    ? await client.DropReplicaWithCompletionAsync(Table.Name,
                        data.Region, data.DropOpt)
                    : await client.DropReplicaAsync(Table.Name, data.Region,
                        data.DropOpt);

            if (!data.IsDropWithCompletion)
            {
                VerifyTableResult(result, Table);
                await result.WaitForCompletionAsync();
            }

            if (replicaIdx == 0) // we dropped last replica
            {
                VerifyActiveTable(result, Table);
                Assert.IsTrue(result.IsSchemaFrozen);
                Assert.IsFalse(result.IsReplicated);
                Assert.IsFalse(result.IsLocalReplicaInitialized);
                Assert.IsNull(result.Replicas);
            }
            else
            {
                VerifyActiveReplica(result, replicaIdx);
                var repInfo = result.Replicas.FirstOrDefault(repInfo =>
                    repInfo.ReplicaName == data.Region.RegionId);
                Assert.IsNull(repInfo);
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(AddReplicaDataSource))]
        public async Task TestC_ReplicaInfoAsync(int replicaIdx)
        {
            // Because MSTest does not support nested suites and this has to
            // run after add replica tests, we have to do preparation during
            // test time.
            if (testReplicaInfos == null)
            {
                await PrepareTestReplicaInfoAsync();
                Assert.IsNotNull(testReplicaInfos);
            }

            var data = Replicas[replicaIdx];
            var repInfo = testReplicaInfos.FirstOrDefault(
                info => info.ReplicaName == data.Region.RegionId);
            // Selected replicas should already be converted to on-demand.
            VerifyReplicaInfo(repInfo, data, data.ToOnDemand);
            Debug.Assert(repInfo != null);

            // All replicas should already be initialized.
            var repClient = Clients[data.Region.RegionId];
            var result = await repClient.GetTableAsync(Table.Name);
            VerifyActiveReplica(result, Replicas.Length,
                GetReplicaTableLimits(data.AddOpt, data.ToOnDemand),
                repInfo.ReplicaOCID);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetReplicaStatsDataSource))]
        public async Task TestD_GetReplicaStatsAsync(
            GetReplicaStatsOptions options, bool expectEmptyResult)
        {
            var result = options == null
                ? await client.GetReplicaStatsAsync(Table.Name)
                : await client.GetReplicaStatsAsync(Table.Name, options);
            VerifyReplicaStatsResult(result, null, options,
                expectEmptyResult);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetReplicaStatsShortDataSource))]
        public async Task TestD_GetReplicaStatsWithNullRegionAsync(
            GetReplicaStatsOptions options, bool expectEmptyResult)
        {
            var result = options == null
                ? await client.GetReplicaStatsAsync(Table.Name, (Region)null)
                : await client.GetReplicaStatsAsync(Table.Name, (Region)null, options);
            VerifyReplicaStatsResult(result, null, options,
                expectEmptyResult);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetReplicaStatsDataSource))]
        public async Task TestD_GetReplicaStatsWithRegionAsync(
            GetReplicaStatsOptions options, bool expectEmptyResult)
        {
            var region = Replicas[0].Region;
            var result = options == null
                ? await client.GetReplicaStatsAsync(Table.Name, region)
                : await client.GetReplicaStatsAsync(Table.Name, region,
                    options);
            VerifyReplicaStatsResult(result, region.RegionId, options,
                expectEmptyResult);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetReplicaStatsShortDataSource))]
        public async Task TestD_GetReplicaStatsWithNullRegionIdAsync(
            GetReplicaStatsOptions options, bool expectEmptyResult)
        {
            var result = options == null
                ? await client.GetReplicaStatsAsync(Table.Name, (string)null)
                : await client.GetReplicaStatsAsync(Table.Name, (string)null, options);
            VerifyReplicaStatsResult(result, null, options,
                expectEmptyResult);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetReplicaStatsDataSource))]
        public async Task TestD_GetReplicaStatsWithRegionIdAsync(
            GetReplicaStatsOptions options, bool expectEmptyResult)
        {
            var regionId = Replicas[1].Region.RegionId;
            var result = options == null
                ? await client.GetReplicaStatsAsync(Table.Name, regionId)
                : await client.GetReplicaStatsAsync(Table.Name, regionId,
                    options);
            VerifyReplicaStatsResult(result, regionId, options,
                expectEmptyResult);
        }

        // Limited positive test (we already invoke this API in other tests).
        [TestMethod]
        public async Task TestE_ForLocalReplicaInitAsync()
        {
            var result = await client.WaitForLocalReplicaInitAsync(Table.Name,
                TimeSpan.FromSeconds(8), TimeSpan.FromMilliseconds(700));
            VerifyActiveTable(result, Table);
            result = await client.WaitForLocalReplicaInitAsync(Table.Name,
                new TableCompletionOptions
                {
                    Timeout = TimeSpan.FromSeconds(8),
                    PollDelay = TimeSpan.FromMilliseconds(500),
                    Compartment = Compartment
                });
            VerifyActiveTable(result, Table);
        }

    }
}
