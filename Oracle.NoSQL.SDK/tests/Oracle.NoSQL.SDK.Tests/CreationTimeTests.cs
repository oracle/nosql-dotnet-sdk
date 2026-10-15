/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static TestSchemas;
    using static TestTables;
    using static Utils;

    [TestClass]
    public class CreationTimeTests : DataTestBase<CreationTimeTests>
    {
        private const int ShardId = 1;

        private static readonly Version CreationTimeVersion =
            new Version("25.3");

        private static readonly TableInfo Table = new TableInfo(
            TableNamePrefix + "CreationTime" +
            Guid.NewGuid().ToString("N").Substring(0, 8),
            DefaultTableLimits,
            new[]
            {
                new TableField("sid", DataType.Integer),
                new TableField("id", DataType.Integer),
                new TableField("name", DataType.String)
            },
            new[] { "sid", "id" },
            1);

        [ClassInitialize]
        public static void InitializeClass(TestContext testContext)
        {
            ClassInitialize(testContext);
        }

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            await DropTableAsync(Table);
            ClassCleanup();
        }

        [TestInitialize]
        public async Task TestInitializeAsync()
        {
            CheckCreationTimeSupport();
            await DropTableAsync(Table);
            await CreateTableAsync(Table);
        }

        [TestMethod]
        public async Task TestGetCreationTimeIsServerGeneratedAndImmutableAsync()
        {
            await client.PutAsync(Table.Name, CreateRow(0));
            var initialResult = await GetRowAsync(0);
            var creationTime = GetRequiredCreationTime(initialResult);

            Assert.AreEqual(DateTimeKind.Utc, creationTime.Kind);
            Assert.IsNotNull(initialResult.ModificationTime);
            Assert.IsTrue(creationTime <= initialResult.ModificationTime);

            var putResult = await client.PutAsync(Table.Name,
                CreateRow(0, "updated"), new PutOptions
                {
                    ReturnExisting = true
                });

            Assert.IsTrue(putResult.Success);
            Assert.AreEqual(creationTime, putResult.ExistingCreationTime);

            var updatedResult = await GetRowAsync(0);
            Assert.AreEqual(creationTime, updatedResult.CreationTime);
            Assert.IsNotNull(updatedResult.ModificationTime);
            Assert.IsTrue(creationTime <= updatedResult.ModificationTime);

            var missingResult = await GetRowAsync(100);
            Assert.IsNull(missingResult.Row);
            Assert.IsNull(missingResult.CreationTime);
        }

        [TestMethod]
        public async Task TestPutAndDeleteReturnExistingCreationTimeAsync()
        {
            await client.PutAsync(Table.Name, CreateRow(0));
            var creationTime = GetRequiredCreationTime(await GetRowAsync(0));

            var conditionalPutResult = await client.PutIfAbsentAsync(
                Table.Name, CreateRow(0, "ignored"), new PutOptions
                {
                    ReturnExisting = true
                });

            Assert.IsFalse(conditionalPutResult.Success);
            Assert.AreEqual(creationTime,
                conditionalPutResult.ExistingCreationTime);

            var deleteResult = await client.DeleteAsync(Table.Name,
                CreateKey(0), new DeleteOptions
                {
                    ReturnExisting = true
                });

            Assert.IsTrue(deleteResult.Success);
            Assert.AreEqual(creationTime, deleteResult.ExistingCreationTime);

            var newRowResult = await client.PutAsync(Table.Name, CreateRow(1),
                new PutOptions
                {
                    ReturnExisting = true
                });
            Assert.IsTrue(newRowResult.Success);
            Assert.IsNull(newRowResult.ExistingCreationTime);
        }

        [TestMethod]
        public async Task TestWriteManyReturnsExistingCreationTimeAsync()
        {
            await client.PutAsync(Table.Name, CreateRow(0));
            await client.PutAsync(Table.Name, CreateRow(1));
            var firstCreationTime = GetRequiredCreationTime(
                await GetRowAsync(0));
            var secondCreationTime = GetRequiredCreationTime(
                await GetRowAsync(1));

            var result = await client.WriteManyAsync(Table.Name,
                new WriteOperationCollection()
                    .AddPutIfPresent(CreateRow(0, "updated"),
                        new PutOptions { ReturnExisting = true })
                    .AddDelete(CreateKey(1),
                        new DeleteOptions { ReturnExisting = true }));

            Assert.IsTrue(result.Success);
            Assert.AreEqual(2, result.Results.Count);
            Assert.AreEqual(firstCreationTime,
                result.Results[0].ExistingCreationTime);
            Assert.AreEqual(secondCreationTime,
                result.Results[1].ExistingCreationTime);
        }

        public static IEnumerable<object[]> ConditionalBatchCases
        {
            get
            {
                foreach (var operation in new[]
                         { "PutIfAbsent", "PutIfVersion", "DeleteIfVersion" })
                {
                    // Exercise each abort setting independently, plus the
                    // non-aborting path that returns individual results.
                    foreach (var abortMode in new[] { "operation", "batch", "none" })
                    {
                        foreach (var returnExisting in new[] { true, false })
                        {
                            yield return new object[]
                                { operation, abortMode, returnExisting };
                        }
                    }
                }
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(ConditionalBatchCases))]
        public async Task TestConditionalBatchCreationTimeAsync(
            string operation, string abortMode, bool returnExisting)
        {
            await client.PutAsync(Table.Name, CreateRow(0));
            var firstRow = await GetRowAsync(0);
            var firstCreationTime = GetRequiredCreationTime(firstRow);

            var inserted = await client.PutAsync(Table.Name, CreateRow(1));
            // Obtain a real stale version, rather than constructing arbitrary
            // version bytes that the server might reject as malformed.
            await client.PutAsync(Table.Name, CreateRow(1, "current"));
            var existing = await GetRowAsync(1);
            var creationTime = GetRequiredCreationTime(existing);
            AssertNotDeepEqual(inserted.Version, existing.Version, true);

            var operationAbort = abortMode == "operation";
            var batchAbort = abortMode == "batch";
            var shouldAbort = operationAbort || batchAbort;
            var putOptions = new PutOptions { ReturnExisting = returnExisting };
            var operations = new WriteOperationCollection()
                .AddPutIfPresent(CreateRow(0, "batch-update"));

            // Only index 1 can fail. Distinct keys avoid depending on the
            // server's execution order within the atomic batch.
            switch (operation)
            {
                case "PutIfAbsent":
                    operations.AddPutIfAbsent(CreateRow(1, "ignored"),
                        putOptions, operationAbort);
                    break;
                case "PutIfVersion":
                    operations.AddPutIfVersion(CreateRow(1, "ignored"),
                        inserted.Version, putOptions, operationAbort);
                    break;
                case "DeleteIfVersion":
                    operations.AddDeleteIfVersion(CreateKey(1),
                        inserted.Version,
                        new DeleteOptions { ReturnExisting = returnExisting },
                        operationAbort);
                    break;
                default:
                    throw new ArgumentException(nameof(operation));
            }

            operations.AddPut(CreateRow(2));
            var result = await client.WriteManyAsync(Table.Name, operations,
                new WriteManyOptions { AbortIfUnsuccessful = batchAbort });

            WriteOperationResult<RecordValue> failed;
            if (shouldAbort)
            {
                Assert.IsFalse(result.Success);
                Assert.AreEqual(1, result.FailedOperationIndex);
                Assert.IsNull(result.Results);
                failed = result.FailedOperationResult;
            }
            else
            {
                Assert.IsTrue(result.Success);
                Assert.IsNull(result.FailedOperationIndex);
                Assert.IsNull(result.FailedOperationResult);
                Assert.AreEqual(3, result.Results.Count);
                Assert.IsTrue(result.Results[0].Success);
                Assert.IsTrue(result.Results[2].Success);
                failed = result.Results[1];
            }

            Assert.IsNotNull(failed);
            Assert.IsFalse(failed.Success);
            Assert.IsNull(failed.Version);
            if (returnExisting)
            {
                Assert.AreEqual(creationTime, failed.ExistingCreationTime);
                Assert.AreEqual(DateTimeKind.Utc,
                    failed.ExistingCreationTime.Value.Kind);
                Assert.AreEqual(existing.Row, failed.ExistingRow);
                AssertDeepEqual(existing.Version, failed.ExistingVersion, true);
            }
            else
            {
                Assert.IsNull(failed.ExistingCreationTime);
                Assert.IsNull(failed.ExistingRow);
                Assert.IsNull(failed.ExistingVersion);
            }

            var firstAfter = await GetRowAsync(0);
            Assert.AreEqual(firstCreationTime, firstAfter.CreationTime);
            if (shouldAbort)
            {
                Assert.AreEqual(firstRow.Row, firstAfter.Row);
                AssertDeepEqual(firstRow.Version, firstAfter.Version, true);
                Assert.IsNull((await GetRowAsync(2)).Row);
            }
            else
            {
                Assert.AreEqual("batch-update",
                    firstAfter.Row["name"].AsString);
                Assert.IsNotNull((await GetRowAsync(2)).Row);
            }

            var failedRowAfter = await GetRowAsync(1);
            Assert.AreEqual(existing.Row, failedRowAfter.Row);
            AssertDeepEqual(existing.Version, failedRowAfter.Version, true);
            Assert.AreEqual(creationTime, failedRowAfter.CreationTime);
        }

        [TestMethod]
        public async Task TestAbortedBatchWithMissingRowHasNoCreationTimeAsync()
        {
            var result = await client.WriteManyAsync(Table.Name,
                new WriteOperationCollection()
                    .AddPut(CreateRow(0))
                    .AddPutIfPresent(CreateRow(1),
                        new PutOptions { ReturnExisting = true }, true));

            Assert.IsFalse(result.Success);
            Assert.AreEqual(1, result.FailedOperationIndex);
            Assert.IsNull(result.Results);
            Assert.IsNotNull(result.FailedOperationResult);
            Assert.IsFalse(result.FailedOperationResult.Success);
            Assert.IsNull(result.FailedOperationResult.ExistingRow);
            Assert.IsNull(result.FailedOperationResult.ExistingCreationTime);
            Assert.IsNull((await GetRowAsync(0)).Row);
            Assert.IsNull((await GetRowAsync(1)).Row);
        }

        [DataTestMethod]
        [DataRow(false, true)]
        [DataRow(false, false)]
        [DataRow(true, true)]
        [DataRow(true, false)]
        public async Task TestConvenienceBatchFailureCreationTimeAsync(
            bool delete, bool abort)
        {
            await client.PutAsync(Table.Name, CreateRow(0));
            await client.PutAsync(Table.Name, CreateRow(1));
            var first = await GetRowAsync(0);
            var existing = await GetRowAsync(1);
            var creationTime = GetRequiredCreationTime(existing);
            AssertNotDeepEqual(first.Version, existing.Version, true);

            WriteManyResult<RecordValue> result;
            if (delete)
            {
                // The first row matches; the second has a different version.
                result = await client.DeleteManyAsync(Table.Name,
                    new[] { CreateKey(0), CreateKey(1) },
                    new DeleteManyOptions
                    {
                        MatchVersion = first.Version,
                        ReturnExisting = true,
                        AbortIfUnsuccessful = abort
                    });
            }
            else
            {
                result = await client.PutManyAsync(Table.Name,
                    new[] { CreateRow(2), CreateRow(1, "ignored") },
                    new PutManyOptions
                    {
                        IfAbsent = true,
                        ReturnExisting = true,
                        AbortIfUnsuccessful = abort
                    });
            }

            WriteOperationResult<RecordValue> failed;
            if (abort)
            {
                Assert.IsFalse(result.Success);
                Assert.AreEqual(1, result.FailedOperationIndex);
                Assert.IsNull(result.Results);
                failed = result.FailedOperationResult;
            }
            else
            {
                Assert.IsTrue(result.Success);
                Assert.IsNull(result.FailedOperationIndex);
                Assert.IsNull(result.FailedOperationResult);
                Assert.AreEqual(2, result.Results.Count);
                Assert.IsTrue(result.Results[0].Success);
                failed = result.Results[1];
            }

            Assert.IsNotNull(failed);
            Assert.IsFalse(failed.Success);
            Assert.AreEqual(creationTime, failed.ExistingCreationTime);
            Assert.AreEqual(existing.Row, failed.ExistingRow);

            var firstAfter = await GetRowAsync(delete ? 0 : 2);
            if (delete && abort)
            {
                Assert.AreEqual(first.Row, firstAfter.Row);
                AssertDeepEqual(first.Version, firstAfter.Version, true);
                Assert.AreEqual(first.CreationTime, firstAfter.CreationTime);
            }
            else if (!delete && !abort)
            {
                Assert.IsNotNull(firstAfter.Row);
            }
            else
            {
                Assert.IsNull(firstAfter.Row);
            }

            var failedRowAfter = await GetRowAsync(1);
            Assert.AreEqual(existing.Row, failedRowAfter.Row);
            AssertDeepEqual(existing.Version, failedRowAfter.Version, true);
            Assert.AreEqual(creationTime, failedRowAfter.CreationTime);
        }

        private static void CheckCreationTimeSupport()
        {
            if (KVVersion != null && KVVersion < CreationTimeVersion)
            {
                Assert.Inconclusive(
                    "This test requires server creation-time support");
            }
        }

        private static DateTime GetRequiredCreationTime(
            GetResult<RecordValue> result)
        {
            Assert.IsNotNull(result.Row);
            Assert.IsNotNull(result.CreationTime);
            return result.CreationTime.Value;
        }

        private static MapValue CreateRow(int id, string name = null) =>
            new MapValue
            {
                ["sid"] = ShardId,
                ["id"] = id,
                ["name"] = name ?? $"name-{id}"
            };

        private static MapValue CreateKey(int id) => new MapValue
        {
            ["sid"] = ShardId,
            ["id"] = id
        };

        private static Task<GetResult<RecordValue>> GetRowAsync(int id) =>
            client.GetAsync(Table.Name, CreateKey(id), new GetOptions
            {
                Consistency = Consistency.Absolute,
                Timeout = TimeSpan.FromSeconds(20)
            });
    }
}
