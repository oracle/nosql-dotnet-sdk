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
    using System.Threading.Tasks;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Utils;
    using static NegativeTestData;
    using static TestSchemas;
    using static TestTables;

    [TestClass]
    public class PutTests : DataTestBase<PutTests>
    {
        private static readonly DataTestFixture Fixture = new DataTestFixture(
            AllTypesTable, new AllTypesRowFactory(), 20);

        private static readonly DataRow GoodRow = Fixture.Rows[0];

        private static readonly IEnumerable<PutOptions>
            BadPutOptions = GetBadPutOptions<PutOptions>();

        private static IEnumerable<object[]> PutNegativeDataSource =>
            (from tableName in BadTableNames
             select new object[]
             {
                    tableName,
                    GoodRow,
                    null
             })
            .Concat(
                from row in GetBadRows(Fixture.Table, GoodRow)
                select new object[]
                {
                    Fixture.Table.Name,
                    row,
                    null
                })
            .Concat(
                from opt in BadPutOptions
                select new object[]
                {
                    Fixture.Table.Name,
                    GoodRow,
                    opt
                })
            .Concat(
                from row in GetBadExactMatchRows(Fixture.Table, GoodRow)
                select new object[]
                {
                    Fixture.Table.Name,
                    row,
                    new PutOptions
                    {
                        ExactMatch = true
                    }
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
        [DynamicData(nameof(PutNegativeDataSource))]
        public async Task TestPutNegativeAsync(string tableName,
            MapValue row, PutOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.PutAsync(tableName, row, options));
        }

        [DataTestMethod]
        [DynamicData(nameof(PutNegativeDataSource))]
        public async Task TestPutIfAbsentNegativeAsync(string tableName,
            MapValue row, PutOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.PutIfAbsentAsync(tableName, row, options));
        }

        [DataTestMethod]
        [DynamicData(nameof(PutNegativeDataSource))]
        public async Task TestPutIfPresentNegativeAsync(string tableName,
            MapValue row, PutOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.PutIfPresentAsync(tableName, row, options));
        }

        [DataTestMethod]
        [DynamicData(nameof(PutNegativeDataSource))]
        public async Task TestPutIfVersionNegativeAsync(string tableName,
            MapValue row, PutOptions options)
        {
            Assert.IsNotNull(GoodRow.Version); // test self-check
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.PutIfVersionAsync(tableName, row, GoodRow.Version,
                    options));
        }

        [TestMethod]
        public async Task TestPutIfVersionNullVersionAsync()
        {
            await AssertThrowsDerivedAsync<ArgumentNullException>(() =>
                client.PutIfVersionAsync(Fixture.Table.Name, GoodRow, null));
        }

        [TestMethod]
        public async Task TestPutNonExistentTableAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.PutAsync("noSuchTable", GoodRow));
        }

        [TestMethod]
        public async Task TestPutIfAbsentNonExistentTableAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.PutIfAbsentAsync("noSuchTable", GoodRow));
        }

        [TestMethod]
        public async Task TestPutIfPresentNonExistentTableAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.PutIfPresentAsync("noSuchTable", GoodRow));
        }

        [TestMethod]
        public async Task TestPutIfVersionNonExistentTableAsync()
        {
            Assert.IsNotNull(GoodRow.Version); // test self-check
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.PutIfVersionAsync("noSuchTable", GoodRow,
                    GoodRow.Version));
        }

        private static IEnumerable<object[]> PutNewRowDataSource =>
            from row in Fixture.Rows select new object[]
                {
                    Fixture.Table,
                    Fixture.MakeRow(row.Id + Fixture.RowIdEnd)
                };

        private static IEnumerable<object[]> PutExistingRowDataSource =>
            from row in Fixture.Rows
            select new object[]
            {
                Fixture.Table,
                row
            };

        private static IEnumerable<object[]> PutDataSource =>
            from row in Fixture.Rows
            select new object[]
            {
                Fixture.Table,
                row,
                Fixture.MakeRow(row.Id + Fixture.RowIdEnd)
            };

        private DataRow existingDataRow;
        private DataRow newDataRow;

        // There doesn't seem to be a way to access dynamic data in setup and
        // teardown methods, so we have to do this manually.
        private void SetForCleanup(DataRow currentExisting,
            DataRow currentNew)
        {
            existingDataRow = currentExisting;
            newDataRow = currentNew;
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            // Restore to the initial state by replacing existing row and
            // deleting new row.

            if (existingDataRow != null)
            {
                await PutRowAsync(Fixture.Table, existingDataRow);
                existingDataRow = null;
            }

            if (newDataRow != null)
            {
                await DeleteRowAsync(Fixture.Table, newDataRow);
                newDataRow = null;
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(PutNewRowDataSource))]
        public async Task TestPutNewRowAsync(TableInfo table, DataRow row)
        {
            SetForCleanup(null, row);
            var options = new PutOptions();
            if (row.TTL.HasValue)
            {
                options.TTL = row.TTL;
            }
            else
            {
                options.Timeout = TimeSpan.FromSeconds(8);
                options.ExactMatch = true;
            }

            var result = await client.PutAsync(table.Name, (MapValue)row,
                options);
            await VerifyPutAsync(result, table, row, options);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutExistingRowDataSource))]
        public async Task TestPutExistingRowAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(row, null);
            var result = await client.PutAsync(table.Name, (MapValue)row);
            await VerifyPutAsync(result, table, row);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutExistingRowDataSource))]
        public async Task TestPutModifiedRowAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(row, null);
            var modifiedRow = Fixture.MakeModifiedRow(row);
            var options = new PutOptions
            {
                TTL = modifiedRow.TTL,
                Compartment = Compartment
            };
            var result = await client.PutAsync(table.Name,
                (MapValue)modifiedRow, options);
            await VerifyPutAsync(result, table, modifiedRow, options);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutExistingRowDataSource))]
        public async Task TestPutExistingRowUpdateTTLToDefaultAsync(
            TableInfo table, DataRow row)
        {
            SetForCleanup(row, null);
            if (!table.TTL.HasValue)
            {
                Assert.Inconclusive(
                    "This test runs only when the table has TTL");
            }

            var options = new PutOptions
            {
                UpdateTTLToDefault = true
            };
            var result = await client.PutAsync(table.Name, (MapValue)row,
                options);
            await VerifyPutAsync(result, table, row, options);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutExistingRowDataSource))]
        public async Task TestPutExistingRowDoNotExpireAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(row, null);
            var options = new PutOptions
            {
                TTL = TimeToLive.DoNotExpire,
                ReturnExisting = true
            };
            var result = await client.PutAsync(table.Name, (MapValue)row,
                options);
            await VerifyPutAsync(result, table, row, options);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutNewRowDataSource))]
        public async Task TestPutWithIfAbsentNewRowAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(null, row);
            var options = new PutOptions
            {
                IfAbsent = true,
                TTL = row.TTL,
                Compartment = Compartment
            };
            var result = await client.PutAsync(table.Name, (MapValue)row,
                options);
            await VerifyPutAsync(result, table, row, options);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutNewRowDataSource))]
        public async Task TestPutIfAbsentNewRowAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(null, row);
            var options = row.TTL.HasValue
                ? new PutOptions {TTL = row.TTL}
                : null;
            var result = await client.PutIfAbsentAsync(table.Name,
                (MapValue)row, options);
            await VerifyPutAsync(result, table, row, options, isConditional:true);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutExistingRowDataSource))]
        public async Task TestPutWithIfAbsentModifiedRowAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(row, null);
            var modifiedRow = Fixture.MakeModifiedRow(row);
            var options = new PutOptions
            {
                IfAbsent = true,
                TTL = modifiedRow.TTL,
                ReturnExisting = true
            };
            var result = await client.PutAsync(table.Name,
                (MapValue)modifiedRow, options);
            await VerifyPutAsync(result, table, modifiedRow, options, false, row);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutExistingRowDataSource))]
        public async Task TestPutIfAbsentExistingRowAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(row, null);
            var result = await client.PutIfAbsentAsync(table.Name,
                (MapValue)row);
            await VerifyPutAsync(result, table, row, null, false, row,
                isConditional:true);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutExistingRowDataSource))]
        public async Task TestPutIfAbsentModifiedRowAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(row, null);
            var modifiedRow = Fixture.MakeModifiedRow(row);
            var options = new PutOptions
            {
                ReturnExisting = true
            };
            var result = await client.PutIfAbsentAsync(table.Name,
                (MapValue)modifiedRow, options);
            await VerifyPutAsync(result, table, modifiedRow, options, false, row,
                isConditional:true);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutExistingRowDataSource))]
        public async Task TestPutWithIfPresentModifiedRowAsync(
            TableInfo table, DataRow row)
        {
            SetForCleanup(row, null);
            var modifiedRow = Fixture.MakeModifiedRow(row);
            var options = new PutOptions
            {
                IfPresent = true,
                TTL = modifiedRow.TTL,
                Compartment = Compartment
            };

            var result = await client.PutAsync(table.Name,
                (MapValue)modifiedRow, options);
            await VerifyPutAsync(result, table, modifiedRow, options);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutExistingRowDataSource))]
        public async Task TestPutIfPresentModifiedRowAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(row, null);
            var modifiedRow = Fixture.MakeModifiedRow(row);
            var options = modifiedRow.TTL.HasValue
                ? new PutOptions
                {
                    TTL = modifiedRow.TTL,
                    ExactMatch = false
                }
                : null;

            if (options != null && !options.ExactMatch)
            {
                modifiedRow["no_such_field"] = DateTime.Now;
            }

            var result = await client.PutIfPresentAsync(table.Name,
                (MapValue)modifiedRow, options);
            await VerifyPutAsync(result, table, modifiedRow, options,
                isConditional:true);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutExistingRowDataSource))]
        public async Task TestPutIfPresentExistingRowAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(row, null);
            // ReturnExisting should have no effect here, TTL does not change
            var options = new PutOptions
            {
                UpdateTTLToDefault = true,
                ReturnExisting = true
            };
            var result = await client.PutIfPresentAsync(table.Name,
                (MapValue)row, options);
            await VerifyPutAsync(result, table, row, options, isConditional:true);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutNewRowDataSource))]
        public async Task TestWithPutIfPresentNewRowAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(null, row);
            var options = new PutOptions
            {
                IfPresent = true,
                TTL = row.TTL
            };
            var result = await client.PutAsync(table.Name, (MapValue)row,
                options);
            await VerifyPutAsync(result, table, row, options, false);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutNewRowDataSource))]
        public async Task TestPutIfPresentNewRowAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(null, row);
            var options = row.TTL.HasValue
                ? new PutOptions {TTL = row.TTL}
                : null;
            var result = await client.PutIfPresentAsync(table.Name,
                (MapValue)row, options);
            await VerifyPutAsync(result, table, row, options, false,
                isConditional:true);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutNewRowDataSource))]
        public async Task TestPutIfPresentNewRowWithReturnExistingAsync(
            TableInfo table, DataRow row)
        {
            SetForCleanup(null, row);
            var options = new PutOptions
            {
                ReturnExisting = true,
                TTL = row.TTL
            };
            var result = await client.PutIfPresentAsync(table.Name,
                (MapValue)row, options);
            await VerifyPutAsync(result, table, row, options, false,
                isConditional:true);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutExistingRowDataSource))]
        public async Task TestPutWithMatchVersionAsync(TableInfo table,
            DataRow row)
        {
            SetForCleanup(row, null);
            var options = new PutOptions
            {
                MatchVersion = row.Version,
                TTL = row.TTL,
                ReturnExisting = true,
                Compartment = Compartment
            };

            // put with correct MatchVersion
            var result = await client.PutAsync(table.Name, (MapValue)row,
                options);
            await VerifyPutAsync(result, table, row, options);

            // modify the row
            var modifiedRow = Fixture.MakeModifiedRow(row);
            options.TTL = modifiedRow.TTL;
            // now the version in options is no longer correct
            // (current) version
            result = await client.PutAsync(table.Name, (MapValue)modifiedRow,
                options);
            await VerifyPutAsync(result, table, modifiedRow, options, false, row);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutExistingRowDataSource))]
        public async Task TestPutIfVersionAsync(TableInfo table, DataRow row)
        {
            SetForCleanup(row, null);
            var modifiedRow = Fixture.MakeModifiedRow(row);
            var options = row.TTL.HasValue
                ? new PutOptions {TTL = row.TTL}
                : null;
            var version = row.Version;

            // put with correct MatchVersion
            var result = await client.PutIfVersionAsync(table.Name,
                (MapValue)modifiedRow, version, options);
            await VerifyPutAsync(result, table, modifiedRow, options,
                isConditional: true);

            // modify the row again
            var modifiedRow2 = Fixture.MakeModifiedRow(row);
            options = new PutOptions
            {
                TTL = modifiedRow2.TTL,
                ReturnExisting = true
            };
            // now the version in options is no longer correct
            // (current) version
            result = await client.PutIfVersionAsync(table.Name,
                (MapValue)modifiedRow2, version, options);
            await VerifyPutAsync(result, table, modifiedRow2, options, false,
                modifiedRow, isConditional:true);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutDataSource))]
        public async Task TestPutNewRowWithMatchVersionAsync(TableInfo table,
            DataRow existingRow, DataRow newRow)
        {
            SetForCleanup(existingRow, newRow);
            Assert.IsNull(newRow.Version); // test self-check
            var options = new PutOptions
            {
                MatchVersion = existingRow.Version,
                TTL = newRow.TTL
            };
            var result = await client.PutAsync(table.Name, (MapValue)newRow,
                options);
            await VerifyPutAsync(result, table, newRow, options, false);
        }

        [DataTestMethod]
        [DynamicData(nameof(PutDataSource))]
        public async Task TestPutIfVersionNewRowAsync(TableInfo table,
            DataRow existingRow, DataRow newRow)
        {
            SetForCleanup(existingRow, newRow);
            Assert.IsNull(newRow.Version); // test self-check
            var result = await client.PutIfVersionAsync(table.Name,
                (MapValue)newRow, existingRow.Version);
            await VerifyPutAsync(result, table, newRow, null, false,
                isConditional: true);
        }

        [TestMethod]
        public void TestPutOptions()
        {
            // Here we test the exclusivity of IfAbsent, IfPresent and
            // MatchVersion options.

            var options = new PutOptions();
            Assert.AreEqual(PutOpKind.Always, options.putOpKind);
            Assert.IsFalse(options.IfAbsent);
            Assert.IsFalse(options.IfPresent);
            Assert.IsNull(options.MatchVersion);

            options.IfAbsent = true;
            Assert.AreEqual(PutOpKind.IfAbsent, options.putOpKind);
            Assert.IsTrue(options.IfAbsent);
            Assert.IsFalse(options.IfPresent);
            Assert.IsNull(options.MatchVersion);

            options.IfPresent = true;
            Assert.AreEqual(PutOpKind.IfPresent, options.putOpKind);
            Assert.IsFalse(options.IfAbsent);
            Assert.IsTrue(options.IfPresent);
            Assert.IsNull(options.MatchVersion);

            options.MatchVersion = Fixture.Rows[0].Version;
            Assert.AreEqual(PutOpKind.IfVersion, options.putOpKind);
            Assert.IsFalse(options.IfAbsent);
            Assert.IsFalse(options.IfPresent);
            Assert.IsNotNull(options.MatchVersion);

            options.MatchVersion = null;
            Assert.AreEqual(PutOpKind.Always, options.putOpKind);
            Assert.IsFalse(options.IfAbsent);
            Assert.IsFalse(options.IfPresent);
            Assert.IsNull(options.MatchVersion);

            options.MatchVersion = Fixture.Rows[0].Version;
            Assert.AreEqual(PutOpKind.IfVersion, options.putOpKind);
            options.IfAbsent = true;
            Assert.AreEqual(PutOpKind.IfAbsent, options.putOpKind);
            Assert.IsTrue(options.IfAbsent);
            Assert.IsFalse(options.IfPresent);
            Assert.IsNull(options.MatchVersion);
        }
    }
}
