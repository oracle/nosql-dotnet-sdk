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
    public class QueryFieldStepTests
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
                RegisterCount = 8,
                DriverQueryPlan = new ReceiveStep()
            });

        private static FieldStep Project(PlanStep input,
            int resultPosition = 0) => new FieldStep
        {
            ResultPosition = resultPosition,
            InputStep = input,
            FieldName = "key"
        };

        private static ConstStep Constant(FieldValue value,
            int resultPosition = 1) => new ConstStep
        {
            ResultPosition = resultPosition,
            Value = value
        };

        private static MapValue Item(FieldValue value) =>
            new MapValue { ["key"] = value };

        private static void AssertResults(PlanSyncIterator iterator,
            params FieldValue[] expected)
        {
            foreach (var value in expected)
            {
                Assert.IsTrue(iterator.Next(), "Missing projected value");
                Assert.AreEqual(value.DbType, iterator.Result.DbType);
                Assert.AreEqual(value.ToJsonString(),
                    iterator.Result.ToJsonString());
            }
            Assert.IsFalse(iterator.Next(), "Unexpected projected value");
            Assert.IsFalse(iterator.Next(), "Exhaustion must be stable");
        }

        [TestMethod]
        public void TestCaseProjectionDistinguishesJsonNullAndSqlNull()
        {
            using var client = CreateClient();
            foreach (var value in new[]
                { FieldValue.JsonNull, FieldValue.Null, Item(42) })
            {
                var input = new CaseStep
                {
                    ResultPosition = 1,
                    ConditionSteps = new PlanStep[]
                        { Constant(BooleanValue.True, 2) },
                    ThenSteps = new PlanStep[] { Constant(value, 3) }
                };
                var iterator = Project(input)
                    .CreateSyncIterator(CreateRuntime(client));
                var expected = value == FieldValue.JsonNull
                    ? Array.Empty<FieldValue>()
                    : new[] { value == FieldValue.Null
                        ? FieldValue.Null : new IntegerValue(42) };
                AssertResults(iterator, expected);
                iterator.Reset(true);
                AssertResults(iterator, expected);
            }
        }

        [TestMethod]
        public void TestProjectedSequenceFeedsConstructorAndAggregate()
        {
            using var client = CreateClient();
            var input = Project(Constant(new ArrayValue { Item(42), Item(43) },
                2), resultPosition: 1);
            var arrayIterator = new ArrayConstructorStep
            {
                ResultPosition = 0,
                IsConditional = true,
                ArgSteps = new PlanStep[] { input }
            }.CreateSyncIterator(CreateRuntime(client));
            AssertResults(arrayIterator, new ArrayValue { 42, 43 });
            arrayIterator.Reset(true);
            AssertResults(arrayIterator, new ArrayValue { 42, 43 });

            var sumIterator = new SeqAggregateStep
            {
                ResultPosition = 0,
                FuncCode = QueryFuncCode.SeqSum,
                InputStep = input
            }.CreateSyncIterator(CreateRuntime(client));
            AssertResults(sumIterator, new LongValue(85));
            sumIterator.Reset(true);
            AssertResults(sumIterator, new LongValue(85));
        }
    }
}
