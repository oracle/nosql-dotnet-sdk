/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 * https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static TestSchemas;
    using static TestTables;
    using static Utils;

    // Unlike the feature tests, this exercises real older servers too.
    [TestClass]
    public class CreationTimeCompatibilityTests :
        DataTestBase<CreationTimeCompatibilityTests>
    {
        private static readonly TableInfo Table = new TableInfo(
            TableNamePrefix + "CreationTimeCompat" +
            Guid.NewGuid().ToString("N").Substring(0, 8), DefaultTableLimits,
            new[] { new TableField("sid", DataType.Integer),
                new TableField("id", DataType.Integer),
                new TableField("name", DataType.String) },
            new[] { "sid", "id" }, 1);

        [ClassInitialize]
        public static void InitializeClass(TestContext context) =>
            ClassInitialize(context);

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            await DropTableAsync(Table);
            ClassCleanup();
        }

        [TestMethod]
        public async Task TestUnavailableCreationTimePreservesLiveMetadataAsync()
        {
            if (!IsExpectedProtocolV4OrAbove)
            {
                Assert.Inconclusive("This test requires protocol V4 metadata");
            }
            await CreateTableAsync(Table);
            var row = new MapValue
                { ["sid"] = 1, ["id"] = 1, ["name"] = "original" };
            var key = new MapValue { ["sid"] = 1, ["id"] = 1 };
            var inserted = await client.PutAsync(Table.Name, row);
            var get = await client.GetAsync(Table.Name, key,
                new GetOptions { Consistency = Consistency.Absolute });
            Assert.AreEqual(row, get.Row);
            Assert.IsNotNull(get.Version);
            AssertDeepEqual(inserted.Version, get.Version, true);
            Assert.IsNotNull(get.ModificationTime);
            Assert.IsTrue(get.ModificationTime > DateTime.UnixEpoch);
            Assert.AreEqual(DateTimeKind.Utc, get.ModificationTime.Value.Kind);
            if (CreationTimeTestCapabilities.IsSupported(KVVersion))
            {
                Assert.IsNotNull(get.CreationTime);
                Assert.IsTrue(get.CreationTime <= get.ModificationTime);
            }
            else
            {
                Assert.IsNull(get.CreationTime);
            }

            var put = await client.PutIfAbsentAsync(Table.Name, row,
                new PutOptions { ReturnExisting = true });
            Assert.IsFalse(put.Success);
            AssertExisting(put, get);

            // A valid but stale version gives failed-delete return metadata.
            var stale = inserted.Version;
            await client.PutAsync(Table.Name, row);
            get = await client.GetAsync(Table.Name, key,
                new GetOptions { Consistency = Consistency.Absolute });
            AssertNotDeepEqual(stale, get.Version, true);
            var delete = await client.DeleteIfVersionAsync(Table.Name, key,
                stale, new DeleteOptions { ReturnExisting = true });
            Assert.IsFalse(delete.Success);
            AssertExisting(delete, get);

            var other = new MapValue
                { ["sid"] = 1, ["id"] = 2, ["name"] = "rolled-back" };
            var batch = await client.WriteManyAsync(Table.Name,
                new WriteOperationCollection().AddPut(other)
                    .AddPutIfAbsent(row,
                        new PutOptions { ReturnExisting = true }, true));
            Assert.IsFalse(batch.Success);
            Assert.AreEqual(1, batch.FailedOperationIndex);
            Assert.IsNull(batch.Results);
            Assert.IsNotNull(batch.FailedOperationResult);
            Assert.IsFalse(batch.FailedOperationResult.Success);
            AssertExisting(batch.FailedOperationResult, get);
            Assert.IsNull((await client.GetAsync(Table.Name,
                new MapValue { ["sid"] = 1, ["id"] = 2 },
                new GetOptions { Consistency = Consistency.Absolute })).Row);
            var after = await client.GetAsync(Table.Name, key,
                new GetOptions { Consistency = Consistency.Absolute });
            Assert.AreEqual(get.Row, after.Row);
            AssertDeepEqual(get.Version, after.Version, true);
            Assert.AreEqual(get.ModificationTime, after.ModificationTime);
            Assert.AreEqual(get.CreationTime, after.CreationTime);
        }

        private static void AssertExisting(IWriteResult<RecordValue> result,
            GetResult<RecordValue> expected)
        {
            Assert.AreEqual(expected.Row, result.ExistingRow);
            AssertDeepEqual(expected.Version, result.ExistingVersion, true);
            Assert.AreEqual(expected.ModificationTime,
                result.ExistingModificationTime);
            Assert.AreEqual(expected.CreationTime, result.ExistingCreationTime);
        }
    }
}
