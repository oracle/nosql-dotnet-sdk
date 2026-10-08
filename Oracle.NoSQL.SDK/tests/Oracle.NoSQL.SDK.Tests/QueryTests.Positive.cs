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

        [TestMethod]
        public async Task TestUnionAllSequentialContinuationAsync()
        {
            await VerifyUnionAllContinuationAsync(false);
        }

        [TestMethod]
        public async Task TestUnionAllSortedContinuationAsync()
        {
            await VerifyUnionAllContinuationAsync(true);
        }

        [TestMethod]
        public async Task TestUnionAllCaseExpressionAsync()
        {
            await CheckUnionSupportedAsync(Fixture.Table.Name);
            var source = $"SELECT colInteger FROM {Fixture.Table.Name}";
            var sql = "SELECT CASE WHEN $u.colInteger > 10 THEN " +
                "$u.colInteger ELSE 0 END AS value FROM (" + source +
                " UNION ALL " + source + ") $u";
            var statement = await client.PrepareAsync(sql);
            var rows = new List<RecordValue>();

            await foreach (var result in client.GetQueryAsyncEnumerable(
                statement))
            {
                rows.AddRange(result.Rows);
            }

            Assert.AreEqual(Fixture.Rows.Count() * 2, rows.Count);
            var expected = Fixture.Rows.Select(row =>
                row["colInteger"].IsNumeric && row["colInteger"].AsInt32 > 10
                    ? row["colInteger"].AsInt32 : 0)
                .ToArray();
            CollectionAssert.AreEquivalent(expected.Concat(expected).ToArray(),
                rows.Select(row => row["value"].AsInt32).ToArray());
        }

        [TestMethod]
        public async Task TestUnionAllTimestampCaseExpressionAsync()
        {
            await CheckUnionSupportedAsync(Fixture.Table.Name);
            const string earlier = "CAST('2026-01-01T00:00:00Z' AS TIMESTAMP(7))";
            const string later = "CAST('2026-01-01T00:00:00.1Z' AS TIMESTAMP(7))";
            var first = $"SELECT {earlier} AS ts FROM {Fixture.Table.Name}";
            var second = $"SELECT {later} AS ts FROM {Fixture.Table.Name}";
            // Global aggregates keep CASE above UNION in the driver plan;
            // a per-row CASE can be pushed down and hide a driver regression.
            var statement = await client.PrepareAsync(
                $"SELECT CASE WHEN min($u.ts) < {later} THEN 1 ELSE 0 END AS earlier, " +
                $"CASE WHEN max($u.ts) < {later} THEN 1 ELSE 0 END AS later " +
                $"FROM ({first} UNION ALL {second}) $u");
            StringAssert.Contains(Query.PlanFormatter.Format(
                statement.DriverQueryPlan), "CASE");
            var rows = await ReadUnionRowsAsync(statement);
            Assert.AreEqual(1, rows.Count);
            Assert.AreEqual(1, rows[0]["earlier"].AsInt32);
            Assert.AreEqual(0, rows[0]["later"].AsInt32);
        }

        [TestMethod]
        public async Task TestUnionAllGroupedCountAsync()
        {
            await CheckUnionSupportedAsync(Fixture.Table.Name);
            var select = $"SELECT shardId FROM {Fixture.Table.Name}";
            var statement = await client.PrepareAsync(
                "SELECT $u.shardId, count(*) AS total FROM (" + select +
                " UNION ALL " + select + ") $u GROUP BY $u.shardId " +
                "ORDER BY $u.shardId");
            var rows = await ReadUnionRowsAsync(statement);
            var expected = Fixture.Rows.GroupBy(row => row["shardId"].AsInt32)
                .OrderBy(group => group.Key).ToArray();
            Assert.AreEqual(expected.Length, rows.Count);
            for (var i = 0; i < rows.Count; i++)
            {
                Assert.AreEqual(expected[i].Key, rows[i]["shardId"].AsInt32);
                Assert.AreEqual(2L * expected[i].Count(), rows[i]["total"].ToInt64());
            }
        }

        [DataTestMethod]
        [DataRow(true, false, false)]
        [DataRow(false, true, false)]
        [DataRow(true, true, false)]
        [DataRow(true, false, true)]
        [DataRow(false, true, true)]
        [DataRow(true, true, true)]
        public async Task TestUnionAllEmptyBranchContinuationAsync(
            bool firstEmpty, bool secondEmpty, bool sorted)
        {
            await CheckUnionSupportedAsync(Fixture.Table.Name);
            var select = $"SELECT shardId, pkString FROM {Fixture.Table.Name}";
            // Bind predicates so an empty result is discovered at execution
            // time, not folded into a different plan during preparation.
            var first = select + " WHERE shardId < $firstLimit";
            var second = select + " WHERE shardId < $secondLimit";
            var sql = first + " UNION ALL " + second;
            if (sorted)
            {
                sql = "SELECT $u.shardId, $u.pkString FROM (" + sql +
                    ") $u ORDER BY $u.shardId, $u.pkString";
            }
            var statement = await client.PrepareAsync(
                "DECLARE $firstLimit INTEGER; $secondLimit INTEGER; " + sql);
            Assert.AreEqual(2, statement.QueryBranches.Count);
            var minShard = Fixture.Rows.Min(row => row["shardId"].AsInt32);
            var maxShard = Fixture.Rows.Max(row => row["shardId"].AsInt32) + 1;
            statement.SetVariable("$firstLimit", firstEmpty ? minShard : maxShard);
            statement.SetVariable("$secondLimit", secondEmpty ? minShard : maxShard);
            var rows = await ReadUnionRowsAsync(statement);
            var expected = firstEmpty && secondEmpty
                ? Array.Empty<string>()
                : Fixture.Rows.Select(row => row["pkString"].AsString).ToArray();
            CollectionAssert.AreEquivalent(expected,
                rows.Select(row => row["pkString"].AsString).ToArray());
        }

        private static async Task<List<RecordValue>> ReadUnionRowsAsync(
            PreparedStatement statement)
        {
            var rows = new List<RecordValue>();
            var options = new QueryOptions { Limit = 1 };
            var calls = 0;
            do
            {
                Assert.IsTrue(++calls <= 4 * Fixture.Rows.Count() + 50,
                    "UNION continuation did not terminate");
                var result = await client.QueryAsync(statement, options);
                rows.AddRange(result.Rows);
                options.ContinuationKey = result.ContinuationKey;
            } while (options.ContinuationKey != null);
            return rows;
        }

        private static async Task VerifyUnionAllContinuationAsync(
            bool sorted)
        {
            await CheckUnionSupportedAsync(Fixture.Table.Name);

            var select = sorted
                ? $"SELECT shardId, pkString FROM {Fixture.Table.Name}"
                : $"SELECT 1 AS branch, shardId, pkString FROM " +
                  Fixture.Table.Name;
            var secondSelect = sorted
                ? select
                : $"SELECT 2 AS branch, shardId, pkString FROM " +
                  Fixture.Table.Name;
            var union = select + " UNION ALL " + secondSelect;
            var sql = union;
            if (sorted)
            {
                // ORDER BY belongs to a UNION branch. Wrap the UNION in an
                // outer query to order the combined result.
                sql = "SELECT $u.shardId, $u.pkString FROM (" + union +
                    ") AS $u ORDER BY $u.shardId, $u.pkString";
            }

            var statement = await client.PrepareAsync(sql);
            var options = new QueryOptions
            {
                // Each call can issue only one proxy fetch. A one-row limit
                // exercises UNION branch switching and continuation state.
                Limit = 1
            };
            var rows = new List<RecordValue>();
            var queryCallCount = 0;

            do
            {
                Assert.IsTrue(queryCallCount < 4 * Fixture.Rows.Count() + 50,
                    "UNION continuation did not terminate");
                var result = await client.QueryAsync(statement, options);
                rows.AddRange(result.Rows);
                queryCallCount++;
                options.ContinuationKey = result.ContinuationKey;
            } while (options.ContinuationKey != null);

            Assert.AreEqual(Fixture.Rows.Count() * 2, rows.Count);
            Assert.IsTrue(queryCallCount > 1);
            var expectedKeys = Fixture.Rows.Select(row =>
                row["shardId"].AsInt32 + ":" + row["pkString"].AsString).ToArray();
            CollectionAssert.AreEquivalent(
                expectedKeys.Concat(expectedKeys).ToArray(),
                rows.Select(row => row["shardId"].AsInt32 + ":" +
                    row["pkString"].AsString).ToArray());

            if (!sorted)
            {
                // UNION ALL without ORDER BY must drain branch 1 before
                // advancing to branch 2, even when every row has a separate
                // continuation key.
                var branchBoundary = Fixture.Rows.Count();
                for (var i = 0; i < rows.Count; i++)
                {
                    Assert.AreEqual(i < branchBoundary ? 1 : 2,
                        rows[i]["branch"].AsInt32);
                }
                return;
            }

            // A sorted UNION must maintain its order across continuation
            // keys, including equal rows supplied by the two branches.
            for (var i = 1; i < rows.Count; i++)
            {
                var previous = rows[i - 1];
                var current = rows[i];
                var shardComparison = previous["shardId"].AsInt32.CompareTo(
                    current["shardId"].AsInt32);
                Assert.IsTrue(shardComparison < 0 ||
                    shardComparison == 0 && string.CompareOrdinal(
                        previous["pkString"].AsString,
                        current["pkString"].AsString) <= 0);
            }
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
