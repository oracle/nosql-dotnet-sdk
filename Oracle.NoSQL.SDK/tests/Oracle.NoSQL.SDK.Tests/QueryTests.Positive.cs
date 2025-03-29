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
    using static TestSchemas;
    using static QueryUtils;

    public partial class QueryTests
    {
        // We assume one query execution per test-case.
        private List<RecordValue> rows = new List<RecordValue>();

        private readonly ConsumedCapacity cc = new ConsumedCapacity();

        private int iterationCount;

        private IReadOnlyList<int> deletedRowIdList;
        private IReadOnlyList<DataRow> updatedRowList;

        // Statement preparation read cost in KB.
        private const int PrepareReadKB = 2;

        private static void VerifyPrepareResult(
            PreparedStatement preparedStatement, PrepareOptions options)
        {
            Assert.IsNotNull(preparedStatement);
            VerifyConsumedCapacity(preparedStatement.ConsumedCapacity);
            if (!IsOnPrem)
            {
                Assert.IsTrue(preparedStatement.ConsumedCapacity.ReadKB > 0);
                Assert.IsTrue(
                    preparedStatement.ConsumedCapacity.ReadUnits > 0);
                Assert.AreEqual(0,
                    preparedStatement.ConsumedCapacity.WriteKB);
                Assert.AreEqual(0,
                    preparedStatement.ConsumedCapacity.WriteUnits);
            }
            Assert.IsNotNull(preparedStatement.ProxyStatement);
            Assert.IsTrue(preparedStatement.ProxyStatement.Length > 0);

            if (options != null && options.GetQueryPlan)
            {
                Assert.IsFalse(string.IsNullOrEmpty(
                    preparedStatement.QueryPlan));
            }

            if (IsExpectedProtocolV4OrAbove &&
                (options != null && options.GetResultSchema))
            {
                Assert.IsFalse(string.IsNullOrEmpty(
                    preparedStatement.ResultSchema));
            }
        }

        // Verify consumed capacity after query is finished. For advanced
        // queries, verifying consumed capacity after each iteration is not
        // reliable based on returned rows or updated rows, because the
        // driver's query engine could cache some results.  So we just add up
        // CC over all iterations for the query and verify the totals.
        private void VerifyTotalConsumedCapacity(QTestCase testCase,
            QueryOptions options)
        {
            Assert.IsTrue(cc.ReadKB >= rows.Count);

            var readUnits = rows.Count;
            if (IsAbsoluteConsistency || options != null &&
                options.Consistency == Consistency.Absolute)
            {
                readUnits *= 2;
            }
            Assert.IsTrue(cc.ReadUnits >= readUnits);

            if (testCase.UpdatedRowList != null)
            {
                Assert.IsTrue(cc.WriteKB >= testCase.UpdatedRowList.Count);
                Assert.IsTrue(cc.WriteUnits >= testCase.UpdatedRowList.Count);
            }
            else if (testCase.DeletedRowIds != null)
            {
                Assert.IsTrue(cc.WriteKB >= testCase.DeletedRowIdList.Count);
                Assert.IsTrue(
                    cc.WriteUnits >= testCase.DeletedRowIdList.Count);
            }
            else
            {
                Assert.AreEqual(0, cc.WriteKB);
                Assert.AreEqual(0, cc.WriteUnits);
            }
        }

        // For update and delete queries make sure that any rows, other than
        // the ones updated or deleted by the query, have not been touched.
        private async Task VerifyUnmodifiedRowsAsync()
        {
            var unmodifiedRows = (IEnumerable<DataRow>)Fixture.Rows;
            if (updatedRowList != null)
            {
                var rowSet = new HashSet<int>(
                    from row in updatedRowList select row.Id);
                unmodifiedRows = from row in Fixture.Rows
                    where !rowSet.Contains(row.Id)
                    select row;
            }
            else if (deletedRowIdList != null)
            {
                var rowSet = new HashSet<int>(deletedRowIdList);
                unmodifiedRows = from row in Fixture.Rows
                    where !rowSet.Contains(row.Id)
                    select row;
            }

            foreach (var row in unmodifiedRows)
            {
                var getResult = await client.GetAsync(Fixture.Table.Name,
                    MakePrimaryKey(Fixture.Table, row));
                Assert.IsNotNull(getResult);
                Assert.IsNotNull(getResult.Row);
                AssertDeepEqual(row.Version, getResult.Version, true);
            }
        }

        private async Task VerifyQueryResultAsync(
            QueryResult<RecordValue> result, QTest test, QTestCase testCase,
            QueryOptions options = null, bool isDirect = false)
        {
            Assert.IsNotNull(result);
            Assert.IsNotNull(result.Rows);

            rows.AddRange(result.Rows);
            Assert.IsTrue(rows.Count <= testCase.ExpectedRowList.Count);

            if (options?.Limit != null)
            {
                Assert.IsTrue(result.Rows.Count <= options.Limit);
            }

            VerifyConsumedCapacity(result.ConsumedCapacity);
            if (!IsOnPrem)
            {
                if (options?.MaxReadKB != null)
                {
                    var maxReadKB = options.MaxReadKB + Fixture.MaxRowKB;
                    // On the first query call of un-prepared queries we need
                    // to add the preparation cost (2KB).
                    if (isDirect && iterationCount == 0)
                    {
                        maxReadKB += PrepareReadKB;
                    }

                    Assert.IsTrue(result.ConsumedCapacity.ReadKB <= maxReadKB);
                }

                // The below expectation will not hold if secondary indexes
                // are present.
                if (options?.MaxWriteKB != null && Fixture.Indexes == null)
                {
                    var maxWriteKB = options.MaxWriteKB + Fixture.MaxRowKB;
                    Assert.IsTrue(
                        result.ConsumedCapacity.WriteKB <= maxWriteKB);
                }

                cc.Add(result.ConsumedCapacity);
            }

            iterationCount++;

            // We verify the rest once the query is finished.
            if (result.ContinuationKey != null)
            {
                return;
            }

            var resultType = test.ExpectedFields != null
                ? new RecordFieldType(test.ExpectedFields)
                : Fixture.Table.RecordType;
            VerifyResultRows(rows, testCase.ExpectedRowList, resultType,
                test.IsOrdered);

            if (!IsOnPrem)
            {
                VerifyTotalConsumedCapacity(testCase, options);
            }

            if (testCase.UpdatedRowList != null) // update query
            {
                var currentTime = DateTime.UtcNow;

                foreach (var row in testCase.UpdatedRowList)
                {
                    // Rough estimation of modification time, since this
                    // function is called right after the update query.
                    row.ModificationTime = currentTime;

                    // We update put time for new rows and if this query
                    // has updated TTL of the rows.  For existing updated rows
                    // without TTL update, the put time should be the original
                    // put time set by PutRowAsync().
                    if (Fixture.GetRow(row.Id, true) == null ||
                        test.UpdateTTL)
                    {
                        row.PutTime = currentTime;
                    }

                    var primaryKey = MakePrimaryKey(Fixture.Table, row);
                    var getResult = await client.GetAsync(Fixture.Table.Name,
                        primaryKey);

                    // This should verify all modifications, including TTL,
                    // but not the row version, since the query result doesn't
                    // tell us updated row versions.
                    VerifyGetResult(getResult, Fixture.Table, row,
                        skipVerifyVersion: true);
                }

                updatedRowList = testCase.UpdatedRowList;
            }
            else if (testCase.DeletedRowIds != null) // delete query
            {
                foreach (var rowId in testCase.DeletedRowIdList)
                {
                    var primaryKey = MakePrimaryKey(Fixture.Table,
                        Fixture.GetRow(rowId));
                    var getResult = await client.GetAsync(Fixture.Table.Name,
                        primaryKey);
                    // Verify that the row no longer exists.
                    VerifyGetResult(getResult, Fixture.Table, null);
                }

                deletedRowIdList = testCase.DeletedRowIdList;
            }

            if (test.IsUpdate)
            {
                await VerifyUnmodifiedRowsAsync();
            }
        }

        private static PrepareOptions[] PrepareOptions2Test => new[]
        {
            null,
            new PrepareOptions
            {
                Timeout = TimeSpan.FromMilliseconds(12002),
                Compartment = Compartment
            },
            new PrepareOptions
            {
                GetQueryPlan = true,
                GetResultSchema = true
            },
            new PrepareOptions
            {
                Timeout = TimeSpan.FromSeconds(8),
                GetQueryPlan = true
            },
            new PrepareOptions
            {
                Timeout = TimeSpan.FromSeconds(10),
                GetResultSchema = true
            }
        };

        private static IEnumerable<object[]> PrepareDataSource =>
            QTests().Select((test, index) => new object[]
            {
                test.SQL,
                PrepareOptions2Test[index % PrepareOptions2Test.Length]
            });

        private static IEnumerable<QueryOptions> GetQueryOptions(QTest test,
            QTestCase testCase)
        {
            yield return null;

            yield return new QueryOptions
            {
                Timeout = TimeSpan.FromSeconds(12)
            };

            yield return new QueryOptions
            {
                Consistency = Consistency.Absolute,
                Compartment = Compartment,
                Timeout = TimeSpan.FromMilliseconds(22222)
            };

            yield return new QueryOptions
            {
                Limit = Math.Max(1, testCase.ExpectedRowList.Count / 3)
            };

            yield return new QueryOptions
            {
                MaxReadKB = Fixture.MaxRowKB + 1
            };

            yield return new QueryOptions
            {
                Limit = 3,
                MaxReadKB = Fixture.MaxRowKB + 2
            };

            if (test.IsUpdate)
            {
                yield return new QueryOptions
                {
                    MaxWriteKB = Fixture.MaxRowKB + 1
                };

                if (IsExpectedProtocolV4OrAbove)
                {
                    yield return new QueryOptions
                    {
                        Durability = Durability.CommitSync
                    };
                }
            }

            if (test.MaxMemoryBytes != null)
            {
                yield return new TestQueryOptions
                {
                    MaxMemoryBytes = test.MaxMemoryBytes
                };
                yield return new QueryOptions
                {
                    MaxMemoryMB =
                        (int)((test.MaxMemoryBytes + 0xfffff) / 0x100000),
                    Limit = Math.Max(1, testCase.ExpectedRowList.Count / 4),
                    Timeout = TimeSpan.FromSeconds(15)
                };
            }
        }

        private static IEnumerable<object[]> DirectQueryDataSource =>
            from test in QTests()
            where test.TestCases[0].Bindings == null
            let testCase = test.TestCases[0]
            from opt in GetQueryOptions(test, testCase)
            select new object[]
            {
                test,
                testCase,
                opt
            };

        private static PreparedStatement PrepareSync(string sql) =>
            Task.Run(() => client.PrepareAsync(sql)).Result;

        private static PreparedStatement BindStatement(PreparedStatement preparedStatement,
            QTestCase testCase)
        {
            var result = preparedStatement.CopyStatement();
            if (testCase.Bindings != null)
            {
                foreach (var kv in testCase.Bindings)
                {
                    result.Variables.Add(kv.Key, kv.Value);
                }
            }

            return result;
        }

        private static IEnumerable<object[]> PreparedQueryDataSource =>
            from test in QTests()
            let preparedStatement = PrepareSync(test.SQL)
            from testCase in test.TestCases
            let boundStatement = BindStatement(preparedStatement, testCase)
            from opt in GetQueryOptions(test, testCase)
            select new object[]
            {
                test,
                boundStatement,
                testCase,
                opt
            };

        [TestCleanup]
        public async Task TestCleanupAsync()
        {
            if (updatedRowList != null)
            {
                foreach (var row in updatedRowList)
                {
                    var originalRow = Fixture.GetRow(row.Id, true);
                    if (originalRow != null)
                    {
                        // This was update, we restore the original row.
                        await PutRowAsync(Fixture.Table, originalRow);
                    }
                    else
                    {
                        // This was insert, we delete the new row.
                        await DeleteRowAsync(Fixture.Table, row);
                    }
                }

                updatedRowList = null;
            }
            else if (deletedRowIdList != null)
            {
                foreach (var rowId in deletedRowIdList)
                {
                    // Restore deleted row.
                    await PutRowAsync(Fixture.Table, Fixture.GetRow(rowId));
                }

                deletedRowIdList = null;
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(PrepareDataSource))]
        public async Task TestPrepareAsync(string sql, PrepareOptions options)
        {
            var result = await client.PrepareAsync(sql, options);
            VerifyPrepareResult(result, options);
        }

        [DataTestMethod]
        [DynamicData(nameof(DirectQueryDataSource))]
        public async Task TestDirectQueryAsync(QTest test, QTestCase testCase,
            QueryOptions options)
        {
            options ??= new QueryOptions();
            do
            {
                var result = await client.QueryAsync(test.SQL, options);
                await VerifyQueryResultAsync(result, test, testCase, options,
                    true);
                options.ContinuationKey = result.ContinuationKey;
            } while (options.ContinuationKey != null);
        }

        [DataTestMethod]
        [DynamicData(nameof(DirectQueryDataSource))]
        public async Task TestDirectQueryAsyncEnumerableAsync(QTest test,
            QTestCase testCase, QueryOptions options)
        {
            var enumerable = client.GetQueryAsyncEnumerable(test.SQL,
                options);
            await foreach (var result in enumerable)
            {
                await VerifyQueryResultAsync(result, test, testCase, options,
                    true);
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(PreparedQueryDataSource))]
        public async Task TestPreparedQueryAsync(QTest test,
            PreparedStatement preparedStatement, QTestCase testCase,
            QueryOptions options)
        {
            options ??= new QueryOptions();
            do
            {
                var result = await client.QueryAsync(preparedStatement,
                    options);
                await VerifyQueryResultAsync(result, test, testCase, options,
                    true);
                options.ContinuationKey = result.ContinuationKey;
            } while (options.ContinuationKey != null);
        }

        [DataTestMethod]
        [DynamicData(nameof(PreparedQueryDataSource))]
        public async Task TestPreparedQueryAsyncEnumerableAsync(QTest test,
            PreparedStatement preparedStatement, QTestCase testCase,
            QueryOptions options)
        {
            var enumerable = client.GetQueryAsyncEnumerable(preparedStatement,
                options);
            await foreach (var result in enumerable)
            {
                await VerifyQueryResultAsync(result, test, testCase, options,
                    true);
            }
        }

    }
}
