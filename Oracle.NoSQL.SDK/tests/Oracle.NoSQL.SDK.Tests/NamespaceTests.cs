namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static TestTables;
    using static TestSchemas;
    using static Utils;

    [TestClass]
    public class NamespaceTests : DataTestBase<NamespaceTests>
    {
        private const string NamespaceName = "test_opt_ns";

        private static readonly string TableName = AllTypesTable.Name;
        private static readonly string FullTableName =
            $"{NamespaceName}:{TableName}";

        private static readonly DataTestFixture Fixture = new DataTestFixture(
            GetAllTypesTableWithName(FullTableName), new AllTypesRowFactory(),
            20);

        private static readonly DataRow GoodRow = Fixture.Rows[0];
        private static readonly MapValue GoodPK = MakePrimaryKey(
            Fixture.Table, Fixture.Rows[0]);
        private static readonly MapValue GoodPartialPK = RemoveFieldFromMap(
            GoodPK, Fixture.Table.PrimaryKey[^1]);

        private static NoSQLClient nsClient;
        private static NoSQLClient invalidNsClient;

        [ClassInitialize]
        public static async Task ClassInitializeAsync(TestContext testContext)
        {
            ClassInitialize(testContext);
            if (!IsOnPrem)
            {
                return;
            }

            await client.ExecuteAdminWithCompletionAsync(
                $"DROP NAMESPACE IF EXISTS {NamespaceName} CASCADE");
            await client.ExecuteAdminAsync($"CREATE NAMESPACE {NamespaceName}");

            await CreateTableAsync(Fixture.Table);

            var nsConfig = CopyConfig();
            nsConfig.Namespace = NamespaceName;
            nsClient = new NoSQLClient(nsConfig);
            nsConfig = CopyConfig();
            nsConfig.Namespace = "invalid";
            invalidNsClient = new NoSQLClient(nsConfig);
        }

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            if (IsOnPrem)
            {
                invalidNsClient?.Dispose();
                nsClient?.Dispose();
                await client.ExecuteAdminWithCompletionAsync(
                    $"DROP NAMESPACE IF EXISTS {NamespaceName} CASCADE");
            }

            ClassCleanup();
        }

        // For some reason MSTest executes tests even if ClassInitialize
        // fails. Instead we disable them here if we are not running on-prem.
        [TestInitialize]
        public void TestInitialize()
        {
            CheckOnPrem();
        }

        [TestMethod]
        public async Task TestGetAsync()
        {
            await PutRowAsync(Fixture.Table, GoodRow);

            // namespace not specified
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.GetAsync(TableName, GoodPK));
            // invalid namespace in config
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                invalidNsClient.GetAsync(TableName, GoodPK));
            // invalid namespace in options
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.GetAsync(TableName, GoodPK, new GetOptions
                {
                    Namespace = "invalid"
                }));
            // invalid namespace in options overrides valid one in config
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                nsClient.GetAsync(TableName, GoodPK, new GetOptions
                {
                    Namespace = "invalid"
                }));

            // full table name specified
            var result = await client.GetAsync(FullTableName, GoodPK);
            Assert.IsNotNull(result.Row);
            // full table name overrides invalid namespace in config
            result = await invalidNsClient.GetAsync(FullTableName, GoodPK);
            Assert.IsNotNull(result.Row);
            // valid namespace in config
            result = await nsClient.GetAsync(TableName, GoodPK);
            Assert.IsNotNull(result.Row);
            // valid namespace in options
            result = await client.GetAsync(TableName, GoodPK, new GetOptions
            {
                Namespace = NamespaceName
            });
            Assert.IsNotNull(result.Row);
            // valid namespace in options overrides invalid one in config
            result = await invalidNsClient.GetAsync(TableName, GoodPK,
                new GetOptions
                {
                    Namespace = NamespaceName
                });
            Assert.IsNotNull(result.Row);
        }

        [TestMethod]
        public async Task TestPutAsync()
        {
            await PutRowAsync(Fixture.Table, GoodRow);

            // namespace not specified
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.PutAsync(TableName, GoodRow));
            // invalid namespace in config
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                invalidNsClient.PutAsync(TableName, GoodRow));
            // invalid namespace in options
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.PutAsync(TableName, GoodRow, new PutOptions
                {
                    Namespace = "invalid"
                }));
            // invalid namespace in options overrides valid one in config
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                nsClient.PutAsync(TableName, GoodRow, new PutOptions
                {
                    Namespace = "invalid"
                }));

            // full table name specified
            var result = await client.PutAsync(FullTableName, GoodRow);
            Assert.IsTrue(result.Success);
            // full table name overrides invalid namespace in config
            result = await invalidNsClient.PutAsync(FullTableName, GoodRow);
            Assert.IsTrue(result.Success);
            // valid namespace in config
            result = await nsClient.PutAsync(TableName, GoodRow);
            Assert.IsTrue(result.Success);
            // valid namespace in options
            result = await client.PutAsync(TableName, GoodRow, new PutOptions
            {
                Namespace = NamespaceName
            });
            Assert.IsTrue(result.Success);
            // valid namespace in options overrides invalid one in config
            result = await invalidNsClient.PutAsync(TableName, GoodRow,
                new PutOptions
                {
                    Namespace = NamespaceName
                });
            Assert.IsTrue(result.Success);
        }

        // We will shorten the rest of DML tests to only focus on options

        [TestMethod]
        public async Task TestDeleteAsync()
        {
            await PutRowAsync(Fixture.Table, GoodRow);

            // invalid namespace in options overrides valid one in config
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                nsClient.DeleteAsync(TableName, GoodPK, new DeleteOptions
                {
                    Namespace = "invalid"
                }));

            // valid namespace in options
            var result = await client.DeleteAsync(TableName, GoodPK,
                new DeleteOptions
            {
                Namespace = NamespaceName
            });
            Assert.IsTrue(result.Success);
            await PutRowAsync(Fixture.Table, GoodRow);

            // valid namespace in options overrides invalid one in config
            result = await invalidNsClient.DeleteAsync(TableName, GoodPK,
                new DeleteOptions
                {
                    Namespace = NamespaceName
                });
            Assert.IsTrue(result.Success);
        }

        [TestMethod]
        public async Task TestDeleteRangeAsync()
        {
            await PutRowsAsync(Fixture.Table, Fixture.Rows);

            // invalid namespace in options overrides valid one in config
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                nsClient.DeleteRangeAsync(TableName, GoodPartialPK,
                    new DeleteRangeOptions
                    {
                        Namespace = "invalid"
                    }));

            // full table name overrides invalid namespace in config
            var result = await invalidNsClient.DeleteRangeAsync(FullTableName,
                GoodPartialPK);
            Assert.IsTrue(result.DeletedCount > 0);

            await PutRowsAsync(Fixture.Table, Fixture.Rows);

            // valid namespace in options
            result = await client.DeleteRangeAsync(TableName,
                GoodPartialPK, new DeleteRangeOptions
                {
                    Namespace = NamespaceName
                });
            Assert.IsTrue(result.DeletedCount > 0);

            await PutRowsAsync(Fixture.Table, Fixture.Rows);

            // valid namespace in options overrides invalid one in config
            result = await invalidNsClient.DeleteRangeAsync(TableName,
                GoodPartialPK, new DeleteRangeOptions
                {
                    Namespace = NamespaceName
                });
            Assert.IsTrue(result.DeletedCount > 0);
        }

        [TestMethod]
        public async Task TestWriteManyAsync()
        {
            await PutRowAsync(Fixture.Table, GoodRow);

            var woc = new WriteOperationCollection().AddPut(GoodRow);

            // invalid namespace in options overrides valid one in config
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                nsClient.WriteManyAsync(TableName, woc, new WriteManyOptions
                {
                    Namespace = "invalid"
                }));
            // invalid namespace in options overrides valid one in config
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                nsClient.PutManyAsync(TableName, new [] { GoodRow },
                    new PutManyOptions
                {
                    Namespace = "invalid"
                }));
            // invalid namespace in options overrides valid one in config
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                nsClient.DeleteManyAsync(TableName, new [] { GoodPK },
                    new DeleteManyOptions
                    {
                        Namespace = "invalid"
                    }));

            // full table name overrides invalid namespace in config
            var result = await invalidNsClient.WriteManyAsync(FullTableName,
                woc);
            Assert.IsNotNull(result.Results);

            // valid namespace in options
            result = await client.WriteManyAsync(TableName, woc,
                new WriteManyOptions
                {
                    Namespace = NamespaceName
                });
            Assert.IsNotNull(result.Results);
            // valid namespace in options
            result = await client.PutManyAsync<RecordValue>(TableName,
                new[] { GoodRow }, new PutManyOptions
                {
                    Namespace = NamespaceName
                });
            Assert.IsNotNull(result.Results);
            // valid namespace in options
            result = await client.DeleteManyAsync<RecordValue>(TableName,
                new[] { GoodPK }, new DeleteManyOptions
                {
                    Namespace = NamespaceName
                });
            Assert.IsNotNull(result.Results);

            // valid namespace in options overrides invalid one in config
            result = await invalidNsClient.WriteManyAsync(TableName, woc,
                new WriteManyOptions
                {
                    Namespace = NamespaceName
                });
            Assert.IsNotNull(result.Results);
            // valid namespace in options overrides invalid one in config
            result = await invalidNsClient.PutManyAsync<RecordValue>(
                TableName, new[] { GoodRow }, new PutManyOptions
                {
                    Namespace = NamespaceName
                });
            Assert.IsNotNull(result.Results);
            // valid namespace in options overrides invalid one in config
            result = await invalidNsClient.DeleteManyAsync<RecordValue>(
                TableName, new[] { GoodPK }, new DeleteManyOptions
                {
                    Namespace = NamespaceName
                });
            Assert.IsNotNull(result.Results);

            await PutRowAsync(Fixture.Table, GoodRow);

            woc.Clear();
            woc.AddPut(TableName, GoodRow);
            
            // WriteManyAsync overload without table name parameter
            // valid namespace in options
            result = await client.WriteManyAsync(woc, new WriteManyOptions
            {
                Namespace = NamespaceName
            });
            Assert.IsNotNull(result.Results);

            woc.Clear();
            woc.AddPut(GoodRow, new PutOptions
            {
                Namespace = "invalid"
            });
            // namespace in sub-operation options should have no effect
            result = await client.WriteManyAsync(TableName, woc,
                new WriteManyOptions
                {
                    Namespace = NamespaceName
                });
            Assert.IsNotNull(result.Results);
        }

        [TestMethod]
        public async Task TestQueryAsync()
        {
            await PutRowsAsync(Fixture.Table, Fixture.Rows);

            var sql = $"SELECT * FROM {TableName}";
            var sql2 = $"SELECT * FROM {FullTableName}";

            // namespace not specified
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.PrepareAsync(sql));
            // namespace not specified
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.QueryAsync(sql));
            // invalid namespace in config
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                invalidNsClient.PrepareAsync(sql));
            // invalid namespace in config
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                invalidNsClient.QueryAsync(sql));
            // invalid namespace in options
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.PrepareAsync(sql, new PrepareOptions
                {
                    Namespace = "invalid"
                }));
            // invalid namespace in options
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.QueryAsync(sql, new QueryOptions
                {
                    Namespace = "invalid"
                }));
            // invalid namespace in options overrides valid one in config
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                nsClient.PrepareAsync(sql, new PrepareOptions
                {
                    Namespace = "invalid"
                }));
            // invalid namespace in options overrides valid one in config
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                nsClient.QueryAsync(sql, new QueryOptions
                {
                    Namespace = "invalid"
                }));

            // In the following test case the server still returns an error:
            // Table YevTestAllTypes not found.  Perhaps a bug?

            // full table name overrides invalid namespace in config
            // var prepStmt = await invalidNsClient.PrepareAsync(sql2);
            // Assert.IsNotNull(prepStmt);
            // var result = await invalidNsClient.QueryAsync(sql2);
            // Assert.IsNotNull(result.Rows);

            // valid namespace in config
            var prepStmt = await nsClient.PrepareAsync(sql);
            Assert.IsNotNull(prepStmt);
            var result = await nsClient.QueryAsync(sql);
            Assert.IsNotNull(result.Rows);
            // valid namespace in options
            prepStmt = await client.PrepareAsync(sql, new PrepareOptions
            {
                Namespace = NamespaceName
            });
            Assert.IsNotNull(prepStmt);
            result = await client.QueryAsync(sql, new QueryOptions
            {
                Namespace = NamespaceName
            });
            Assert.IsNotNull(result.Rows);
            // valid namespace in options overrides invalid one in config
            prepStmt = await invalidNsClient.PrepareAsync(sql,
                new PrepareOptions
                {
                    Namespace = NamespaceName
                });
            Assert.IsNotNull(prepStmt);
            result = await invalidNsClient.QueryAsync(sql, new QueryOptions
            {
                Namespace = NamespaceName
            });
            Assert.IsNotNull(result.Rows);
        }

        [TestMethod]
        public async Task TestTableDDLAsync()
        {
            var ddl = $"ALTER TABLE {TableName} USING TTL 5 days";
            var ddl2 = $"ALTER TABLE {FullTableName} USING TTL 5 days";

            // Below, the server is returning ILLEGAL_ARGUMENT
            // (ArgumentException), even though it should be returning
            // TABLE_NOT_FOUND (TableNotFoundException), so for now we don't
            // check exact exception type.

            // namespace not specified
            await AssertThrowsDerivedAsync<Exception>(() =>
                client.ExecuteTableDDLWithCompletionAsync(ddl));
            // invalid namespace in config
            await AssertThrowsDerivedAsync<Exception>(() =>
                invalidNsClient.ExecuteTableDDLWithCompletionAsync(ddl));
            // invalid namespace in options
            await AssertThrowsDerivedAsync<Exception>(() =>
                client.ExecuteTableDDLWithCompletionAsync(ddl,
                    new TableDDLOptions
                    {
                        Namespace = "invalid"
                    }));
            // invalid namespace in options overrides valid one in config
            await AssertThrowsDerivedAsync<Exception>(() =>
                nsClient.ExecuteTableDDLWithCompletionAsync(ddl,
                    new TableDDLOptions
                    {
                        Namespace = "invalid"
                    }));

            // In the following test case the server still returns an error:
            // Table "invalid:YevTestAllTypes" not found.  Perhaps a bug?

            // full table name overrides invalid namespace in config
            // var result = await invalidNsClient
            //     .ExecuteTableDDLWithCompletionAsync(ddl2);
            // Assert.AreEqual(NamespaceName, result.Namespace);

            // valid namespace in config
            var result = await nsClient.ExecuteTableDDLWithCompletionAsync(ddl);
            Assert.AreEqual(NamespaceName, result.Namespace);
            // valid namespace in options
            result = await client.ExecuteTableDDLWithCompletionAsync(ddl,
                new TableDDLOptions
                {
                    Namespace = NamespaceName
                });
            Assert.AreEqual(NamespaceName, result.Namespace);
            // valid namespace in options overrides invalid one in config
            result = await invalidNsClient.ExecuteTableDDLWithCompletionAsync(
                ddl, new TableDDLOptions
                {
                    Namespace = NamespaceName
                });
            Assert.AreEqual(NamespaceName, result.Namespace);
        }

        [TestMethod]
        public async Task TestCreateTableAsync()
        {
            var tableName = "TestTable";
            var fullTableName = $"{NamespaceName}:{tableName}";

            var result = await nsClient.ExecuteTableDDLAsync(
                $"CREATE TABLE {tableName}(col1 LONG, PRIMARY KEY(col1))");
            await result.WaitForCompletionAsync();
            Assert.AreEqual(NamespaceName, result.Namespace);
            Assert.AreEqual(fullTableName, result.TableName);

            await client.ExecuteTableDDLWithCompletionAsync(
                $"DROP TABLE {tableName}", new TableDDLOptions
                {
                    Namespace = NamespaceName
                });
        }

        [TestMethod]
        public async Task TestGetTableAsync()
        {
            // namespace not specified
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.GetTableAsync(TableName));
            // invalid namespace in config
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                invalidNsClient.GetTableAsync(TableName));
            // invalid namespace in options
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.GetTableAsync(TableName, new GetTableOptions
                {
                    Namespace = "invalid"
                }));
            // invalid namespace in options overrides valid one in config
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                nsClient.GetTableAsync(TableName, new GetTableOptions
                {
                    Namespace = "invalid"
                }));

            // full table name specified
            var result = await client.GetTableAsync(FullTableName);
            Assert.AreEqual(FullTableName, result.TableName);
            // full table name overrides invalid namespace in config
            result = await invalidNsClient.GetTableAsync(FullTableName);
            Assert.AreEqual(FullTableName, result.TableName);
            // valid namespace in config
            result = await nsClient.GetTableAsync(TableName);
            Assert.AreEqual(FullTableName, result.TableName);
            // valid namespace in options
            result = await client.GetTableAsync(TableName, new GetTableOptions
            {
                Namespace = NamespaceName
            });
            Assert.AreEqual(FullTableName, result.TableName);
            // valid namespace in options overrides invalid one in config
            result = await invalidNsClient.GetTableAsync(TableName,
                new GetTableOptions
                {
                    Namespace = NamespaceName
                });
            Assert.AreEqual(FullTableName, result.TableName);
        }

    }
}
