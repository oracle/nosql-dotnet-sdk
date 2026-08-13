/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static TestSchemas;
    using static TestTables;

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
        public static async Task ClassInitializeAsync(TestContext testContext)
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
