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
    public class TableDDLTests : TablesTestBase<TableDDLTests>
    {
        private static readonly TableInfo Table = SimpleTable;
        private static readonly IndexInfo[] Indexes = SimpleTableIndexes;

        private static readonly IEnumerable<TableDDLOptions>
            BadTableDDLOptsNoLimits =
                (from timeout in BadTimeSpans
                    select new TableDDLOptions
                    {
                        Timeout = timeout
                    })
                .Concat(
                    from pollDelay in BadTimeSpans
                    select new TableDDLOptions
                    {
                        PollDelay = pollDelay
                    })
                .Append(new TableDDLOptions
                {
                    // poll delay greater than timeout
                    Timeout = TimeSpan.FromSeconds(5),
                    PollDelay = TimeSpan.FromMilliseconds(5100)
                });

        private static readonly IEnumerable<TableLimits> BadTableLimits =
            (from readUnits in BadPositiveInt32
                select new TableLimits(
                    readUnits, DefaultTableLimits.WriteUnits,
                    DefaultTableLimits.StorageGB))
            .Concat(
                from writeUnits in BadPositiveInt32
                select new TableLimits(
                    DefaultTableLimits.ReadUnits, writeUnits,
                    DefaultTableLimits.StorageGB
                ))
            .Concat(
                from storageGB in BadPositiveInt32
                select new TableLimits(
                    DefaultTableLimits.ReadUnits,
                    DefaultTableLimits.WriteUnits, storageGB));

        private static readonly IEnumerable<TableDDLOptions> BadTableDDLOpts =
            (from opt in BadTableDDLOptsNoLimits
                select CombineProperties(opt,
                    new {TableLimits = DefaultTableLimits}))
            .Concat(
                from tableLimits in BadTableLimits
                select new TableDDLOptions
                {
                    TableLimits = tableLimits
                });

        private static readonly IEnumerable<string> BadStatements =
            BadNonEmptyStrings.Append("blah blah");

        private static readonly string CreateTableIfNotExistsStmt =
            MakeCreateTable(Table, true);

        private static IEnumerable<object[]> TableDDLNegativeDataSource =>
            (from badStmt in BadStatements select new object[]
            {
                badStmt,
                null
            })
            .Concat(from badOpt in BadTableDDLOpts
                select new object[]
                {
                    CreateTableIfNotExistsStmt,
                    badOpt
                });

        private static IEnumerable<object[]> TableLimitsNegativeDataSource =>
            (from badTableName in BadTableNames
                select new object[]
                {
                    badTableName,
                    DefaultTableLimits,
                    null
                })
            .Concat(from badTableLimits in BadTableLimits
                select new object[]
                {
                    Table.Name,
                    badTableLimits,
                    null
                })
            .Concat(from badOpt in BadTableDDLOptsNoLimits
                select new object[]
                {
                    Table.Name,
                    DefaultTableLimits,
                    badOpt
                });

        [ClassInitialize]
        public new static void ClassInitialize(TestContext testContext)
        {
            TablesTestBase<TableDDLTests>.ClassInitialize(testContext);
        }

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            await DropTableAsync(Table);
            ClassCleanup();
        }

        [TestInitialize]
        public async Task InitTestCaseAsync()
        {
            if (TestContext.TestName.Contains("Negative"))
            {
                return;
            }

            await DropTableAsync(Table);

            if (TestContext.TestName.Contains("CreateTable"))
            {
                return;
            }

            await CreateTableAsync(Table);

            if (TestContext.TestName.Contains("DropIndex"))
            {
                await CreateIndexesAsync(Table, Indexes);
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(TableDDLNegativeDataSource))]
        public async Task TestTableDDLNegativeAsync(string stmt,
            TableDDLOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.ExecuteTableDDLAsync(stmt, options));
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.ExecuteTableDDLWithCompletionAsync(stmt, options));
        }

        [DataTestMethod]
        [DynamicData(nameof(TableLimitsNegativeDataSource))]
        public async Task TestSetTableLimitsNegativeAsync(string tableName,
            TableLimits tableLimits, TableDDLOptions options)
        {
            CheckNotOnPrem();
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.SetTableLimitsAsync(tableName, tableLimits, options));
        }

        [TestMethod]
        public async Task TestCreateTableAsync()
        {
            var result = await client.ExecuteTableDDLAsync(
                MakeCreateTable(Table),
                new TableDDLOptions
                {
                    Compartment = Compartment,
                    Timeout = TimeSpan.FromSeconds(10),
                    TableLimits = DefaultTableLimits
                });
            VerifyTableResult(result, Table);

            await result.WaitForCompletionAsync();
            VerifyActiveTable(result, Table);

            result = await client.GetTableAsync(Table.Name, new GetTableOptions
            {
                Timeout = TimeSpan.FromMilliseconds(9999)
            });
            VerifyActiveTable(result, Table);
        }

        [TestMethod]
        public async Task TestCreateIndexAsync()
        {
            var result = await client.GetTableAsync(Table.Name,
                new GetTableOptions
                {
                    Timeout = TimeSpan.FromSeconds(6)
                });
            VerifyActiveTable(result, Table);

            result = await client.ExecuteTableDDLAsync(
                MakeCreateIndex(Table, Indexes[0]), new TableDDLOptions
                {
                    Timeout = TimeSpan.FromMilliseconds(5551),
                    Compartment = Compartment
                });
            VerifyTableResult(result, Table);

            await result.WaitForCompletionAsync(TimeSpan.FromSeconds(30),
                TimeSpan.FromMilliseconds(500));
            VerifyActiveTable(result, Table);
        }

        [TestMethod]
        public async Task TestDropIndexAsync()
        {
            var result = await client.GetTableAsync(Table.Name);
            VerifyActiveTable(result, Table);

            result = await client.ExecuteTableDDLWithCompletionAsync(
                MakeDropIndex(Table, Indexes[^1]),
                new TableDDLOptions
                {
                    Timeout = TimeSpan.FromSeconds(80),
                    Compartment = Compartment
                });
            VerifyTableResult(result, Table);

            await result.WaitForCompletionAsync(null,
                TimeSpan.FromMilliseconds(500));
            VerifyActiveTable(result, Table);
        }

        [TestMethod]
        public async Task TestAddFieldAsync()
        {
            var result = await client.GetTableAsync(Table.Name);
            VerifyActiveTable(result, Table);

            result = await client.ExecuteTableDDLAsync(
                MakeAddField(Table, new TableField("salary", DataType.Double)),
                new TableDDLOptions());
            VerifyTableResult(result, Table);

            await result.WaitForCompletionAsync(null,
                TimeSpan.FromTicks(15030001));
            VerifyActiveTable(result, Table);
        }

        [TestMethod]
        public async Task TestDropFieldAsync()
        {
            var result = await client.GetTableAsync(Table.Name);
            VerifyActiveTable(result, Table);

            result = await client.ExecuteTableDDLWithCompletionAsync(
                MakeDropField(Table, Table.Fields[^2]),
                new TableDDLOptions
                {
                    PollDelay = TimeSpan.FromMilliseconds(500)
                });
            VerifyActiveTable(result, Table);
        }

        [TestMethod]
        public async Task TestAlterTTLAsync()
        {
            var result = await client.GetTableAsync(Table.Name);
            VerifyActiveTable(result, Table);

            result = await client.ExecuteTableDDLWithCompletionAsync(
                MakeAlterTTL(Table, TimeToLive.OfDays(7)),
                new TableDDLOptions()
                {
                    Timeout = TimeSpan.FromSeconds(30),
                    PollDelay = TimeSpan.FromMilliseconds(701)
                });
            VerifyActiveTable(result, Table);
        }

        // We parametrize tests for SetTableLimitsAsync and
        // SetTableLimitsWithCompletionAsync to call them with different
        // options.  We don't parametrize tests with ExecuteTableDDLAsync and
        // ExecuteTableDDLWithCompletionAsync because they are already called
        // in several combinations in different test cases.

        private static TableDDLOptions[] OptionsForLimits => new[]
        {
            null,
            new TableDDLOptions(),
            new TableDDLOptions
            {
                Compartment = Compartment,
                Timeout = TimeSpan.FromSeconds(10)
            }
        };

        private static IEnumerable<object[]> OptionsForLimitsDataSource =>
            from options in OptionsForLimits select new object[] { options };

        private static readonly TableDDLOptions[]
            OptionsForLimitsWithCompletion =
        {
            null,
            new TableDDLOptions(),
            new TableDDLOptions
            {
                Timeout = TimeSpan.FromSeconds(44)
            },
            new TableDDLOptions
            {
                PollDelay = TimeSpan.FromMilliseconds(888)
            }
        };

        private static IEnumerable<object[]>
            OptionsForLimitsWithCompletionDataSource =>
                from options in OptionsForLimitsWithCompletion
                select new object[] { options };

        [DataTestMethod]
        [DynamicData(nameof(OptionsForLimitsDataSource))]
        public async Task TestAlterLimitsAsync(TableDDLOptions options)
        {
            CheckNotOnPrem();

            var result = await client.GetTableAsync(Table.Name);
            VerifyActiveTable(result, Table);

            var newTableLimits = new TableLimits(2000, 1000, 200);
            result = await client.SetTableLimitsAsync(Table.Name,
                newTableLimits, options);
            VerifyTableResult(result, Table, ignoreTableLimits: true);

            await result.WaitForCompletionAsync();
            VerifyActiveTable(result, Table, newTableLimits);
        }

        [DataTestMethod]
        [DynamicData(nameof(OptionsForLimitsWithCompletionDataSource))]
        public async Task TestAlterLimitsWithCompletionAsync(
            TableDDLOptions options)
        {
            CheckNotOnPrem();

            var result = await client.GetTableAsync(Table.Name);
            VerifyActiveTable(result, Table);

            var newTableLimits = new TableLimits(5, 5, 1);
            result = await client.SetTableLimitsWithCompletionAsync(
                Table.Name, newTableLimits, options);
            VerifyActiveTable(result, Table, newTableLimits);
        }

        [TestMethod]
        public async Task TestDropTableAsync()
        {
            var result = await client.ExecuteTableDDLWithCompletionAsync(
                MakeDropTable(Table), new TableDDLOptions());
            Assert.IsNotNull(result);
            Assert.AreEqual(result.TableName, Table.Name);
            Assert.AreEqual(result.TableState, TableState.Dropped);
        }
    }

}
