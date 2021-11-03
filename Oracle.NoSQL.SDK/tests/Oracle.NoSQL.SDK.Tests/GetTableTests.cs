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
    using System.Threading.Tasks;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Utils;
    using static NegativeTestData;
    using static TestSchemas;
    using static TestTables;

    [TestClass]
    public class GetTableTests : TablesTestBase<GetTableTests>
    {
        // use for negative tests and GetTableAsync test, table is
        // created only once
        private static readonly TableInfo Table = SimpleTable;

        // use for positive tests, this table is created and dropped for
        // every test
        private static readonly TableInfo Table2 = GetSimpleTableWithName(
            TableNamePrefix + "T2");

        private static readonly IndexInfo Index = SimpleTableIndexes[0];

        private static readonly IEnumerable<GetTableOptions>
            BadGetTableOptions =
                from timeout in BadTimeSpans select new GetTableOptions
                {
                    Timeout = timeout
                };

        private static IEnumerable<object[]> GetTableNegativeDataSource =>
            (from tableName in BadTableNames
                select new object[]
                {
                    tableName,
                    null
                })
            .Concat(
                from opt in BadGetTableOptions
                select new object[]
                {
                    Table.Name,
                    opt
                });

        private static readonly IEnumerable<TableCompletionOptions>
            BadTableCompletionOptions =
                (from opt in BadGetTableOptions
                    select new TableCompletionOptions
                    {
                        Timeout = opt.Timeout
                    })
                .Concat(
                    from pollDelay in BadTimeSpans
                    select new TableCompletionOptions
                    {
                        PollDelay = pollDelay
                    })
                .Append(new TableCompletionOptions
                {
                    // poll delay greater than timeout
                    Timeout = TimeSpan.FromSeconds(5),
                    PollDelay = TimeSpan.FromMilliseconds(5100)
                });

        private static IEnumerable<object[]>
            ForCompletionNegativeDataSource =>
            from opt in BadTableCompletionOptions
            select new object[] {opt.Timeout, opt.PollDelay};

        private static IEnumerable<object[]>
            ForTableStateNegativeDataSource =>
            (from tableName in BadTableNames
                select new object[] {tableName, TableState.Active, null})
            .Append(new object[] {Table.Name, (TableState)(-1), null})
            .Concat(
                from opt in BadTableCompletionOptions
                select new object[] {Table.Name, TableState.Active, opt});

        private static TableResult sampleResult;

        [ClassInitialize]
        public static async Task ClassInitializeAsync(TestContext testContext)
        {
            ClassInitialize(testContext);
            // we will use this result for WaitForCompletion negative tests
            sampleResult = await client.ExecuteTableDDLAsync(
                MakeCreateTable(Table, true), DefaultTableLimits);
            await sampleResult.WaitForCompletionAsync();
        }

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            await DropTableAsync(Table);
            ClassCleanup();
        }

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            if (TestContext.TestName.Contains("Negative"))
            {
                return;
            }

            if (TestContext.TestName.Contains("ForTableState") ||
                TestContext.TestName.Contains("ForCompletion"))
            {
                await DropTableAsync(Table2);
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(GetTableNegativeDataSource))]
        public async Task TestGetTableNegativeAsync(string tableName,
            GetTableOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.GetTableAsync(tableName, options));
        }

        [TestMethod]
        public async Task TestGetTableNonExistentAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.GetTableAsync("noSuchTable"));
        }

        [DataTestMethod]
        [DynamicData(nameof(ForCompletionNegativeDataSource))]
        public async Task TestForCompletionNegativeAsync(
            TimeSpan? timeout, TimeSpan? pollDelay)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                sampleResult.WaitForCompletionAsync(timeout, pollDelay));
        }

        [DataTestMethod]
        [DynamicData(nameof(ForTableStateNegativeDataSource))]
        public async Task TestForTableStateNegativeAsync(string tableName,
            TableState tableState, TableCompletionOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.WaitForTableStateAsync(tableName, tableState,
                    options));
        }

        [TestMethod]
        public async Task TestForTableStateTimeoutAsync()
        {
            await Assert.ThrowsExceptionAsync<TimeoutException>(() =>
                client.WaitForTableStateAsync(Table.Name, TableState.Dropping,
                    new TableCompletionOptions
                    {
                        Timeout = TimeSpan.FromMilliseconds(500),
                        PollDelay = TimeSpan.FromMilliseconds(200)
                    }));
        }

        [TestMethod]
        public async Task TestGetTableAsync()
        {
            var result = await client.GetTableAsync(Table.Name,
                new GetTableOptions
                {
                    Timeout = TimeSpan.FromSeconds(12)
                });
            VerifyActiveTable(result, Table);
        }

        [TestMethod]
        public async Task TestForCompletionOnCreateTableAsync()
        {
            var result = await client.ExecuteTableDDLAsync(
                MakeCreateTable(Table2), DefaultTableLimits);
            VerifyTableResult(result, Table2);

            await result.WaitForCompletionAsync(null,
                TimeSpan.FromMilliseconds(499));
            VerifyActiveTable(result, Table2);
        }

        [TestMethod]
        public async Task TestForCompletionOnUpdateTableAsync()
        {
            await CreateTableAsync(Table2);
            var result = await client.ExecuteTableDDLAsync(
                MakeCreateIndex(Table2, Index));
            VerifyTableResult(result, Table2);

            await result.WaitForCompletionAsync(TimeSpan.FromSeconds(30),
                TimeSpan.FromMilliseconds(503));
            VerifyActiveTable(result, Table2);
        }

        [TestMethod]
        public async Task TestForCompletionOnDropTableAsync()
        {
            await CreateTableAsync(Table2);
            var result = await client.ExecuteTableDDLAsync(
                MakeDropTable(Table2));

            await result.WaitForCompletionAsync(TimeSpan.FromSeconds(61));

            Assert.AreEqual(Table2.Name, result.TableName);
            Assert.AreEqual(TableState.Dropped, result.TableState);
        }

        [TestMethod]
        public async Task TestForTableStateOnCreateTableAsync()
        {
            var result = await client.ExecuteTableDDLAsync(
                MakeCreateTable(Table2), new TableDDLOptions
                {
                    TableLimits = DefaultTableLimits
                });
            VerifyTableResult(result, Table2);

            result = await client.WaitForTableStateAsync(Table2.Name,
                TableState.Active);
            VerifyActiveTable(result, Table2);
        }

        [TestMethod]
        public async Task TestForTableStateOnUpdateTableAsync()
        {
            await CreateTableAsync(Table2);
            var result = await client.ExecuteTableDDLAsync(
                MakeCreateIndex(Table2, Index));
            VerifyTableResult(result, Table2);

            result = await client.WaitForTableStateAsync(Table2.Name,
                TableState.Active, new TableCompletionOptions
                {
                    Timeout = TimeSpan.FromSeconds(50),
                    PollDelay = TimeSpan.FromMilliseconds(600)
                });
            VerifyActiveTable(result, Table2);
        }

        [TestMethod]
        public async Task TestForTableStateOnDropTableAsync()
        {
            await CreateTableAsync(Table2);
            var result = await client.ExecuteTableDDLAsync(
                MakeDropTable(Table2));
            VerifyTableResult(result, Table2, ignoreTableLimits: true);

            result = await client.WaitForTableStateAsync(Table2.Name,
                TableState.Dropped, new TableCompletionOptions
                {
                    PollDelay = TimeSpan.FromMilliseconds(300)
                });

            Assert.AreEqual(Table2.Name, result.TableName);
            Assert.AreEqual(TableState.Dropped, result.TableState);
        }

    }

}
