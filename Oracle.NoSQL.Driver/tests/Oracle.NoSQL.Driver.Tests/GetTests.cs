/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver.Tests
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
    public class GetTests : DataTestBase<GetTests>
    {
        private static readonly DataTestFixture Fixture = new DataTestFixture(
            AllTypesTable, new AllTypesRowFactory(), 20);

        private static readonly MapValue GoodPK = MakePrimaryKey(
            Fixture.Table, Fixture.Rows[0]);

        private static readonly IEnumerable<GetOptions>
            BadGetOptions =
                (from timeout in BadTimeSpans
                select new GetOptions
                {
                    Timeout = timeout
                })
                .Concat(
                    from consistency in BadConsistencies
                    select new GetOptions
                    {
                        Consistency = consistency
                    });

        private static IEnumerable<object[]> GetNegativeDataSource =>
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
                from opt in BadGetOptions
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
        [DynamicData(nameof(GetNegativeDataSource))]
        public async Task TestGetNegativeAsync(string tableName,
            MapValue primaryKey, GetOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.GetAsync(tableName, primaryKey, options));
        }

        [TestMethod]
        public async Task TestGetNonExistentTableAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.GetAsync("noSuchTable", GoodPK));
        }

        private static IEnumerable<object[]> GetPositiveDataSource =>
            from row in Fixture.Rows select new object[]
                {
                    Fixture.Table,
                    MakePrimaryKey(Fixture.Table, row),
                    row
                };

        [DataTestMethod]
        [DynamicData(nameof(GetPositiveDataSource))]
        public async Task TestGetAsync(TableInfo table, MapValue primaryKey,
            DataRow row)
        {
            var result = await client.GetAsync(table.Name, primaryKey);
            VerifyGetResult(result, table, row);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetPositiveDataSource))]
        public async Task TestGetWithTimeoutAndConsistencyAsync(
            TableInfo table, MapValue primaryKey, DataRow row)
        {
            var result = await client.GetAsync(table.Name, primaryKey,
                new GetOptions
                {
                    Timeout = TimeSpan.FromMilliseconds(12123),
                    Consistency = AllowEventualConsistency
                        ? Consistency.Eventual
                        : Consistency.Absolute
                });
            VerifyGetResult(result, table, row);
        }

        [DataTestMethod]
        [DynamicData(nameof(GetPositiveDataSource))]
        public async Task TestGetWithAbsoluteConsistencyAsync(
            TableInfo table, MapValue primaryKey, DataRow row)
        {
            var result = await client.GetAsync(table.Name, primaryKey,
                new GetOptions
                {
                    Consistency = Consistency.Absolute
                });
            VerifyGetResult(result, table, row);
        }

        internal static readonly MapValue ExtraPK = MakePrimaryKey(
            Fixture.Table, Fixture.MakeRow(Fixture.Rows.Length));

        [TestMethod]
        public async Task TestGetNonExistent()
        {
            var result = await client.GetAsync(Fixture.Table.Name, ExtraPK);
            VerifyGetResult(result, Fixture.Table, null);
        }

        [TestMethod]
        public async Task TestGetNonExistentWithAbsoluteConsistency()
        {
            var result = await client.GetAsync(Fixture.Table.Name, ExtraPK,
                new GetOptions
                {
                    Consistency = Consistency.Absolute
                });
            VerifyGetResult(result, Fixture.Table, null,
                Consistency.Absolute);
        }
    }

}
