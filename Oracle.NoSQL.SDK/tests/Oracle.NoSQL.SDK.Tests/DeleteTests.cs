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
    public class DeleteTests : DataTestBase<DeleteTests>
    {
        private static readonly DataTestFixture Fixture = new DataTestFixture(
            AllTypesTable, new AllTypesRowFactory(), 20);

        private static readonly DataRow GoodRow = Fixture.Rows[0];

        private static readonly MapValue GoodPK = MakePrimaryKey(
            Fixture.Table, GoodRow);

        private static readonly IEnumerable<DeleteOptions> BadDeleteOptions =
            GetBadDeleteOptions<DeleteOptions>();

        private static IEnumerable<object[]> DeleteNegativeDataSource =>
            (from tableName in BadTableNames
                select new object[]
                {
                    tableName,
                    GoodPK,
                    null
                })
            .Concat(
                from pk in GetBadPrimaryKeys(Fixture.Table, GoodPK)
                select new object[]
                {
                    Fixture.Table.Name,
                    pk,
                    null
                })
            .Concat(
                from opt in BadDeleteOptions
                select new object[]
                {
                    Fixture.Table.Name,
                    GoodPK,
                    opt
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

        [DataTestMethod]
        [DynamicData(nameof(DeleteNegativeDataSource))]
        public async Task TestDeleteNegativeAsync(string tableName,
            MapValue primaryKey, DeleteOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.DeleteAsync(tableName, primaryKey, options));
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteNegativeDataSource))]
        public async Task TestDeleteIfVersionNegativeAsync(string tableName,
            MapValue primaryKey, DeleteOptions options)
        {
            Assert.IsNotNull(GoodRow.Version); // test self-check
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.DeleteIfVersionAsync(tableName, primaryKey,
                    GoodRow.Version, options));
        }

        [TestMethod]
        public async Task TestDeleteIfVersionNullVersionAsync()
        {
            await AssertThrowsDerivedAsync<ArgumentNullException>(() =>
                client.DeleteIfVersionAsync(Fixture.Table.Name, GoodPK, null));
        }

        [TestMethod]
        public async Task TestDeleteNonExistentTableAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.DeleteAsync("noSuchTable", GoodPK));
        }

        [TestMethod]
        public async Task TestDeleteIfVersionNonExistentTableAsync()
        {
            Assert.IsNotNull(GoodRow.Version); // test self-check
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.DeleteIfVersionAsync("noSuchTable", GoodPK,
                    GoodRow.Version));
        }

        private static IEnumerable<object[]> DeleteExistingRowDataSource =>
            from row in Fixture.Rows
            select new object[]
            {
                Fixture.Table,
                row
            };

        private static IEnumerable<object[]> DeleteNonExistentRowDataSource =>
            from row in Fixture.Rows
            select new object[]
            {
                Fixture.Table,
                Fixture.MakeRow(row.Id + Fixture.RowIdEnd)
            };

        private static IEnumerable<object[]> DeleteDataSource =>
            from row in Fixture.Rows
            select new object[]
            {
                Fixture.Table,
                row,
                Fixture.MakeRow(row.Id + Fixture.RowIdEnd)
            };

        private DataRow existingDataRow;

        // There doesn't seem to be a way to access dynamic data in setup and
        // teardown methods, so we have to do this manually.
        private void SetForCleanup(DataRow currentExisting)
        {
            existingDataRow = currentExisting;
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            // Restore to the initial state by replacing existing row.
            if (existingDataRow != null)
            {
                await PutRowAsync(Fixture.Table, existingDataRow);
                existingDataRow = null;
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteExistingRowDataSource))]
        public async Task TestDeleteExistingRowAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(row);
            var primaryKey = MakePrimaryKey(table, row);
            var result = await client.DeleteAsync(table.Name, primaryKey);
            await VerifyDeleteAsync(result, table, primaryKey);
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteExistingRowDataSource))]
        public async Task TestDeleteExistingRowWithOptionsAsync(
            TableInfo table, DataRow row)
        {
            SetForCleanup(row);
            var primaryKey = MakePrimaryKey(table, row);
            var options = new DeleteOptions
            {
                Compartment = Compartment,
                Timeout = TimeSpan.FromSeconds(12),
                // should return row as it was before deletion
                ReturnExisting = true,
                Durability = Durability.CommitSync
            };
            var result = await client.DeleteAsync(table.Name, primaryKey,
                options);
            await VerifyDeleteAsync(result, table, primaryKey, options,
                existingRow: row);
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteNonExistentRowDataSource))]
        public async Task TestDeleteNonExistentRowAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(null);
            var primaryKey = MakePrimaryKey(table, row);
            var result = await client.DeleteAsync(table.Name, primaryKey);
            await VerifyDeleteAsync(result, table, primaryKey, null, false);
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteNonExistentRowDataSource))]
        public async Task TestDeleteNonExistentRowWithOptionsAsync(
            TableInfo table, DataRow row)
        {
            var primaryKey = MakePrimaryKey(table, row);
            var options = new DeleteOptions
            {
                Timeout = TimeSpan.FromMilliseconds(8625),
                ReturnExisting = true // should have no effect here
            };
            var result = await client.DeleteAsync(table.Name, primaryKey,
                options);
            await VerifyDeleteAsync(result, table, primaryKey, options, false);
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteExistingRowDataSource))]
        public async Task TestDeleteWithMatchVersionAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(row);
            var primaryKey = MakePrimaryKey(table, row);
            var options = new DeleteOptions
            {
                MatchVersion = row.Version,
                ReturnExisting = true,
                Compartment = Compartment
            };

            var result = await client.DeleteAsync(table.Name, primaryKey,
                options);
            // existing row not returned if MatchVersion is specified
            await VerifyDeleteAsync(result, table, primaryKey, options);

            // now the row has been deleted
            result = await client.DeleteAsync(table.Name, primaryKey,
                options);
            await VerifyDeleteAsync(result, table, primaryKey, options,
                false);

            // re-insert the row
            await PutRowAsync(table, row);

            // now the old version in options is no longer correct
            // (current) version
            result = await client.DeleteAsync(table.Name, primaryKey,
                options);
            await VerifyDeleteAsync(result, table, primaryKey, options, false,
                row);

            // do the same with ReturnExisting = false and verify
            options.ReturnExisting = false;
            result = await client.DeleteAsync(table.Name, primaryKey,
                options);
            await VerifyDeleteAsync(result, table, primaryKey, options, false,
                row);
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteDataSource))]
        public async Task TestDeleteWithMatchVersionNonExistentAsync(
            TableInfo table, DataRow existingRow, DataRow nonExistentRow)
        {
            var primaryKey = MakePrimaryKey(table, nonExistentRow);
            var options = new DeleteOptions
            {
                MatchVersion = existingRow.Version,
                ReturnExisting = true
            };
            var result = await client.DeleteAsync(table.Name, primaryKey,
                options);
            await VerifyDeleteAsync(result, table, primaryKey, options, false);
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteExistingRowDataSource))]
        public async Task TestDeleteIfVersionAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(row);
            var primaryKey = MakePrimaryKey(table, row);
            var version = row.Version;

            var result = await client.DeleteIfVersionAsync(table.Name,
                primaryKey, version);
            await VerifyDeleteAsync(result, table, primaryKey,
                isConditional:true);

            // now the row has been deleted
            result = await client.DeleteIfVersionAsync(table.Name, primaryKey,
                version);
            await VerifyDeleteAsync(result, table, primaryKey, null, false,
                isConditional:true);

            // re-insert the row
            await PutRowAsync(table, row);

            // now the old version is no longer correct (current) version
            result = await client.DeleteIfVersionAsync(table.Name, primaryKey,
                version);
            await VerifyDeleteAsync(result, table, primaryKey, null, false,
                row, isConditional:true);

            // do the same with ReturnExisting = true and verify
            var options = new DeleteOptions
            {
                ReturnExisting = true
            };
            result = await client.DeleteIfVersionAsync(table.Name, primaryKey,
                version, options);
            await VerifyDeleteAsync(result, table, primaryKey, options, false,
                row, isConditional:true);
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteDataSource))]
        public async Task TestDeleteIfVersionNonExistentAsync(TableInfo table,
            DataRow existingRow, DataRow nonExistentRow)
        {
            var primaryKey = MakePrimaryKey(table, nonExistentRow);
            var options = new DeleteOptions
            {
                Timeout = TimeSpan.FromSeconds(20),
                ReturnExisting = true
            };
            var result = await client.DeleteIfVersionAsync(table.Name,
                primaryKey, existingRow.Version, options);
            await VerifyDeleteAsync(result, table, primaryKey, options, false,
                isConditional:true);
        }

        [DataTestMethod]
        [DynamicData(nameof(DeleteExistingRowDataSource))]
        public async Task TestDeleteIfVersionWithDurabilityAsync(
            TableInfo table, DataRow row)
        {
            SetForCleanup(row);
            var primaryKey = MakePrimaryKey(table, row);
            var version = row.Version;

            var result = await client.DeleteIfVersionAsync(table.Name,
                primaryKey, version, new DeleteOptions
                {
                    Durability = new Durability(SyncPolicy.Sync,
                        SyncPolicy.WriteNoSync, ReplicaAckPolicy.All),
                    ReturnExisting = true
                });
            // existing row not returned if MatchVersion is specified
            await VerifyDeleteAsync(result, table, primaryKey,
                isConditional:true);
        }
    }
}
