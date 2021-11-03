/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
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
    using System.Runtime.InteropServices.ComTypes;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Utils;
    using static NegativeTestData;
    using static TestSchemas;
    using static TestTables;

    [TestClass]
    public class GetTableUsageTests : TablesTestBase<GetTableUsageTests>
    {
        private static readonly TableInfo Table = SimpleTable;

        private static readonly DateTime SampleTime = DateTime.UtcNow;

        private static readonly IEnumerable<GetTableUsageOptions>
            BadGetTableUsageOptions =
                (from timeout in BadTimeSpans
                    select new GetTableUsageOptions
                    {
                        Timeout = timeout
                    })
                // StartTime and EndTime may not have kind Unspecified
                .Append(new GetTableUsageOptions
                {
                    StartTime = DateTime.SpecifyKind(SampleTime,
                        DateTimeKind.Unspecified)
                })
                .Append(new GetTableUsageOptions
                {
                    EndTime = DateTime.SpecifyKind(SampleTime,
                        DateTimeKind.Unspecified)
                })
                // StartTime and EndTime must be of the same kind
                .Append(new GetTableUsageOptions
                {
                    StartTime = SampleTime,
                    EndTime = SampleTime.ToLocalTime()
                })
                // StartTime cannot be greater than EndTime
                .Append(new GetTableUsageOptions
                {
                    StartTime = SampleTime,
                    EndTime = SampleTime - TimeSpan.FromDays(1)
                })
                // May not specify limit without the time range
                .Append(new GetTableUsageOptions
                {
                    Limit = 10
                })
                .Concat(
                    from limit in BadPositiveInt32
                    select new GetTableUsageOptions
                    {
                        StartTime = SampleTime,
                        EndTime = SampleTime + TimeSpan.FromDays(1),
                        Limit = limit
                    });

        private static IEnumerable<object[]>
            GetTableUsageNegativeDataSource =>
            (from tableName in BadTableNames
                select new object[]
                {
                    tableName,
                    null
                })
            .Concat(
                from opt in BadGetTableUsageOptions
                select new object[]
                {
                    Table.Name,
                    opt
                });

        // stored as kind Utc
        private static DateTime startTime;
        private static DateTime endTime;

        private static readonly MapValue SampleRow = new MapValue
        {
            ["id"] = 1,
            ["lastName"] = "Smith",
            ["firstName"] = "John",
            ["info"] = new MapValue
            {
                ["blah"] = "blah"
            },
            ["startDate"] = SampleTime
        };

        private static readonly MapValue SamplePrimaryKey = new MapValue
        {
            ["id"] = 1
        };

        [ClassInitialize]
        public static async Task ClassInitializeAsync(TestContext testContext)
        {
            ClassInitialize(testContext);
            await CreateTableAsync(Table);

            startTime = DateTime.UtcNow;

            if (IsOnPrem || IsCloudSim)
            {
                endTime = startTime + TimeSpan.FromSeconds(2);
            }
            else
            {
                for (var i = 0; i < 60; i++)
                {
                    await client.PutAsync(Table.Name, SampleRow);
                    await client.GetAsync(Table.Name, SamplePrimaryKey);
                    await Task.Delay(1000);
                }

                await Task.Delay(60000);
                endTime = DateTime.UtcNow;
            }
        }

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            await DropTableAsync(Table);
            ClassCleanup();
        }

        [DataTestMethod]
        [DynamicData(nameof(GetTableUsageNegativeDataSource))]
        public async Task TestGetTableUsageNegativeAsync(string tableName,
            GetTableUsageOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.GetTableUsageAsync(tableName, options));
        }

        [TestMethod]
        public async Task TestGetTableUsageNonExistentAsync()
        {
            if (IsOnPrem || IsCloudSim)
            {
                Assert.Inconclusive(
                    "This test is not run with on-prem kvstore or cloudsim");
            }
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.GetTableUsageAsync("noSuchTable"));
        }

        private static void VerifyTableUsageResult(TableUsageResult result,
            TableInfo table, GetTableUsageOptions options)
        {
            Assert.IsNotNull(result);
            Assert.AreEqual(table.Name, result.TableName);
            Assert.IsNotNull(result.UsageRecords);

            if (!IsCloudSim)
            {
                if (options != null &&
                    (options.StartTime.HasValue || options.EndTime.HasValue ||
                     options.Limit > 1))
                {
                    Assert.IsTrue(result.UsageRecords.Count > 1);
                }
                else
                {
                    Assert.AreEqual(1, result.UsageRecords.Count);
                }
            }

            var prevStartTime = DateTime.MinValue;
            var duration = TimeSpan.Zero;

            foreach(var record in result.UsageRecords)
            {
                Assert.IsNotNull(record);

                Assert.AreEqual(DateTimeKind.Utc, record.StartTime.Kind);
                Assert.IsTrue(record.StartTime > prevStartTime);
                prevStartTime = record.StartTime;

                if (duration == TimeSpan.Zero)
                {
                    duration = record.Duration;
                }
                else
                {
                    // all durations should be the same
                    Assert.AreEqual(duration, record.Duration);
                }

                Assert.IsTrue(record.ReadUnits >= 0);
                Assert.IsTrue(record.WriteUnits >= 0);
                Assert.IsTrue(record.StorageGB >= 0);
                Assert.AreEqual(0, record.ReadThrottleCount);
                Assert.AreEqual(0, record.WriteThrottleCount);
                Assert.AreEqual(0, record.StorageThrottleCount);
            }
        }

        private static IEnumerable<GetTableUsageOptions> GetOptions()
        {
            yield return null;
            yield return new GetTableUsageOptions();
            yield return new GetTableUsageOptions
            {
                Timeout = TimeSpan.FromMilliseconds(10002)
            };
            yield return new GetTableUsageOptions
            {
                StartTime = startTime,
                EndTime = endTime
            };
            yield return new GetTableUsageOptions
            {
                Timeout = TimeSpan.FromSeconds(8),
                StartTime = startTime,
                EndTime = endTime
            };
            yield return new GetTableUsageOptions
            {
                StartTime = startTime,
                EndTime = endTime,
                Limit = 3
            };
            yield return new GetTableUsageOptions
            {
                StartTime = startTime,
                Timeout = TimeSpan.FromSeconds(10)
            };
            yield return new GetTableUsageOptions
            {
                EndTime = endTime,
                Limit = 10
            };
        }

        private static IEnumerable<object[]> GetTableUsageDataSource =>
            from options in GetOptions() select new object[] {options};

        [DataTestMethod]
        [DynamicData(nameof(GetTableUsageDataSource))]
        public async Task TestGetTableUsageAsync(GetTableUsageOptions options)
        {
            if (IsOnPrem)
            {
                await Assert.ThrowsExceptionAsync<NotSupportedException>(() =>
                    client.GetTableUsageAsync(Table.Name, options));
                return;
            }

            var result = await client.GetTableUsageAsync(Table.Name, options);
            VerifyTableUsageResult(result, Table, options);
        }
    }

}
