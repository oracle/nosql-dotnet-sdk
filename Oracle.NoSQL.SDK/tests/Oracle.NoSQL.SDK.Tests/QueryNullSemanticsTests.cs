/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 * https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Oracle.NoSQL.SDK.Query;

    [TestClass]
    public class QueryNullSemanticsTests
    {
        private static NoSQLClient CreateClient() => new NoSQLClient(
            new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "http://localhost:8080"
            });

        private static QueryRuntime CreateRuntime(NoSQLClient client) =>
            new QueryRuntime(client, new PreparedStatement
            {
                RegisterCount = 3,
                DriverQueryPlan = new ReceiveStep()
            }) { MaxMemory = 1024 * 1024 };

        public static IEnumerable<object[]> SizeCases => new[]
        {
            new object[] { "SQL NULL", FieldValue.Null, null },
            new object[] { "empty array", new ArrayValue(), 0 },
            new object[] { "array with nulls", new ArrayValue
                { 1, FieldValue.Null, FieldValue.JsonNull }, 3 },
            new object[] { "empty map", new MapValue(), 0 },
            new object[] { "map with nulls", new MapValue
                { ["sql"] = FieldValue.Null, ["json"] = FieldValue.JsonNull }, 2 }
        };

        [DataTestMethod]
        [DynamicData(nameof(SizeCases))]
        public void TestSizePreservesSqlNullAndCountsCollectionEntries(
            string name, FieldValue input, int? expected)
        {
            using var client = CreateClient();
            var iterator = new FuncSizeStep
            {
                ResultPosition = 0,
                InputStep = new ConstStep { ResultPosition = 1, Value = input }
            }.CreateSyncIterator(CreateRuntime(client));

            for (var repeat = 0; repeat < 3; repeat++)
            {
                Assert.IsTrue(iterator.Next(), name);
                if (expected.HasValue)
                {
                    Assert.AreEqual(expected.Value, iterator.Result.AsInt32,
                        name);
                }
                else
                {
                    Assert.AreSame(FieldValue.Null, iterator.Result, name);
                }
                Assert.IsFalse(iterator.Next(), name);
                iterator.Reset(repeat == 1);
            }
        }

        [TestMethod]
        public void TestSizeOfAbsentFieldProducesNoItem()
        {
            using var client = CreateClient();
            var iterator = new FuncSizeStep
            {
                ResultPosition = 0,
                InputStep = new FieldStep
                {
                    ResultPosition = 1,
                    FieldName = "missing",
                    InputStep = new ConstStep
                    {
                        ResultPosition = 2,
                        Value = new MapValue()
                    }
                }
            }.CreateSyncIterator(CreateRuntime(client));

            Assert.IsFalse(iterator.Next());
            iterator.Reset();
            Assert.IsFalse(iterator.Next());
        }

        public static IEnumerable<object[]> CountCases => new[]
        {
            new object[] { "JSON null", FieldValue.JsonNull, 1L },
            new object[] { "SQL NULL", FieldValue.Null, 0L },
            new object[] { "empty", FieldValue.Empty, 0L },
            new object[] { "number", new IntegerValue(1), 1L },
            new object[] { "string", new StringValue("one"), 1L },
            new object[] { "array", new ArrayValue(), 1L },
            new object[] { "map", new MapValue(), 1L }
        };

        [DataTestMethod]
        [DynamicData(nameof(CountCases))]
        public void TestCountDistinguishesJsonNullSqlNullAndEmpty(
            string name, FieldValue value, long expected)
        {
            var aggregator = new CountAggregator();
            Assert.AreEqual(0L, aggregator.Result.AsInt64, name);
            aggregator.Aggregate(value);
            Assert.AreEqual(expected, aggregator.Result.AsInt64, name);
            aggregator.Clear();
            Assert.AreEqual(0L, aggregator.Result.AsInt64, name);
            aggregator.Aggregate(value, true);
            Assert.AreEqual(expected, aggregator.Result.AsInt64, name);
        }

        [TestMethod]
        public void TestCountVariantsOnMixedValues()
        {
            var count = new CountAggregator();
            var countStar = new CountStarAggregator();
            var countNumbers = new CountNumbersAggregator();
            foreach (var value in new FieldValue[]
                { FieldValue.JsonNull, new IntegerValue(1), FieldValue.Null,
                    FieldValue.Empty, new StringValue("one"), new ArrayValue() })
            {
                count.Aggregate(value);
                countStar.Aggregate(value);
                countNumbers.Aggregate(value);
            }
            Assert.AreEqual(4L, count.Result.AsInt64);
            Assert.AreEqual(6L, countStar.Result.AsInt64);
            Assert.AreEqual(1L, countNumbers.Result.AsInt64);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task TestGroupCountIncludesJsonNull(
            bool countMemory, bool removeResult)
        {
            using var client = CreateClient();
            var rows = new List<RecordValue>();
            foreach (var value in new FieldValue[]
                { FieldValue.JsonNull, new IntegerValue(1), FieldValue.Null,
                    FieldValue.Empty })
            {
                rows.Add(new RecordValue
                    { ["group"] = "mixed", ["count"] = value });
            }
            rows.Add(new RecordValue
                { ["group"] = "excluded", ["count"] = FieldValue.Null });
            rows.Add(new RecordValue
                { ["group"] = "excluded", ["count"] = FieldValue.Empty });
            var iterator = new GroupStep
            {
                ResultPosition = 0,
                InputStep = new RowsStep
                    { ResultPosition = 1, Rows = rows.ToArray() },
                GroupingColumnCount = 1,
                ColumnNames = new[] { "group", "count" },
                AggregateFuncCodes = new[] { SQLFuncCode.Count },
                CountMemory = countMemory,
                RemoveResult = removeResult
            }.CreateAsyncIterator(CreateRuntime(client));

            var results = new Dictionary<string, long>();
            while (await iterator.NextAsync(CancellationToken.None))
            {
                var row = (RecordValue)iterator.Result;
                results.Add(row["group"].AsString, row["count"].AsInt64);
            }
            Assert.AreEqual(2, results.Count);
            Assert.AreEqual(2L, results["mixed"]);
            Assert.AreEqual(0L, results["excluded"]);
        }

        private sealed class RowsStep : PlanAsyncStep
        {
            internal RecordValue[] Rows { get; set; }
            internal override string Name => "TEST_ROWS";
            internal override PlanAsyncIterator CreateAsyncIterator(
                QueryRuntime runtime) => new RowsIterator(runtime, this);
        }

        private sealed class RowsIterator : PlanAsyncIterator
        {
            private readonly RowsStep step;
            private int position;
            internal RowsIterator(QueryRuntime runtime, RowsStep step)
                : base(runtime) => this.step = step;
            internal override PlanStep Step => step;
            internal override Task<bool> NextAsync(CancellationToken token)
            {
                if (position == step.Rows.Length)
                {
                    return Task.FromResult(false);
                }
                Result = step.Rows[position++];
                return Task.FromResult(true);
            }
        }
    }
}
