/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
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
    using static Utils;

    [TestClass]
    public class RowMetadataTests : DataTestBase<RowMetadataTests>
    {
        private const int ShardId = 1;

        private static readonly TableInfo Table = new TableInfo(
            TableNamePrefix + "RowMetadata" +
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

        private static readonly string CreatedBy =
            "{\"user\":\"create\",\"time\":\"2026-02-10\"}";

        private static readonly string UpdatedBy1 =
            "{\"user\":\"update1\",\"time\":\"2026-03-10\"}";

        private static readonly string UpdatedBy2 =
            "{\"user\":\"update2\",\"time\":\"2026-03-20\"}";

        private static readonly string DeletedBy =
            "{\"user\":\"delete\",\"time\":\"2026-04-10\"}";

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
            await DropTableAsync(Table);
            await CreateTableAsync(Table);
        }

        [TestMethod]
        public async Task TestPutAndGetRowMetadataAsync()
        {
            var putResult = await client.PutAsync(Table.Name, CreateRow(0),
                new PutOptions
                {
                    LastWriteMetadata = CreatedBy
                });
            Assert.IsTrue(putResult.Success);

            var getResult = await GetRowAsync(0);
            AssertJsonEqual(CreatedBy, getResult.LastWriteMetadata);

            putResult = await client.PutAsync(Table.Name, CreateRow(0, "n2"),
                new PutOptions
                {
                    LastWriteMetadata = UpdatedBy1,
                    ReturnExisting = true
                });

            Assert.IsTrue(putResult.Success);
            AssertJsonEqual(CreatedBy, putResult.ExistingLastWriteMetadata);

            getResult = await GetRowAsync(0);
            AssertJsonEqual(UpdatedBy1, getResult.LastWriteMetadata);

            putResult = await client.PutAsync(Table.Name, CreateRow(0, "n3"),
                new PutOptions
                {
                    ReturnExisting = true
                });

            Assert.IsTrue(putResult.Success);
            AssertJsonEqual(UpdatedBy1, putResult.ExistingLastWriteMetadata);

            getResult = await GetRowAsync(0);
            Assert.IsNull(getResult.LastWriteMetadata);
        }

        [TestMethod]
        public async Task TestDeleteRowMetadataAsync()
        {
            await client.PutAsync(Table.Name, CreateRow(0),
                new PutOptions
                {
                    LastWriteMetadata = CreatedBy
                });

            var deleteResult = await client.DeleteAsync(Table.Name,
                CreateKey(0), new DeleteOptions
                {
                    LastWriteMetadata = DeletedBy,
                    ReturnExisting = true
                });

            Assert.IsTrue(deleteResult.Success);
            AssertJsonEqual(CreatedBy, deleteResult.ExistingLastWriteMetadata);

            var getResult = await GetRowAsync(0);
            Assert.IsNull(getResult.Row);
        }

        [TestMethod]
        public async Task TestDeleteRangeWithRowMetadataAsync()
        {
            await client.PutAsync(Table.Name, CreateRow(0),
                new PutOptions { LastWriteMetadata = CreatedBy });
            await client.PutAsync(Table.Name, CreateRow(1),
                new PutOptions { LastWriteMetadata = CreatedBy });
            await client.PutAsync(Table.Name, CreateRow(2),
                new PutOptions { LastWriteMetadata = CreatedBy });

            var result = await client.DeleteRangeAsync(Table.Name,
                CreateShardKey(), new DeleteRangeOptions
                {
                    LastWriteMetadata = DeletedBy
                });

            Assert.AreEqual(3, result.DeletedCount);

            for (var i = 0; i < 3; i++)
            {
                var getResult = await GetRowAsync(i);
                Assert.IsNull(getResult.Row);
            }
        }

        [TestMethod]
        public async Task TestWriteManyRowMetadataAsync()
        {
            var writeResult = await client.WriteManyAsync(Table.Name,
                new WriteOperationCollection()
                    .AddPut(CreateRow(0), new PutOptions
                    {
                        LastWriteMetadata = CreatedBy
                    })
                    .AddPut(CreateRow(1), new PutOptions
                    {
                        LastWriteMetadata = UpdatedBy1
                    }));

            Assert.IsTrue(writeResult.Success);
            AssertJsonEqual(CreatedBy,
                (await GetRowAsync(0))
                .LastWriteMetadata);
            AssertJsonEqual(UpdatedBy1,
                (await GetRowAsync(1))
                .LastWriteMetadata);

            writeResult = await client.WriteManyAsync(Table.Name,
                new WriteOperationCollection()
                    .AddPutIfPresent(CreateRow(0, "n2"), new PutOptions
                    {
                        LastWriteMetadata = UpdatedBy2,
                        ReturnExisting = true
                    })
                    .AddDelete(CreateKey(1), new DeleteOptions
                    {
                        LastWriteMetadata = DeletedBy,
                        ReturnExisting = true
                    }));

            Assert.IsTrue(writeResult.Success);
            Assert.AreEqual(2, writeResult.Results.Count);
            AssertJsonEqual(CreatedBy,
                writeResult.Results[0].ExistingLastWriteMetadata);
            AssertJsonEqual(UpdatedBy1,
                writeResult.Results[1].ExistingLastWriteMetadata);

            AssertJsonEqual(UpdatedBy2,
                (await GetRowAsync(0))
                .LastWriteMetadata);
            Assert.IsNull((await GetRowAsync(1)).Row);
        }

        [TestMethod]
        public async Task TestQueryRowMetadataAsync()
        {
            await ExecuteQueryAsync(
                $"UPSERT INTO {Table.Name} VALUES({ShardId}, 0, 'n0')",
                new QueryOptions
                {
                    LastWriteMetadata = CreatedBy
                });

            AssertJsonEqual(CreatedBy,
                (await GetRowAsync(0))
                .LastWriteMetadata);

            await ExecuteQueryAsync(
                $"UPDATE {Table.Name} AS t SET t.name = 'n1' " +
                $"WHERE t.sid = {ShardId} AND t.id = 0",
                new QueryOptions
                {
                    LastWriteMetadata = UpdatedBy1
                });

            AssertJsonEqual(UpdatedBy1,
                (await GetRowAsync(0))
                .LastWriteMetadata);

            await ExecuteQueryAsync(
                $"DELETE FROM {Table.Name} AS t " +
                $"WHERE t.sid = {ShardId} AND t.id = 0",
                new QueryOptions
                {
                    LastWriteMetadata = DeletedBy
                });

            Assert.IsNull((await GetRowAsync(0)).Row);
        }

        private static async Task ExecuteQueryAsync(string statement,
            QueryOptions options)
        {
            QueryResult<RecordValue> result;
            do
            {
                result = await client.QueryAsync(statement, options);
                options.ContinuationKey = result.ContinuationKey;
            } while (options.ContinuationKey != null);
        }

        private static MapValue CreateRow(int id, string name = null) =>
            new MapValue
            {
                ["sid"] = ShardId,
                ["id"] = id,
                ["name"] = name ?? $"name-{id}"
            };

        private static MapValue CreateKey(int id) =>
            new MapValue
            {
                ["sid"] = ShardId,
                ["id"] = id
            };

        private static MapValue CreateShardKey() =>
            new MapValue
            {
                ["sid"] = ShardId
            };

        private static Task<GetResult<RecordValue>> GetRowAsync(int id) =>
            client.GetAsync(Table.Name, CreateKey(id), new GetOptions
            {
                Consistency = Consistency.Absolute,
                Timeout = TimeSpan.FromSeconds(20)
            });

        private static void AssertJsonEqual(string expected, string actual)
        {
            if (expected == null || actual == null)
            {
                Assert.AreEqual(expected, actual);
                return;
            }

            AssertDeepEqual(FieldValue.FromJsonString(expected),
                FieldValue.FromJsonString(actual));
        }
    }
}
