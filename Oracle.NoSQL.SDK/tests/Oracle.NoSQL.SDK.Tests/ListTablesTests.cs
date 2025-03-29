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
    public class ListTablesTests : TablesTestBase<ListTablesTests>
    {
        // The ordering of table names from ListTables is alphabetic but
        // case-insensitive.
        private static readonly string[] Names =
        {
            "8", "a1", "A5", "abc1", "Abc2"
        };

        private static readonly TableInfo[] Tables =
            (from name in Names
                select GetSimpleTableWithName($"{TableNamePrefix}tbl{name}"))
            .ToArray();

        private static readonly string[] TableNames =
            (from table in Tables select table.Name).ToArray();

        private const string NamespaceName = TableNamePrefix + "spca1";
        private const int NamespaceTableCount = 2;

        private static readonly TableInfo[] NamespaceTables =
            (from table in Tables
                select GetSimpleTableWithName(
                    $"{NamespaceName}:{table.Name}"))
            .Take(NamespaceTableCount)
            .ToArray();

        private static readonly string[] NamespaceTableNames =
            (from table in NamespaceTables select table.Name).ToArray();

        private static readonly IEnumerable<ListTablesOptions>
            BadListTablesOptions =
                (from timeout in BadTimeSpans
                    select new ListTablesOptions
                    {
                        Timeout = timeout
                    })
                .Append(new ListTablesOptions
                {
                    FromIndex = -20
                })
                .Concat(from limit in BadPositiveInt32
                    select new ListTablesOptions
                    {
                        Limit = limit
                    });

        private static IEnumerable<object[]> ListTablesNegativeDataSource =>
            from opt in BadListTablesOptions select new object[] {opt};

        [ClassInitialize]
        public static async Task ClassInitializeAsync(TestContext testContext)
        {
            ClassInitialize(testContext);

            foreach (var table in Tables)
            {
                await CreateTableAsync(table);
            }

            if (IsOnPrem)
            {
                await client.ExecuteAdminWithCompletionAsync(
                    $"DROP NAMESPACE IF EXISTS {NamespaceName} CASCADE");
                await client.ExecuteAdminWithCompletionAsync(
                    $"CREATE NAMESPACE {NamespaceName}");
                foreach (var table in NamespaceTables)
                {
                    await CreateTableAsync(table);
                }
            }
        }

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            foreach (var table in Tables)
            {
                await DropTableAsync(table);
            }

            if (IsOnPrem)
            {
                await client.ExecuteAdminWithCompletionAsync(
                    $"DROP NAMESPACE {NamespaceName} CASCADE");
            }

            ClassCleanup();
        }

        [DataTestMethod]
        [DynamicData(nameof(ListTablesNegativeDataSource))]
        public async Task TestListTablesNegative(ListTablesOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.ListTablesAsync(options));
        }

        [DataTestMethod]
        [DynamicData(nameof(ListTablesNegativeDataSource))]
        public void TestListTablesAsyncEnumerableNegative(
            ListTablesOptions options)
        {
            AssertThrowsDerived<ArgumentException>(() =>
                client.GetListTablesAsyncEnumerable(options));
        }

        // The tests below allow if there are extra tables in the system not
        // created by the test, but assume that the tables don't get created
        // or dropped while the test runs.

        [TestMethod]
        public async Task TestListTablesAsync()
        {
            var result = await client.ListTablesAsync();
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.TableNames);

            var names = result.TableNames.Where(name => TableNames.Contains(
                name, StringComparer.OrdinalIgnoreCase)).ToArray();
            AssertDeepEqual(TableNames, names);

            Assert.AreEqual(result.TableNames.Count, result.NextIndex);

            var result2 = await client.ListTablesAsync(new ListTablesOptions
            {
                Timeout = TimeSpan.FromSeconds(20)
            });
            AssertDeepEqual(result, result2);
        }

        [TestMethod]
        public async Task TestListTablesInNamespaceAsync()
        {
            CheckOnPrem();

            var result = await client.ListTablesAsync(NamespaceName);
            Assert.IsNotNull(result);
            AssertDeepEqual(NamespaceTableNames, result.TableNames.ToArray());
            Assert.AreEqual(result.TableNames.Count, result.NextIndex);

            var result2 = await client.ListTablesAsync(new ListTablesOptions
            {
                Timeout = TimeSpan.FromMilliseconds(20001),
                Namespace = NamespaceName
            });
            AssertDeepEqual(result, result2);
        }

        [TestMethod]
        public async Task TestListTablesWithLimitAsync()
        {
            var result = await client.ListTablesAsync();
            Assert.IsNotNull(result);

            var tableNames = result.TableNames;
            Assert.IsNotNull(tableNames);
            Assert.IsTrue(tableNames.Count >= TableNames.Length);

            var limit = tableNames.Count / 2;
            result = await client.ListTablesAsync(new ListTablesOptions
            {
                Limit = limit
            });
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.TableNames);
            AssertDeepEqual(tableNames.Take(limit).ToArray(),
                result.TableNames.ToArray());
            Assert.AreEqual(limit, result.NextIndex);

            result = await client.ListTablesAsync(new ListTablesOptions
            {
                Timeout = TimeSpan.FromSeconds(10),
                FromIndex = limit,
                Limit = limit + 5
            });
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.TableNames);
            AssertDeepEqual(
                tableNames.Skip(limit).Take(tableNames.Count - limit)
                    .ToArray(),
                result.TableNames.ToArray());
            Assert.AreEqual(tableNames.Count, result.NextIndex);
        }

        [TestMethod]
        public async Task TestListTablesAsyncEnumerableAsync()
        {
            var result1 = await client.ListTablesAsync();
            Assert.IsNotNull(result1);

            var tableNames = result1.TableNames.ToList();
            Assert.IsNotNull(tableNames);
            Assert.IsTrue(tableNames.Count >= TableNames.Length);

            var options = new ListTablesOptions
            {
                Limit = 2
            };

            var optionsCopy = options.Clone();

            var offset = 0;
            var resultNames = new List<string>();

            await foreach(var result in
                client.GetListTablesAsyncEnumerable(options))
            {
                Assert.IsNotNull(result);
                Assert.IsNotNull(result.TableNames);
                resultNames.AddRange(result.TableNames);
                Assert.AreEqual(offset + result.TableNames.Count,
                    result.NextIndex);
                offset = result.NextIndex;
                // Make sure the iterator does not change user's options
                // object.
                AssertDeepEqual(optionsCopy, options, true);
            }

            Assert.AreEqual(tableNames.Count, offset);
            AssertDeepEqual(tableNames, resultNames);
        }

        [TestMethod]
        public async Task TestListTablesAsyncEnumerableInNamespaceAsync()
        {
            CheckOnPrem();

            var result1 = await client.ListTablesAsync(NamespaceName);
            Assert.IsNotNull(result1);

            var tableNames = result1.TableNames.ToList();
            Assert.IsNotNull(tableNames);
            Assert.AreEqual(NamespaceTableNames.Length, tableNames.Count);

            var options = new ListTablesOptions
            {
                Namespace = NamespaceName,
                Limit = 1
            };

            var resultNames = new List<string>();

            await foreach (var result in
                client.GetListTablesAsyncEnumerable(options))
            {
                Assert.IsNotNull(result);
                Assert.IsNotNull(result.TableNames);
                Assert.AreEqual(1, result.TableNames.Count);
                resultNames.Add(result.TableNames[0]);
            }

            AssertDeepEqual(tableNames, resultNames);
        }

    }

}
