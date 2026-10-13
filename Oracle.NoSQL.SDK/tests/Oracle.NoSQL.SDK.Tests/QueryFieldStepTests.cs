/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 * https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
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

        private static FieldStep Project(PlanStep input, string field = "key",
            int resultPosition = 0) => new FieldStep
        {
            ResultPosition = resultPosition,
            InputStep = input,
            FieldName = field
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

        public static IEnumerable<object[]> ScalarFieldValues => new[]
        {
            new object[] { "integer", new IntegerValue(42) },
            new object[] { "SQL NULL", FieldValue.Null },
            new object[] { "JSON null", FieldValue.JsonNull },
            new object[] { "array", new ArrayValue { 1, 2 } },
            new object[] { "map", new MapValue { ["nested"] = 3 } }
        };

        [DataTestMethod]
        [DynamicData(nameof(ScalarFieldValues))]
        public void TestScalarColumnPreservesFieldValue(string name,
            FieldValue value)
        {
            using var client = CreateClient();
            var runtime = CreateRuntime(client);
            runtime.ResultRegistry[1] = new RecordValue { ["key"] = value };
            var iterator = Project(new VarRefStep
            {
                ResultPosition = 1,
                VarName = "$row"
            }).CreateSyncIterator(runtime);

            for (var repeat = 0; repeat < 3; ++repeat)
            {
                Assert.IsTrue(iterator.Next(), name);
                Assert.AreSame(value, iterator.Result, name);
                Assert.IsFalse(iterator.Next(), name);
                iterator.Reset(repeat == 1);
            }
        }

        public static IEnumerable<object[]> NoItemContexts => new[]
        {
            new object[] { "integer", new IntegerValue(1) },
            new object[] { "long", new LongValue(1) },
            new object[] { "double", new DoubleValue(1) },
            new object[] { "number", new NumberValue(1) },
            new object[] { "boolean", BooleanValue.True },
            new object[] { "string", new StringValue("text") },
            new object[] { "binary", new BinaryValue(new byte[] { 1 }) },
            new object[] { "timestamp", new TimestampValue(
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)) },
            new object[] { "JSON null", FieldValue.JsonNull },
            new object[] { "empty array", new ArrayValue() },
            new object[] { "missing field", new MapValue() },
            new object[] { "EMPTY field", Item(FieldValue.Empty) }
        };

        [DataTestMethod]
        [DynamicData(nameof(NoItemContexts))]
        public void TestContextsWithoutFieldProduceNoItems(string name,
            FieldValue value)
        {
            using var client = CreateClient();
            var iterator = Project(Constant(value))
                .CreateSyncIterator(CreateRuntime(client));
            for (var repeat = 0; repeat < 3; ++repeat)
            {
                Assert.IsFalse(iterator.Next(), name);
                Assert.IsFalse(iterator.Next(), name);
                iterator.Reset(repeat == 1);
            }
        }

        [TestMethod]
        public void TestSqlNullContextPropagates()
        {
            using var client = CreateClient();
            var iterator = Project(Constant(FieldValue.Null))
                .CreateSyncIterator(CreateRuntime(client));
            AssertResults(iterator, FieldValue.Null);
            iterator.Reset(true);
            AssertResults(iterator, FieldValue.Null);
        }

        [TestMethod]
        public void TestNestedArraysPreserveOrderAndFieldValues()
        {
            using var client = CreateClient();
            var arrayField = new ArrayValue { 3, 4 };
            var input = new ArrayValue
            {
                Item(1),
                new ArrayValue
                {
                    FieldValue.JsonNull, 99, new MapValue(), new ArrayValue(),
                    new ArrayValue { Item(2), Item(FieldValue.Empty) }
                },
                "ignored",
                Item(arrayField), Item(FieldValue.JsonNull), Item(FieldValue.Null)
            };
            var original = input.ToJsonString();
            var iterator = Project(Constant(input))
                .CreateSyncIterator(CreateRuntime(client));

            AssertResults(iterator, 1, 2, arrayField,
                FieldValue.JsonNull, FieldValue.Null);
            iterator.Reset();
            AssertResults(iterator, 1, 2, arrayField,
                FieldValue.JsonNull, FieldValue.Null);
            Assert.AreEqual(original, input.ToJsonString(),
                "Field navigation must not mutate input arrays or maps");
        }

        [TestMethod]
        public void TestMissingContextsDoNotEndInputSequence()
        {
            using var client = CreateClient();
            var input = new SequenceStep
            {
                ResultPosition = 1,
                Values = new FieldValue[]
                {
                    new MapValue(), new IntegerValue(99), FieldValue.JsonNull,
                    Item(FieldValue.Empty),
                    new ArrayValue { Item(10), new ArrayValue { Item(20) } },
                    new MapValue(), Item(30)
                }
            };
            var iterator = Project(input)
                .CreateSyncIterator(CreateRuntime(client));

            Assert.IsTrue(iterator.Next());
            Assert.AreEqual(10, iterator.Result.AsInt32);
            Assert.AreEqual(5, input.NextCalls);
            Assert.IsTrue(iterator.Next());
            Assert.AreEqual(20, iterator.Result.AsInt32);
            Assert.AreEqual(5, input.NextCalls,
                "Drain the current array before advancing the input");
            AssertResults(iterator, 30);
            Assert.AreEqual(8, input.NextCalls);
        }

        [TestMethod]
        public void TestEmptyInputIsTerminationSentinel()
        {
            using var client = CreateClient();
            var input = new SequenceStep
            {
                ResultPosition = 1,
                Values = new FieldValue[] { Item(1), FieldValue.Empty, Item(2) }
            };
            var iterator = Project(input)
                .CreateSyncIterator(CreateRuntime(client));

            AssertResults(iterator, 1);
            Assert.AreEqual(2, input.NextCalls);
            iterator.Reset();
            AssertResults(iterator, 1);
            Assert.AreEqual(2, input.NextCalls);
        }

        [TestMethod]
        public void TestNestedFieldStepsTraverseArrayValuedFields()
        {
            using var client = CreateClient();
            var input = new ArrayValue
            {
                new MapValue { ["children"] = new ArrayValue
                    {
                        Item(1), new MapValue(),
                        new ArrayValue { 99, Item(2) }
                    } },
                new MapValue { ["children"] = FieldValue.JsonNull },
                new MapValue { ["children"] = FieldValue.Null },
                new MapValue(),
                new MapValue { ["children"] = Item(3) }
            };
            var inner = Project(Constant(input, 2), "children", 1);
            var iterator = Project(inner)
                .CreateSyncIterator(CreateRuntime(client));

            AssertResults(iterator, 1, 2, FieldValue.Null, 3);
            iterator.Reset(true);
            AssertResults(iterator, 1, 2, FieldValue.Null, 3);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TestPartialResetDiscardsPendingArrayContexts(
            bool resetResult)
        {
            using var client = CreateClient();
            var values = new FieldValue[]
            {
                new ArrayValue
                {
                    new ArrayValue { Item(1), Item(2) }, Item(3)
                },
                Item(4)
            };
            var input = new SequenceStep { ResultPosition = 1, Values = values };
            var iterator = Project(input)
                .CreateSyncIterator(CreateRuntime(client));
            Assert.IsTrue(iterator.Next());
            Assert.AreEqual(1, iterator.Result.AsInt32);

            input.Values = new FieldValue[] { Item(99) };
            iterator.Reset(resetResult);
            Assert.AreEqual(resetResult, input.LastResetResult);
            AssertResults(iterator, 99);

            input.Values = values;
            iterator.Reset(resetResult);
            AssertResults(iterator, 1, 2, 3, 4);
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

        private sealed class SequenceStep : PlanSyncStep
        {
            internal FieldValue[] Values { get; set; }
            internal int NextCalls { get; set; }
            internal bool LastResetResult { get; set; }
            internal override string Name => "TEST_SEQUENCE";
            internal override PlanSyncIterator CreateSyncIterator(
                QueryRuntime runtime) => new SequenceIterator(runtime, this);
        }

        private sealed class SequenceIterator : PlanSyncIterator
        {
            private readonly SequenceStep step;
            private int position;

            internal SequenceIterator(QueryRuntime runtime, SequenceStep step)
                : base(runtime) => this.step = step;

            internal override PlanStep Step => step;

            internal override bool Next()
            {
                ++step.NextCalls;
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
                step.NextCalls = 0;
                step.LastResetResult = resetResult;
            }
        }
    }
}
