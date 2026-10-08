/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 * https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Oracle.NoSQL.SDK.Query;

    [TestClass]
    public class QueryV6NumericAggregateTests
    {
        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void TestMixedNumberAndLargeDouble(bool average, bool reverse)
        {
            var values = Ordered(reverse, new DoubleValue(1e100),
                new NumberValue(1m));
            var result = Aggregate(average, values);

            Assert.AreEqual(DbType.Double, result.DbType);
            Assert.AreEqual(average ? 5e99 : 1e100, result.AsDouble);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void TestMixedNumberAndRepresentableDouble(bool average,
            bool reverse)
        {
            var result = Aggregate(average, Ordered(reverse,
                new DoubleValue(0.5), new NumberValue(1m)));

            Assert.AreEqual(DbType.Number, result.DbType);
            Assert.AreEqual(average ? 0.75m : 1.5m, result.AsDecimal);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TestDecimalSumBeyondRange(bool average)
        {
            var result = Aggregate(average, new NumberValue(decimal.MaxValue),
                new NumberValue(decimal.MaxValue));

            Assert.AreEqual(DbType.Double, result.DbType);
            Assert.AreEqual((double)decimal.MaxValue * (average ? 1 : 2),
                result.AsDouble);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TestMixedSumContinuesAfterDecimalOverflow(bool average)
        {
            var result = Aggregate(average, new DoubleValue(1e100),
                new NumberValue(1m), new NumberValue(2m),
                new DoubleValue(1e100));

            Assert.AreEqual(DbType.Double, result.DbType);
            Assert.AreEqual(average ? 5e99 : 2e100, result.AsDouble);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TestNumberPromotionAfterOverflowAndCancellation(
            bool average)
        {
            var result = Aggregate(average, new DoubleValue(1e100),
                new NumberValue(1m), new DoubleValue(-1e100),
                new NumberValue(2m), new LongValue(3));

            Assert.AreEqual(DbType.Number, result.DbType);
            Assert.AreEqual(average ? 1m : 5m, result.AsDecimal);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void TestMixedNumberAndLongBeyondDecimalRange(bool average,
            bool reverse)
        {
            var result = Aggregate(average, Ordered(reverse,
                new LongValue(1), new NumberValue(decimal.MaxValue)));

            Assert.AreEqual(DbType.Double, result.DbType);
            Assert.AreEqual((double)decimal.MaxValue / (average ? 2 : 1),
                result.AsDouble);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void TestMixedSumBeyondDecimalRange(bool average, bool reverse)
        {
            var result = Aggregate(average, Ordered(reverse,
                new DoubleValue(1e28), new NumberValue(decimal.MaxValue)));

            Assert.AreEqual(DbType.Double, result.DbType);
            Assert.AreEqual(((double)decimal.MaxValue + 1e28) /
                (average ? 2 : 1), result.AsDouble);
        }

        [DataTestMethod]
        [DataRow(double.PositiveInfinity, false)]
        [DataRow(double.PositiveInfinity, true)]
        [DataRow(double.NegativeInfinity, false)]
        [DataRow(double.NegativeInfinity, true)]
        [DataRow(double.NaN, false)]
        [DataRow(double.NaN, true)]
        public void TestMixedNumberAndNonFiniteDouble(double value,
            bool reverse)
        {
            foreach (var average in new[] { false, true })
            {
                var result = Aggregate(average, Ordered(reverse,
                    new DoubleValue(value), new NumberValue(1m)));

                Assert.AreEqual(DbType.Double, result.DbType);
                Assert.AreEqual(value, result.AsDouble);
            }
        }

        [TestMethod]
        public void TestIntegerSumUsesLongAndIgnoresNonNumericInputs()
        {
            var sum = Aggregate(false, new IntegerValue(int.MaxValue),
                FieldValue.Null, new StringValue("ignored"),
                new IntegerValue(1));
            Assert.AreEqual(DbType.Long, sum.DbType);
            Assert.AreEqual(2147483648L, sum.AsInt64);

            var average = Aggregate(true, new IntegerValue(1),
                FieldValue.Null, new StringValue("ignored"),
                new IntegerValue(2));
            Assert.AreEqual(DbType.Double, average.DbType);
            Assert.AreEqual(1.5, average.AsDouble);
        }

        [TestMethod]
        public void TestLongOverflowKeepsJavaSequenceAggregateSemantics()
        {
            var sum = Aggregate(false, new LongValue(long.MaxValue),
                new LongValue(1));
            Assert.AreEqual(DbType.Long, sum.DbType);
            Assert.AreEqual(long.MinValue, sum.AsInt64);

            var average = Aggregate(true, new LongValue(long.MaxValue),
                new LongValue(1));
            Assert.AreEqual(DbType.Double, average.DbType);
            Assert.AreEqual((double)long.MinValue / 2, average.AsDouble);
        }

        private static FieldValue[] Ordered(bool reverse,
            FieldValue first, FieldValue second) => reverse
            ? new[] { second, first } : new[] { first, second };

        private static FieldValue Aggregate(bool average,
            params FieldValue[] values)
        {
            using var client = new NoSQLClient(new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "http://localhost:8080"
            });
            var runtime = new QueryRuntime(client, new PreparedStatement
            {
                RegisterCount = 2,
                DriverQueryPlan = new ReceiveStep()
            });
            var inputJson = Array.ConvertAll(values,
                value => value.ToJsonString());
            var iterator = new SeqAggregateStep
            {
                ResultPosition = 0,
                FuncCode = average ? QueryFuncCode.SeqAverage :
                    QueryFuncCode.SeqSum,
                InputStep = new SequenceStep
                {
                    ResultPosition = 1,
                    Values = values
                }
            }.CreateSyncIterator(runtime);

            Assert.IsTrue(iterator.Next());
            var result = iterator.Result;
            Assert.IsFalse(iterator.Next());
            iterator.Reset();
            Assert.IsTrue(iterator.Next());
            Assert.AreEqual(result.DbType, iterator.Result.DbType);
            Assert.AreEqual(result.ToJsonString(),
                iterator.Result.ToJsonString());
            CollectionAssert.AreEqual(inputJson,
                Array.ConvertAll(values, value => value.ToJsonString()),
                "Aggregation must not mutate its input values.");
            return result;
        }

        private sealed class SequenceStep : PlanSyncStep
        {
            internal FieldValue[] Values { get; set; }

            internal override string Name => "TEST_SEQUENCE";

            internal override PlanSyncIterator CreateSyncIterator(
                QueryRuntime runtime) => new SequenceIterator(runtime, this);
        }

        private sealed class SequenceIterator : PlanSyncIterator
        {
            private readonly SequenceStep step;
            private int position;

            internal SequenceIterator(QueryRuntime runtime, SequenceStep step)
                : base(runtime)
            {
                this.step = step;
            }

            internal override PlanStep Step => step;

            internal override bool Next()
            {
                if (position == step.Values.Length)
                {
                    return false;
                }
                Result = step.Values[position++];
                return true;
            }

            internal override void Reset(bool resetResult = false)
            {
                position = 0;
            }
        }
    }
}
