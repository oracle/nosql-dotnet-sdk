/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 * https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Globalization;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Oracle.NoSQL.SDK.Query;

    [TestClass]
    public class QueryTimestampComparisonTests
    {
        private static readonly TimestampValue Timestamp = new TimestampValue(
            new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

        private static readonly QueryFuncCode[] Operations =
        {
            QueryFuncCode.Equal, QueryFuncCode.NotEqual,
            QueryFuncCode.LessThan, QueryFuncCode.LessOrEqual,
            QueryFuncCode.GreaterThan, QueryFuncCode.GreaterOrEqual
        };

        private static NoSQLClient CreateClient() => new NoSQLClient(
            new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "http://localhost:8080"
            });

        private static QueryRuntime CreateRuntime(NoSQLClient client) =>
            new QueryRuntime(client, new PreparedStatement
            {
                RegisterCount = 6,
                DriverQueryPlan = new ReceiveStep()
            });

        private static ValueCompareStep Comparison(FieldValue left,
            FieldValue right, QueryFuncCode operation) => new ValueCompareStep
        {
            ResultPosition = 0,
            FuncCode = operation,
            LeftStep = new ConstStep { ResultPosition = 1, Value = left },
            RightStep = new ConstStep { ResultPosition = 2, Value = right }
        };

        [DataTestMethod]
        [DataRow("2026-01-01T00:00:00.1Z", -1)]
        [DataRow("2026-01-01T00:00:00.000Z", 0)]
        [DataRow("2026-01-01T00:00:00.000000000Z", 0)]
        [DataRow("2025-12-31T23:59:59.9Z", 1)]
        [DataRow("2026-01-01T05:30:00+05:30", 0)]
        [DataRow("2025-12-31T18:30:00-05:30", 0)]
        [DataRow("2026-01-01T00:00:00+00:00", 0)]
        [DataRow("2026-01-01T00:00:00", 0)]
        [DataRow("2026-01-01", 0)]
        [DataRow("2026-1-1T0:0:0", 0)]
        [DataRow("2026-01-01T00", 0)]
        [DataRow("2026-01-01T00:00", 0)]
        public void TestMixedTimestampComparisonInBothOrders(string text,
            int order)
        {
            var value = new StringValue(text);
            VerifyComparisons(Timestamp, value, order);
            VerifyComparisons(value, Timestamp, -order);
        }

        [DataTestMethod]
        [DataRow("2026-01-01T00:00:00.000000049Z", 0)]
        [DataRow("2026-01-01T00:00:00.000000050Z", -1)]
        [DataRow("2026-01-01T00:00:00.0000001Z", -1)]
        public void TestMixedTimestampUsesSdkDateTimePrecision(string text,
            int order)
        {
            var wireValue = new TimestampValue(
                BinaryProtocol.Protocol.StringToDateTime(text));
            Assert.AreEqual(order, Math.Sign(Timestamp.AsDateTime.CompareTo(
                wireValue.AsDateTime)));
            VerifyComparisons(Timestamp, new StringValue(text), order);
            VerifyComparisons(new StringValue(text), Timestamp, -order);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("not-a-date")]
        [DataRow("01/01/2026")]
        [DataRow("January 1, 2026")]
        [DataRow("2026-02-30T00:00:00Z")]
        [DataRow("2026-01-01T24:00:00Z")]
        [DataRow("2026-01-01T00:00:00.1234567890Z")]
        [DataRow("2026-01-01T00:00:00+19:00")]
        [DataRow("2026-01-01T00:00:00+00:60")]
        [DataRow("2026-1-1T1+01:00")]
        public void TestInvalidTimestampStringsFailInBothOrders(string text)
        {
            using var client = CreateClient();
            var runtime = CreateRuntime(client);
            foreach (var operation in Operations)
            {
                foreach (var reverse in new[] { false, true })
                {
                    var value = new StringValue(text);
                    var iterator = Comparison(reverse ? value : Timestamp,
                        reverse ? Timestamp : value, operation)
                        .CreateSyncIterator(runtime);
                    var exception = Assert.ThrowsException<InvalidOperationException>(
                        () => iterator.Next());
                    StringAssert.Contains(exception.Message,
                        "invalid timestamp string");
                }
            }
        }

        [TestMethod]
        public void TestMixedTimestampComparisonIsCultureIndependent()
        {
            var previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
                VerifyComparisons(Timestamp,
                    new StringValue("2026-01-01T05:30:00.100+05:30"), -1);
            }
            finally
            {
                CultureInfo.CurrentCulture = previous;
            }
        }

        [TestMethod]
        public void TestMixedTimestampNestedValues()
        {
            VerifyComparisons(new ArrayValue { Timestamp },
                new ArrayValue { new StringValue("2026-01-01T00:00:00.1Z") }, -1);
            VerifyComparisons(new MapValue { ["time"] = Timestamp },
                new MapValue { ["time"] = "2026-01-01T00:00:00.000Z" },
                0, equalityOnly: true);
        }

        [TestMethod]
        public void TestNullComparisonDoesNotParseInvalidTimestampText()
        {
            using var client = CreateClient();
            var runtime = CreateRuntime(client);
            foreach (var operation in Operations)
            {
                foreach (var reverse in new[] { false, true })
                {
                    var value = new StringValue("not-a-date");
                    var iterator = Comparison(reverse ? value : FieldValue.Null,
                        reverse ? FieldValue.Null : value, operation)
                        .CreateSyncIterator(runtime);
                    Assert.IsTrue(iterator.Next());
                    Assert.AreSame(FieldValue.Null, iterator.Result);
                }
            }
        }

        private static void VerifyComparisons(FieldValue left, FieldValue right,
            int order, bool equalityOnly = false)
        {
            using var client = CreateClient();
            var runtime = CreateRuntime(client);
            var expected = new[]
            {
                order == 0, order != 0, order < 0,
                order <= 0, order > 0, order >= 0
            };
            for (var i = 0; i < (equalityOnly ? 2 : Operations.Length); i++)
            {
                var comparison = Comparison(left, right, Operations[i]);
                var iterator = comparison.CreateSyncIterator(runtime);
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    Assert.IsTrue(iterator.Next());
                    Assert.AreEqual(expected[i], iterator.Result.AsBoolean,
                        Operations[i].ToString());
                    Assert.IsFalse(iterator.Next());
                    iterator.Reset();
                }

                var conditional = new CaseStep
                {
                    ResultPosition = 3,
                    ConditionSteps = new PlanStep[] { comparison },
                    ThenSteps = new PlanStep[]
                    {
                        new ConstStep { ResultPosition = 4, Value = new IntegerValue(1) }
                    },
                    ElseStep = new ConstStep
                    {
                        ResultPosition = 5, Value = new IntegerValue(0)
                    }
                }.CreateSyncIterator(runtime);
                Assert.IsTrue(conditional.Next());
                Assert.AreEqual(expected[i] ? 1 : 0,
                    conditional.Result.AsInt32);
                Assert.IsFalse(conditional.Next());
            }
        }
    }
}
