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
    using static TestTables;

    [TestClass]
    public partial class QueryTests : DataTestBase<QueryTests>
    {
        private static readonly AllTypesRowFactory RowFactory =
            new AllTypesRowFactory();

        private static readonly DataTestFixture Fixture = new DataTestFixture(
            AllTypesTable, RowFactory, 20);

        private static readonly IEnumerable<string> BadSQLStatements =
            BadNonEmptyStrings.Append("SELECT BLAH BLAH"); // invalid SQL

        private static readonly IEnumerable<PrepareOptions>
            BadPrepareOptions =
            from timeout in BadTimeSpans
            select new PrepareOptions
            {
                Timeout = timeout
            };

        private static readonly string GoodStatement4Prepare =
            "DECLARE $shardId INTEGER; $pkString STRING; SELECT * FROM " +
            $"{Fixture.Table.Name} WHERE shardId = $shardId AND " +
            "pkString = $pkString";

        private const string NonExistentTableStatement =
            "SELECT * FROM no_such_table";

        private static PreparedStatement goodPreparedStatement;

        private static IEnumerable<object[]> PrepareNegativeDataSource =>

            (from badSQLStmt in BadSQLStatements
                select new object[]
                {
                    badSQLStmt,
                    null
                })
            .Concat(from badOpt in BadPrepareOptions
                select new object[]
                {
                    GoodStatement4Prepare,
                    badOpt
                });

        private static
            IEnumerable<IEnumerable<KeyValuePair<string, FieldValue>>>
            BadBindings =>
            Enumerable.Empty<IEnumerable<KeyValuePair<string, FieldValue>>>()
                .Append(new MapValue()) // empty bindings
                .Concat(
                    from badVarName in BadNonEmptyStrings
                    select new[]
                    {
                        new KeyValuePair<string, FieldValue>(badVarName, 1)
                    })
                .Append(new MapValue
                {
                    ["$shardId"] = 0 // missing binding for pkString
                })
                .Append(new MapValue
                {
                    ["$pkString"] = "a" // missing binding for shardId
                })
                .Append(new MapValue
                {
                    ["$shardId"] = 0,
                    ["$pkString"] = null // null value in primary key
                })
                .Append(new MapValue
                {
                    ["$shardId"] = 0,
                    ["$pkString"] = "a",
                    ["$no_such_variable"] = "n" // undeclared variable
                })
                .Append(new MapValue
                {
                    ["$shardId"] = 0,
                    ["$pkString"] = new ArrayValue {"abc"}
                });

        private static IEnumerable<object[]> BindingsNegativeDataSource =>
            from binding in BadBindings select new object[] {binding};

        private const int MaxReadKBLimit = 2 * 1024;
        private const int MaxWriteKBLimit = 2 * 1024;

        private static IEnumerable<QueryOptions> BadDriverQueryOptions =>
            (from timeout in BadTimeSpans
                select new QueryOptions
                {
                    Timeout = timeout
                })
            .Concat(
                from consistency in BadConsistencies
                select new QueryOptions
                {
                    Consistency = consistency
                })
            .Concat(
                from durability in BadDurabilities
                select new QueryOptions
                {
                    Durability = durability
                })
            .Concat(
                from limit in BadPositiveInt32
                select new QueryOptions
                {
                    Limit = limit
                })
            .Concat(
                from maxReadKB in BadPositiveInt32
                select new QueryOptions
                {
                    MaxReadKB = maxReadKB
                })
            .Concat(
                from maxWriteKB in BadPositiveInt32
                select new QueryOptions
                {
                    MaxReadKB = maxWriteKB
                })
            .Concat(
                from maxMemoryMB in BadPositiveInt32
                select new QueryOptions
                {
                    MaxMemoryMB = maxMemoryMB
                });

        private static IEnumerable<QueryOptions> BadQueryOptions =>
            BadDriverQueryOptions
                .Append(new QueryOptions
                {
                    MaxReadKB = MaxReadKBLimit + 1
                })
                .Append(new QueryOptions
                {
                    MaxWriteKB = MaxWriteKBLimit + 1
                });

        private static IEnumerable<object[]>
            PreparedQueryDriverNegativeDataSource =>
            Enumerable.Empty<object[]>()
            .Append(new object[]
            {
                null,
                null
            })
            .Concat(
                from badOpt in BadDriverQueryOptions
                select new object[]
                {
                    goodPreparedStatement,
                    badOpt
                });

        private static IEnumerable<object[]>
            PreparedQueryNegativeDataSource => Enumerable.Empty<object[]>()
            .Append(new object[]
            {
                null,
                null
            })
            .Append(new object[]
            {
                null,
                new QueryOptions()
            })
            .Concat(
                from badOpt in BadQueryOptions
                select new object[]
                {
                    goodPreparedStatement,
                    badOpt
                });

        private static IEnumerable<object[]> QueryDriverNegativeDataSource =>
            Enumerable.Empty<object[]>()
                .Append(new object[]
                {
                    null,
                    null
                })
                .Append(new object[]
                {
                    null,
                    new QueryOptions()
                })
                .Concat(
                    from badOpt in BadDriverQueryOptions
                    select new object[]
                    {
                        GoodStatement4Prepare,
                        badOpt
                    });

        private static IEnumerable<object[]> QueryNegativeDataSource =>
            Enumerable.Empty<object[]>()
            .Concat(
                from badStmt in BadSQLStatements
                select new object[]
                {
                    badStmt,
                    null
                })
            .Concat(
                from badOpt in BadQueryOptions
                select new object[]
                {
                    GoodStatement4Prepare,
                    badOpt
                });

        private static async Task DoMaxMemoryNegativeAsync(Func<Task> testFunc,
            long maxMemory)
        {
            try
            {
                await testFunc();
                Assert.Fail(
                    "This query should fail when run with memory limit of " +
                    $"{maxMemory} bytes");
            }
            catch (Exception ex)
            {
                Assert.IsTrue(ex is InvalidOperationException);
                Assert.IsNotNull(ex.Message);
                Assert.IsTrue(ex.Message.ToLower().Contains("memory"));
            }
        }

        // For now use only one options and one testCase for each query, these
        // can be expanded in future.
        private static IEnumerable<object[]>
            DirectQueryMaxMemoryNegativeDataSource =>
            from test in QTests() where test.MaxMemoryBytesFail.HasValue
            where test.TestCases[0].Bindings == null
            let testCase = test.TestCases[0]
            select new object[]
            {
                test,
                testCase,
                new TestQueryOptions
                {
                    MaxMemoryBytes = test.MaxMemoryBytesFail
                }
            };

        private static IEnumerable<object[]>
            PreparedQueryMaxMemoryNegativeDataSource =>
            from test in QTests() where test.MaxMemoryBytesFail.HasValue
            let testCase = test.TestCases[0]
            let boundStatement = BindStatement(PrepareSync(test.SQL),
                testCase)
            select new object[]
            {
                test,
                boundStatement,
                testCase,
                new TestQueryOptions
                {
                    MaxMemoryBytes = test.MaxMemoryBytesFail
                }
            };

        [ClassInitialize]
        public static async Task ClassInitializeAsync(TestContext testContext)
        {
            ClassInitialize(testContext);
            await DropTableAsync(Fixture.Table);
            await CreateTableAsync(Fixture.Table);
            await PutRowsAsync(Fixture.Table, Fixture.Rows);

            goodPreparedStatement = await client.PrepareAsync(
                GoodStatement4Prepare);
        }

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            await DropTableAsync(Fixture.Table);
            ClassCleanup();
        }

        [DataTestMethod]
        [DynamicData(nameof(PrepareNegativeDataSource))]
        public async Task TestPrepareNegativeAsync(string stmt,
            PrepareOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.PrepareAsync(stmt, options));
        }

        [TestMethod]
        public async Task TestPrepareNonExistentTableAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.PrepareAsync(NonExistentTableStatement));
        }

        [DataTestMethod]
        [DynamicData(nameof(BindingsNegativeDataSource))]
        public async Task TestBindingsNegativeAsync(
            IEnumerable<KeyValuePair<string, FieldValue>> bindings)
        {
            goodPreparedStatement.Variables.Clear();
            await AssertThrowsDerivedAsync<ArgumentException>(async () =>
            {
                foreach (var binding in bindings)
                {
                    goodPreparedStatement.Variables.Add(binding.Key,
                        binding.Value);
                }
                await client.QueryAsync(goodPreparedStatement);
            });
        }

        [DataTestMethod]
        [DynamicData(nameof(PreparedQueryNegativeDataSource))]
        public async Task TestPreparedQueryNegativeAsync(
            PreparedStatement stmt, QueryOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.QueryAsync(stmt, options));
        }

        [DataTestMethod]
        [DynamicData(nameof(PreparedQueryDriverNegativeDataSource))]
        public void TestPreparedQueryGetQueryAsyncEnumerableNegative(
            PreparedStatement stmt, QueryOptions options)
        {
            AssertThrowsDerived<ArgumentException>(() =>
                client.GetQueryAsyncEnumerable(stmt, options));
        }

        [DataTestMethod]
        [DynamicData(nameof(PreparedQueryNegativeDataSource))]
        public async Task TestPreparedQueryAsyncEnumerableNegativeAsync(
            PreparedStatement stmt, QueryOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(async () =>
            {
                await foreach (var _ in client.GetQueryAsyncEnumerable(stmt,
                    options))
                {
                }
            });
        }

        [DataTestMethod]
        [DynamicData(nameof(QueryNegativeDataSource))]
        public async Task TestQueryNegativeAsync(string stmt,
            QueryOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.QueryAsync(stmt, options));
        }

        [TestMethod]
        public async Task TestQueryNonExistentTableAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.QueryAsync(NonExistentTableStatement));
        }

        [DataTestMethod]
        [DynamicData(nameof(QueryDriverNegativeDataSource))]
        public void TestQueryGetQueryAsyncEnumerableNegative(string stmt,
            QueryOptions options)
        {
            AssertThrowsDerived<ArgumentException>(() =>
                client.GetQueryAsyncEnumerable(stmt, options));
        }

        [DataTestMethod]
        [DynamicData(nameof(QueryNegativeDataSource))]
        public async Task TestQueryAsyncEnumerableNegativeAsync(string stmt,
            QueryOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(async () =>
            {
                await foreach (var _ in client.GetQueryAsyncEnumerable(stmt,
                    options))
                {
                }
            });
        }

        [TestMethod]
        public async Task TestQueryAsyncEnumerableNonExistentTableAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(async () =>
            {
                await foreach (var _ in client.GetQueryAsyncEnumerable(
                    NonExistentTableStatement, new QueryOptions()))
                {
                }
            });
        }

        [Ignore]
        [DataTestMethod]
        [DynamicData(nameof(DirectQueryMaxMemoryNegativeDataSource))]
        public async Task TestDirectQueryMaxMemoryNegativeAsync(QTest test,
            QTestCase testCase, QueryOptions options)
        {
            Assert.IsNotNull(test.MaxMemoryBytesFail); // test self-check
            await DoMaxMemoryNegativeAsync(
                () => TestDirectQueryAsync(test, testCase, options),
                test.MaxMemoryBytesFail.Value);
        }

        [Ignore]
        [DataTestMethod]
        [DynamicData(nameof(DirectQueryMaxMemoryNegativeDataSource))]
        public async Task
            TestDirectQueryAsyncEnumerableMaxMemoryNegativeAsync(QTest test,
            QTestCase testCase, QueryOptions options)
        {
            Assert.IsNotNull(test.MaxMemoryBytesFail); // test self-check
            await DoMaxMemoryNegativeAsync(
                () => TestDirectQueryAsyncEnumerableAsync(test, testCase,
                    options),
                test.MaxMemoryBytesFail.Value);
        }

        [Ignore]
        [DataTestMethod]
        [DynamicData(nameof(PreparedQueryMaxMemoryNegativeDataSource))]
        public async Task TestPreparedQueryMaxMemoryNegativeAsyncAsync(
            QTest test, PreparedStatement preparedStatement,
            QTestCase testCase, QueryOptions options)
        {
            Assert.IsNotNull(test.MaxMemoryBytesFail); // test self-check
            await DoMaxMemoryNegativeAsync(
                () => TestPreparedQueryAsync(test, preparedStatement,
                    testCase, options),
                test.MaxMemoryBytesFail.Value);
        }

        [Ignore]
        [DataTestMethod]
        [DynamicData(nameof(DirectQueryDataSource))]
        public async Task
            TestPreparedQueryAsyncEnumerableMaxMemoryNegativeAsync(QTest test,
            PreparedStatement preparedStatement, QTestCase testCase,
            QueryOptions options)
        {
            Assert.IsNotNull(test.MaxMemoryBytesFail); // test self-check
            await DoMaxMemoryNegativeAsync(
                () => TestPreparedQueryAsyncEnumerableAsync(test,
                    preparedStatement, testCase, options),
                test.MaxMemoryBytesFail.Value);
        }

    }
}
