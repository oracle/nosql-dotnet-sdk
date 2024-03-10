/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
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
    public class GetIndexTests : TablesTestBase<GetIndexTests>
    {
        private static readonly TableInfo Table = SimpleTable;
        // sort our indexes by name
        private static readonly IndexInfo[] Indexes =
            SimpleTableIndexes.OrderBy(index => index.Name).ToArray();
        private static readonly string[] BadIndexNames = BadTableNames;

        private static readonly IEnumerable<GetIndexOptions>
            BadGetIndexOptions =
                from timeout in BadTimeSpans
                select new GetIndexOptions
                {
                    Timeout = timeout
                };

        private static IEnumerable<object[]> GetIndexesNegativeDataSource =>
            (from tableName in BadTableNames
             select new object[]
             {
                    tableName,
                    null
             })
            .Concat(
                from opt in BadGetIndexOptions
                select new object[]
                {
                    Table.Name,
                    opt
                });

        private static IEnumerable<object[]> GetIndexNegativeDataSource =>
            (from tableName in BadTableNames
             select new object[]
             {
                    tableName,
                    Indexes[0].Name,
                    null
             })
            .Concat(
                from indexName in BadIndexNames
                select new object[]
                {
                        Table.Name,
                        indexName,
                        null
                })
                .Concat(
                from opt in BadGetIndexOptions
                select new object[]
                {
                        Table.Name,
                        Indexes[0].Name,
                        opt
                });

        [ClassInitialize]
        public static async Task ClassInitializeAsync(TestContext testContext)
        {
            ClassInitialize(testContext);
            await CreateTableAsync(Table, Indexes);
        }

        [ClassCleanup]
        public static async Task ClassCleanupAsync()
        {
            await DropTableAsync(Table);
            ClassCleanup();
        }

        [DataTestMethod]
        [DynamicData(nameof(GetIndexesNegativeDataSource))]
        public async Task TestGetIndexesNegativeAsync(string tableName,
            GetIndexOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.GetIndexesAsync(tableName, options));
        }

        [DataTestMethod]
        [DynamicData(nameof(GetIndexNegativeDataSource))]
        public async Task TestGetIndexNegativeAsync(string tableName,
            string indexName, GetIndexOptions options)
        {
            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                client.GetIndexAsync(tableName, indexName, options));
        }

        [TestMethod]
        public async Task TestGetIndexesNonExistentTableAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.GetIndexesAsync("noSuchTable"));
        }

        [TestMethod]
        public async Task TestGetIndexNonExistentTableAsync()
        {
            await Assert.ThrowsExceptionAsync<TableNotFoundException>(() =>
                client.GetIndexAsync("noSuchTable", Indexes[0].Name));
        }

        [TestMethod]
        public async Task TestGetIndexNonExistentIndexAsync()
        {
            await Assert.ThrowsExceptionAsync<IndexNotFoundException>(() =>
                client.GetIndexAsync(Table.Name, "noSuchIndex",
                    new GetIndexOptions
                    {
                        Timeout = TimeSpan.FromSeconds(5)
                    }));
        }

        private static void VerifyIndex(IndexResult result, IndexInfo index)
        {
            Assert.IsNotNull(result);
            Assert.AreEqual(index.Name, result.IndexName);
            AssertDeepEqual(index.FieldNames, result.Fields);

            if (IsProtocolV4OrAbove)
            {
                if (result.FieldTypes == null)
                {
                    Assert.IsNull(index.FieldTypes);
                    return;
                }

                // Verify JSON typed index fields if any.
                Assert.AreEqual(index.FieldNames.Length, result.FieldTypes.Count);
                for (var i = 0; i < index.FieldNames.Length; i++)
                {
                    var expected = index.FieldTypes?[i];
                    var actual = result.FieldTypes[i];
                    if (actual == null)
                    {
                        Assert.IsNull(expected);
                        continue;
                    }

                    Assert.IsNotNull(expected);
                    Assert.AreEqual(expected.ToUpper(), actual.ToUpper());
                }
            }
        }

        private static void VerifyIndexes(IReadOnlyList<IndexResult> result,
            IndexInfo[] indexes)
        {
            Assert.IsNotNull(result);
            Assert.AreEqual(indexes.Length, result.Count);

            // indexes should already be sorted by name
            var resultIndexes =
                result.OrderBy(index => index.IndexName).ToList();

            for (var i = 0; i < indexes.Length; i++)
            {
                VerifyIndex(resultIndexes[i], indexes[i]);
            }
        }

        private static readonly GetIndexOptions[] GoodGetIndexOptions =
        {
            null,
            new GetIndexOptions(),
            new GetIndexOptions
            {
                Timeout = TimeSpan.FromMilliseconds(10001)
            }
        };

        private static IEnumerable<object[]>
            TestGetIndexesPositiveDataSource =>
            from opt in GoodGetIndexOptions select new object[] { opt };

        [DataTestMethod]
        [DynamicData(nameof(TestGetIndexesPositiveDataSource))]
        public async Task TestGetIndexesPositiveAsync(GetIndexOptions options)
        {
            var result = await client.GetIndexesAsync(Table.Name, options);
            VerifyIndexes(result, Indexes);
        }

        private static IEnumerable<object[]> TestGetIndexPositiveDataSource =>
            from index in Indexes
            from opt in GoodGetIndexOptions
            select new object[] {index, opt};

        [DataTestMethod]
        [DynamicData(nameof(TestGetIndexPositiveDataSource))]
        public async Task TestGetIndexPositiveAsync(IndexInfo index,
            GetIndexOptions options)
        {
            var result = await client.GetIndexAsync(Table.Name, index.Name,
                options);
            VerifyIndex(result, index);
        }

    }

}
