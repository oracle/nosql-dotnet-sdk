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
    using System.Threading.Tasks;
    using System.Linq;
    using System.Threading;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Utils;
    using static NegativeTestData;
    using static TestSchemas;
    using static TestTables;

    [TestClass]
    public class DeleteRangeTests : DataTestBase<DeleteRangeTests>
    {
        private static readonly DataTestFixture Fixture = new DataTestFixture(
            AllTypesTable, new AllTypesRowFactory(), 20);

        private static readonly MapValue GoodPK = MakePrimaryKey(
            Fixture.Table, Fixture.Rows[0]);

        private static readonly MapValue GoodPK2 = MakePrimaryKey(
            Fixture.Table, Fixture.Rows[1]);

        private static readonly MapValue GoodPartialPK = RemoveFieldFromMap(
            GoodPK, Fixture.Table.PrimaryKey[^1]);

        private const int MaxWriteKBLimit = 2 * 1024;

        private static IEnumerable<DeleteRangeOptions>
            BadDeleteRangeOptions =>
            (from timeout in BadTimeSpans select new DeleteRangeOptions
            {
                Timeout = timeout
            })
            .Concat(from durability in BadDurabilities
                select new DeleteRangeOptions
                {
                    Durability = durability
                })
            .Concat(
                from maxWriteKB in BadPositiveInt32 select new DeleteRangeOptions
                {
                    MaxWriteKB = maxWriteKB
                })
            .Append(new DeleteRangeOptions
                {
                    MaxWriteKB = MaxWriteKBLimit + 1
                })
            .Concat(
                from fieldRange in GetBadFieldRanges(Fixture.Table, GoodPK,
                    GoodPK2)
                select new DeleteRangeOptions
                {
                    FieldRange = fieldRange
                });

        private static IEnumerable<object[]>
            DeleteRangeNegativeDataSourceBase =>
            (from tableName in BadTableNames
                select new object[]
                {
                    tableName,
                    GoodPartialPK
                })
            .Concat(
                from pk in GetBadPrimaryKeys(Fixture.Table, GoodPK, true)
                select new object[]
                {
                    Fixture.Table.Name,
                    pk
                });

        private static IEnumerable<object[]>
            DeleteRangeNegativeDataSourceWithOptions =>
            (from ds in DeleteRangeNegativeDataSourceBase select new[]
            {
                ds[0], ds[1], null
            })
            .Concat(
                from opt in BadDeleteRangeOptions
                select new object[]
                {
                    Fixture.Table.Name,
                    GoodPartialPK,
                    opt
                });

        private static IEnumerable<object[]>
            DeleteRangeNegativeDataSourceWithFieldRange =>
            (from ds in DeleteRangeNegativeDataSourceBase
                select new[]
                {
                    ds[0], ds[1], null
                })
            .Concat(
                from fr in GetBadFieldRanges(Fixture.Table, GoodPK, GoodPK2)
                select new object[]
                {
                    Fixture.Table.Name,
                    GoodPartialPK,
                    fr
                });

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

        // Test all 3 overloads of DeleteRangeAsync.

        [DataTestMethod]
        [DynamicData(nameof(DeleteRangeNegativeDataSourceWithOptions))]
        public async Task TestDeleteRangeNegative1Async(string tableName,
            MapValue partialPrimaryKey, DeleteRangeOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.DeleteRangeAsync(tableName, partialPrimaryKey,
                    options));
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteRangeNegativeDataSourceWithFieldRange))]
        public async Task TestDeleteRangeNegative2Async(string tableName,
            MapValue partialPrimaryKey, FieldRange fieldRange)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.DeleteRangeAsync(tableName, partialPrimaryKey,
                    fieldRange));
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteRangeNegativeDataSourceBase))]
        public async Task TestDeleteRangeNegative3Async(string tableName,
            MapValue partialPrimaryKey)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.DeleteRangeAsync(tableName, partialPrimaryKey,
                    CancellationToken.None));
        }

        [TestMethod]
        public async Task TestDeleteRangeNonExistentTableAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.DeleteRangeAsync("noSuchTable", GoodPartialPK));
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.DeleteRangeAsync("noSuchTable", GoodPartialPK,
                    (FieldRange)null));
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.DeleteRangeAsync("noSuchTable", GoodPartialPK,
                    CancellationToken.None));
        }

        // Test all 3 overloads of GetDeleteRangeAsyncEnumerable.


        [DataTestMethod]
        [DynamicData(nameof(DeleteRangeNegativeDataSourceWithOptions))]
        public async Task TestGetDeleteRangeAsyncEnumerable1Async(
            string tableName, MapValue partialPrimaryKey,
            DeleteRangeOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(async () => {
                await foreach (var _ in client.GetDeleteRangeAsyncEnumerable(
                    tableName, partialPrimaryKey, options))
                {
                }
            });
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteRangeNegativeDataSourceWithFieldRange))]
        public async Task TestGetDeleteRangeAsyncEnumerable2Async(
            string tableName, MapValue partialPrimaryKey,
            FieldRange fieldRange)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(async () => {
                await foreach (var _ in client.GetDeleteRangeAsyncEnumerable(
                    tableName, partialPrimaryKey, fieldRange))
                {
                }
            });
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteRangeNegativeDataSourceBase))]
        public async Task TestGetDeleteRangeAsyncEnumerable3Async(
            string tableName, MapValue partialPrimaryKey)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(async () => {
                await foreach (var _ in client.GetDeleteRangeAsyncEnumerable(
                    tableName, partialPrimaryKey, CancellationToken.None))
                {
                }
            });
        }

        [TestMethod]
        public async Task
            TestGetDeleteRangeAsyncEnumerableNonExistentTableAsync()
        {
            await AssertThrowsDerivedAsync<TableNotFoundException>(async () =>
            {
                await foreach (var _ in client.GetDeleteRangeAsyncEnumerable(
                    "noSuchTable", GoodPartialPK))
                {
                }
            });
            await AssertThrowsDerivedAsync<TableNotFoundException>(async () =>
            {
                await foreach (var _ in client.GetDeleteRangeAsyncEnumerable(
                    "noSuchTable", GoodPartialPK, (FieldRange)null))
                {
                }
            });
            await AssertThrowsDerivedAsync<TableNotFoundException>(async () =>
            {
                await foreach (var _ in client.GetDeleteRangeAsyncEnumerable(
                    "noSuchTable", GoodPartialPK, CancellationToken.None))
                {
                }
            });
        }

        public class TestCase
        {
            internal string Description { get; }

            internal MapValue PartialPrimaryKey { get; }

            internal FieldRange FieldRange { get; }

            internal HashSet<int> RowIds { get; }

            internal TestCase(string description, MapValue partialPrimaryKey,
                FieldRange fieldRange, IEnumerable<int> rowIds)
            {
                Description = description ?? "DeleteRange TestCase";
                PartialPrimaryKey = partialPrimaryKey;
                FieldRange = fieldRange;
                RowIds = rowIds != null ? new HashSet<int>(rowIds) : null;
            }

            internal int RowCount => RowIds?.Count ?? 0;

            internal bool HasRowId(int id) =>
                RowIds != null && RowIds.Contains(id);

            public override string ToString() => $"TestCase: {Description}";
        }

        private IEnumerable<int> currentRowIds;

        // There doesn't seem to be a way to access dynamic data in setup and
        // teardown methods, so we have to do this manually.
        private void SetForCleanup(IEnumerable<int> rowIds)
        {
            currentRowIds = rowIds;
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            if (currentRowIds != null)
            {
                foreach (var rowId in currentRowIds)
                {
                    await PutRowAsync(Fixture.Table, Fixture.GetRow(rowId));
                }

                currentRowIds = null;
            }
        }

        private static readonly TestCase[] TestCases =
        {
            new TestCase("All keys in the shard, no field range",
                new MapValue { ["shardId"] = 0 },
                null,
                Enumerable.Range(0, Fixture.RowsPerShard)),
            new TestCase("One key only, no field range", new MapValue
            {
                ["shardId"] = 0,
                ["pkString"] = "id1"
            }, null, Enumerable.Repeat(1, 1)),
            new TestCase("One key only, non-existing", new MapValue
            {
                ["shardId"] = 0,
                ["pkString"] = "no_such_value"
            }, null, null),
            new TestCase("Field range, non-existing",
                new MapValue { ["shardId"] = 0 },
                new FieldRange("pkString")
                {
                    StartsWith = "no_such_value",
                    EndsWith = "no_such_value2"
                }, null),
            new TestCase("Field range, left and right inclusive",
                new MapValue { ["shardId"] = 0 },
                new FieldRange("pkString")
                {
                    StartsWith = "id1",
                    // ReSharper disable once StringLiteralTypo
                    EndsWith = "idididididididid8"
                }, Enumerable.Range(1, 8)),
            new TestCase("Field range, left, right exclusive",
                new MapValue { ["shardId"] = 0 },
                new FieldRange("pkString")
                {
                    StartsAfter = "id1",
                    // ReSharper disable once StringLiteralTypo
                    EndsBefore = "idididididididid8"
                }, Enumerable.Range(2, 6)),
            new TestCase("Field range, left inclusive, right exclusive",
                new MapValue { ["shardId"] = 0 },
                new FieldRange("pkString")
                {
                    StartsWith = "id1",
                    // ReSharper disable once StringLiteralTypo
                    EndsBefore = "idididididididid8"
                }, Enumerable.Range(1, 7)),
            new TestCase("Field range, left exclusive, right inclusive",
                new MapValue { ["shardId"] = 0 },
                new FieldRange("pkString")
                {
                    StartsAfter = "id1",
                    // ReSharper disable once StringLiteralTypo
                    EndsWith = "idididididididid8"
                }, Enumerable.Range(2, 7))
        };

        private int deletedCount;
        private int iterationCount;

        private static void VerifyFieldRange(FieldRange fieldRange)
        {
            if (fieldRange == null)
            {
                return;
            }

            Assert.IsFalse(
                fieldRange.StartsWith is null &&
                fieldRange.StartsAfter is null &&
                fieldRange.EndsWith is null &&
                fieldRange.EndsBefore is null);

            if (!(fieldRange.StartsWith is null))
            {
                Assert.IsNull(fieldRange.StartsAfter);
                Assert.IsTrue(fieldRange.IsStartInclusive);
                Assert.IsTrue(ReferenceEquals(fieldRange.StartsWith,
                    fieldRange.StartValue));
            }
            if (!(fieldRange.StartsAfter is null))
            {
                Assert.IsNull(fieldRange.StartsWith);
                Assert.IsFalse(fieldRange.IsStartInclusive);
                Assert.IsTrue(ReferenceEquals(fieldRange.StartsAfter,
                    fieldRange.StartValue));
            }
            if (!(fieldRange.EndsWith is null))
            {
                Assert.IsNull(fieldRange.EndsBefore);
                Assert.IsTrue(fieldRange.IsEndInclusive);
                Assert.IsTrue(ReferenceEquals(fieldRange.EndsWith,
                    fieldRange.EndValue));
            }
            if (!(fieldRange.EndsBefore is null))
            {
                Assert.IsNull(fieldRange.EndsWith);
                Assert.IsFalse(fieldRange.IsEndInclusive);
                Assert.IsTrue(ReferenceEquals(fieldRange.EndsBefore,
                    fieldRange.EndValue));
            }
        }

        private async Task VerifyDeleteRangeAsync(DeleteRangeResult result,
            TestCase testCase, DeleteRangeOptions options = null)
        {
            Assert.IsNotNull(result);
            Assert.IsTrue(result.DeletedCount >= 0);
            VerifyConsumedCapacity(result.ConsumedCapacity);

            if (!IsOnPrem)
            {
                Assert.IsTrue(result.ConsumedCapacity.ReadKB >=
                              result.DeletedCount);
                Assert.IsTrue(result.ConsumedCapacity.ReadUnits >=
                              result.DeletedCount);
                Assert.IsTrue(result.ConsumedCapacity.WriteKB >=
                              result.DeletedCount);
                Assert.IsTrue(result.ConsumedCapacity.WriteUnits >=
                              result.DeletedCount);
                if (options?.MaxWriteKB != null)
                {
                    // we allow overrun by max of 1 row
                    Assert.IsTrue(result.ConsumedCapacity.WriteKB <=
                                  options.MaxWriteKB + Fixture.MaxRowKB);
                }
            }

            iterationCount++;
            deletedCount += result.DeletedCount;

            if (result.ContinuationKey != null)
            {
                // We delay the verification until the operation is completed.
                return;
            }

            Assert.AreEqual(testCase.RowCount, deletedCount);

            // Normally our rows should be deleted in one iteration unless we
            // reduce the write KB limit explicitly.
            if (options?.MaxWriteKB == null)
            {
                Assert.AreEqual(1, iterationCount);
            }
            else
            {
                Assert.IsTrue(options.MaxWriteKB > 0); // test self-check
                // Here we assume that no more than MaxWriteKB rows may be
                // deleted in one iteration.
                Assert.IsTrue(iterationCount >=
                              (double)deletedCount / options.MaxWriteKB);
            }

            // Verify that all rows in the range have been deleted and that
            // all the other rows are intact.
            foreach (var row in Fixture.Rows)
            {
                var getResult = await client.GetAsync(Fixture.Table.Name,
                    MakePrimaryKey(Fixture.Table, row));
                VerifyGetResult(getResult, Fixture.Table,
                    testCase.HasRowId(row.Id) ? null : row);
            }
        }

        private static IEnumerable<object[]> TestDeleteRangeMinDataSource =>
            from testCase in TestCases
            where testCase.FieldRange == null
            select new object[]
            {
                Fixture.Table.Name,
                testCase
            };

        private static IEnumerable<object[]>
            TestDeleteRangeNoMaxWriteKBDataSource =>
            from testCase in TestCases
            select new object[]
            {
                Fixture.Table.Name,
                testCase
            };

        private static readonly int?[] MaxWriteKBs =
        {
            null,
            Fixture.MaxRowKB + 1,
            3 * Fixture.MaxRowKB
        };

        private static IEnumerable<object[]> TestDeleteRangeFullDataSource =>
            from maxWriteKB in MaxWriteKBs
            from ds in TestDeleteRangeNoMaxWriteKBDataSource
            select new[] { ds[0], ds[1], maxWriteKB };

        [DataTestMethod]
        [DynamicData(nameof(TestDeleteRangeFullDataSource))]
        public async Task TestDeleteRangeIterateAsync(string tableName,
            TestCase testCase, int? maxWriteKB)
        {
            SetForCleanup(testCase.RowIds);
            var options = new DeleteRangeOptions
            {
                FieldRange = testCase.FieldRange,
                MaxWriteKB = maxWriteKB,
                Timeout = TimeSpan.FromSeconds(10),
                Durability = Durability.CommitSync
            };

            do
            {
                var result = await client.DeleteRangeAsync(tableName,
                    testCase.PartialPrimaryKey, options);
                await VerifyDeleteRangeAsync(result, testCase, options);
                options.ContinuationKey = result.ContinuationKey;
            } while (options.ContinuationKey != null);
        }

        [DataTestMethod]
        [DynamicData(nameof(TestDeleteRangeMinDataSource))]
        public async Task TestDeleteRangeOnce1Async(string tableName,
            TestCase testCase)
        {
            SetForCleanup(testCase.RowIds);
            var result = await client.DeleteRangeAsync(tableName,
                testCase.PartialPrimaryKey);
            await VerifyDeleteRangeAsync(result, testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(TestDeleteRangeNoMaxWriteKBDataSource))]
        public async Task TestDeleteRangeOnce2Async(string tableName,
            TestCase testCase)
        {
            SetForCleanup(testCase.RowIds);
            VerifyFieldRange(testCase.FieldRange);
            var result = await client.DeleteRangeAsync(tableName,
                testCase.PartialPrimaryKey, testCase.FieldRange);
            await VerifyDeleteRangeAsync(result, testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(TestDeleteRangeMinDataSource))]
        public async Task TestDeleteRangeOnce3Async(string tableName,
            TestCase testCase)
        {
            SetForCleanup(testCase.RowIds);
            var result = await client.DeleteRangeAsync(tableName,
                testCase.PartialPrimaryKey, CancellationToken.None);
            await VerifyDeleteRangeAsync(result, testCase);
        }

        [DataTestMethod]
        [DynamicData(nameof(TestDeleteRangeFullDataSource))]
        public async Task TestGetDeleteRangeAsyncEnumerable1Async(
            string tableName, TestCase testCase, int? maxWriteKB)
        {
            SetForCleanup(testCase.RowIds);

            DeleteRangeOptions options = null;
            if (testCase.FieldRange != null || maxWriteKB.HasValue)
            {
                options = new DeleteRangeOptions
                {
                    FieldRange = testCase.FieldRange,
                    MaxWriteKB = maxWriteKB,
                    Durability = Durability.CommitSync
                };
                // Provide some variety in options.
                if (!maxWriteKB.HasValue)
                {
                    options.Timeout = TimeSpan.FromMilliseconds(9999);
                }
                else
                {
                    options.Compartment = Compartment;
                }
            }

            var enumerable = client.GetDeleteRangeAsyncEnumerable(tableName,
                testCase.PartialPrimaryKey, options);
            await foreach (var result in enumerable)
            {
                await VerifyDeleteRangeAsync(result, testCase, options);
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(TestDeleteRangeNoMaxWriteKBDataSource))]
        public async Task TestGetDeleteRangeAsyncEnumerable2Async(
            string tableName, TestCase testCase)
        {
            SetForCleanup(testCase.RowIds);
            var enumerable = client.GetDeleteRangeAsyncEnumerable(tableName,
                testCase.PartialPrimaryKey, testCase.FieldRange);
            await foreach (var result in enumerable)
            {
                await VerifyDeleteRangeAsync(result, testCase);
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(TestDeleteRangeMinDataSource))]
        public async Task TestGetDeleteRangeAsyncEnumerable3Async(
            string tableName, TestCase testCase)
        {
            SetForCleanup(testCase.RowIds);
            var enumerable = client.GetDeleteRangeAsyncEnumerable(tableName,
                testCase.PartialPrimaryKey, CancellationToken.None);
            await foreach (var result in enumerable)
            {
                await VerifyDeleteRangeAsync(result, testCase);
            }
        }
    }
}
