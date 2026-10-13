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
    public class QueryArithmeticRegressionTests
    {
        [TestMethod]
        public void TestTrailingDoublePromotesEarlierIntegerDivision()
        {
            Verify(ArithmeticOpcode.MultiplyDivide, "*/*",
                new DoubleValue(2.5), new IntegerValue(5),
                new IntegerValue(2), new DoubleValue(1));
        }

        [TestMethod]
        public void TestTrailingNumberPromotesEarlierIntegerDivision()
        {
            Verify(ArithmeticOpcode.MultiplyDivide, "*/*",
                new NumberValue(2.5m), new LongValue(5),
                new LongValue(2), new NumberValue(1m));
        }

        [TestMethod]
        public void TestTrailingRealDivisionPromotesEarlierIntegerDivision()
        {
            Verify(ArithmeticOpcode.MultiplyDivide, "*/d",
                new DoubleValue(2.5), new IntegerValue(5),
                new IntegerValue(2), new IntegerValue(1));
        }

        [TestMethod]
        public void TestTrailingSqlNullPrecedesIntegerDivisionByZero()
        {
            Verify(ArithmeticOpcode.MultiplyDivide, "*/*", FieldValue.Null,
                new IntegerValue(1), new IntegerValue(0), FieldValue.Null);
        }

        [TestMethod]
        public void TestTrailingDoubleAvoidsIntegerDivisionOverflow()
        {
            Verify(ArithmeticOpcode.MultiplyDivide, "*/*",
                new DoubleValue(2147483648d), new IntegerValue(int.MinValue),
                new IntegerValue(-1), new DoubleValue(1));
        }

        [TestMethod]
        public void TestTrailingNumberAvoidsLongDivisionOverflow()
        {
            Verify(ArithmeticOpcode.MultiplyDivide, "*/*",
                new NumberValue(9223372036854775808m),
                new LongValue(long.MinValue), new IntegerValue(-1),
                new NumberValue(1m));
        }

        [TestMethod]
        public void TestTrailingDoubleDeterminesLargeIntegerRounding()
        {
            // Java ArithOpIter chooses DOUBLE before evaluating any operator.
            // At this magnitude adding 1 rounds back to the original double.
            Verify(ArithmeticOpcode.AddSubtract, "++-+", new DoubleValue(0),
                new LongValue(long.MaxValue), new IntegerValue(1),
                new LongValue(long.MaxValue), new DoubleValue(0));
        }

        [TestMethod]
        public void TestIntegerDivisionAndNullKindsRemainDistinct()
        {
            Verify(ArithmeticOpcode.MultiplyDivide, "*/", new IntegerValue(2),
                new IntegerValue(5), new IntegerValue(2));
            Assert.ThrowsException<InvalidOperationException>(() =>
                Verify(ArithmeticOpcode.AddSubtract, "++", FieldValue.Null,
                    new IntegerValue(1), FieldValue.JsonNull));
        }

        [TestMethod]
        public void TestLongPromotionPreservesIntegerDivision()
        {
            Verify(ArithmeticOpcode.MultiplyDivide, "*/*", new LongValue(2),
                new IntegerValue(5), new IntegerValue(2), new LongValue(1));
        }

        [TestMethod]
        public void TestMixedNumberAndDoublePreservesDecimalRangeFallback()
        {
            Verify(ArithmeticOpcode.AddSubtract, "++", new NumberValue(1.5m),
                new DoubleValue(0.5), new NumberValue(1m));
            Verify(ArithmeticOpcode.AddSubtract, "++", new DoubleValue(1e100),
                new DoubleValue(1e100), new NumberValue(1m));
            Verify(ArithmeticOpcode.AddSubtract, "++", new DoubleValue(1e100),
                new NumberValue(1m), new DoubleValue(1e100));
        }

        [TestMethod]
        public void TestMixedNumberAndDoubleCancellationPreservesPrecision()
        {
            const decimal value = 9007199254740993m;
            Verify(ArithmeticOpcode.AddSubtract, "++-", new NumberValue(0m),
                new NumberValue(value), new DoubleValue(0),
                new NumberValue(value));
            Verify(ArithmeticOpcode.AddSubtract, "++-", new NumberValue(0m),
                new DoubleValue(0), new NumberValue(value),
                new NumberValue(value));
        }

        [DataTestMethod]
        [DataRow("++", 0d)]
        [DataRow("+-", 0d)]
        [DataRow("**", 1d)]
        [DataRow("*/", 1d)]
        [DataRow("*d", 1d)]
        public void TestMixedNumberAndDoubleOperationsPreservePrecision(
            string operations, double operand)
        {
            const decimal value = 9007199254740993m;
            Verify(operations[0] == '+' ? ArithmeticOpcode.AddSubtract :
                    ArithmeticOpcode.MultiplyDivide,
                operations, new NumberValue(value), new NumberValue(value),
                new DoubleValue(operand));
        }

        [TestMethod]
        public void TestMixedNumberAndDoubleOverflowFallsBackToDouble()
        {
            Verify(ArithmeticOpcode.AddSubtract, "++",
                new DoubleValue((double)decimal.MaxValue),
                new NumberValue(decimal.MaxValue), new DoubleValue(1));
            Verify(ArithmeticOpcode.MultiplyDivide, "**",
                new DoubleValue((double)decimal.MaxValue * 2),
                new NumberValue(decimal.MaxValue), new DoubleValue(2));
        }

        [DataTestMethod]
        [DataRow(double.PositiveInfinity)]
        [DataRow(double.NegativeInfinity)]
        [DataRow(double.NaN)]
        public void TestMixedNumberAndNonFiniteDoublePreservesFallback(
            double value)
        {
            Verify(ArithmeticOpcode.AddSubtract, "++", new DoubleValue(value),
                new NumberValue(1m), new DoubleValue(value));
            Verify(ArithmeticOpcode.AddSubtract, "++", new DoubleValue(value),
                new DoubleValue(value), new NumberValue(1m));
        }

        [DataTestMethod]
        [DataRow("*/", 1e-30)]
        [DataRow("*/", -1e-30)]
        [DataRow("*/", double.Epsilon)]
        [DataRow("*d", 1e-30)]
        [DataRow("*d", -1e-30)]
        [DataRow("*d", double.Epsilon)]
        public void TestTinyDoubleDivisorPreservesDoubleFallback(
            string operations, double divisor)
        {
            Verify(ArithmeticOpcode.MultiplyDivide, operations,
                new DoubleValue(1 / divisor), new NumberValue(1m),
                new DoubleValue(divisor));
        }

        [DataTestMethod]
        [DataRow(false, 1e-30)]
        [DataRow(true, 1e-30)]
        [DataRow(false, -1e-30)]
        [DataRow(true, -1e-30)]
        public void TestTinyDoubleAccumulatorSurvivesLaterNumber(
            bool numberFirst, double value)
        {
            var operands = numberFirst
                ? new FieldValue[]
                {
                    new NumberValue(0m), new DoubleValue(value),
                    new NumberValue(0m)
                }
                : new FieldValue[]
                {
                    new DoubleValue(value), new NumberValue(0m)
                };
            Verify(ArithmeticOpcode.AddSubtract, numberFirst ? "+++" : "++",
                new DoubleValue(value), operands);
        }

        [TestMethod]
        public void TestNumberPromotionResumesAfterDoubleCancellation()
        {
            foreach (var value in new[] { 1e-30, 1e100 })
            {
                Verify(ArithmeticOpcode.AddSubtract, "++-+",
                    new NumberValue(1m), new NumberValue(0m),
                    new DoubleValue(value), new DoubleValue(value),
                    new NumberValue(1m));
            }
        }

        private static void Verify(ArithmeticOpcode opcode, string operations,
            FieldValue expected, params FieldValue[] operands)
        {
            using var client = new NoSQLClient(new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "http://localhost:8080"
            });
            var runtime = new QueryRuntime(client, new PreparedStatement
            {
                RegisterCount = operands.Length + 1,
                DriverQueryPlan = new ReceiveStep()
            });
            var arguments = new PlanStep[operands.Length];
            var originalValues = Array.ConvertAll(operands,
                value => value.ToJsonString());
            for (var i = 0; i < operands.Length; i++)
            {
                arguments[i] = new ConstStep
                {
                    ResultPosition = i + 1,
                    Value = operands[i]
                };
            }
            var iterator = new ArithmeticOpStep
            {
                ResultPosition = 0,
                Opcode = opcode,
                OpSequence = operations,
                ArgSteps = arguments
            }.CreateSyncIterator(runtime);
            for (var repeat = 0; repeat < 2; repeat++)
            {
                Assert.IsTrue(iterator.Next());
                Assert.AreEqual(expected.DbType, iterator.Result.DbType);
                Assert.AreEqual(expected, iterator.Result);
                Assert.IsFalse(iterator.Next());
                CollectionAssert.AreEqual(originalValues,
                    Array.ConvertAll(operands, value => value.ToJsonString()),
                    "Arithmetic must not mutate its operands.");
                iterator.Reset();
            }
        }
    }
}
