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
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Oracle.NoSQL.SDK.Query;
    using BinaryProtocol = Oracle.NoSQL.SDK.BinaryProtocol.Protocol;
    using NsonProtocol = Oracle.NoSQL.SDK.NsonProtocol.Protocol;
    using PlanSerializer = Oracle.NoSQL.SDK.Query.BinaryProtocol.PlanSerializer;

    [TestClass]
    public class QueryV6CompatibilityTests
    {
        [TestMethod]
        public void TestPreparedStatementUsesEachUnionBranch()
        {
            using var client = CreateClient("configNamespace");
            var statement = new PreparedStatement();
            for (var branch = 0; branch < 2; branch++)
            {
                statement.AddQueryBranch(new PreparedStatement.QueryBranch
                {
                    ProxyStatement = new[] { (byte)branch },
                    Namespace = "namespace" + branch,
                    TableName = "table" + branch
                });
            }
            var request = new QueryRequest<RecordValue>(client, statement,
                new QueryOptions { Namespace = "requestNamespace" });
            for (var branch = 0; branch < 2; branch++)
            {
                request.UnionBranch = branch;
                Assert.AreEqual((byte)branch, statement.GetProxyStatement(branch)[0]);
                Assert.AreEqual("table" + branch, request.InternalTableName);
                Assert.AreEqual("requestNamespace", request.Namespace);
            }
            request.Options = null;
            Assert.AreEqual("namespace1", request.Namespace);
            request.UnionBranch = 0;
            Assert.AreEqual("namespace0", request.Namespace);
            statement.Namespace = null;
            Assert.AreEqual("configNamespace", request.Namespace);
            Assert.ThrowsException<BadProtocolException>(() =>
                statement.GetProxyStatement(2));
            Assert.ThrowsException<BadProtocolException>(() =>
                statement.GetProxyStatement(-1));
            var incomplete = new PreparedStatement { Namespace = "namespace" };
            Assert.ThrowsException<BadProtocolException>(() =>
                incomplete.GetProxyStatement(0));
        }

        [TestMethod]
        public void TestQueryVersionFallbackFromV6ToV3()
        {
            var handler = new ProtocolHandler();

            Assert.AreEqual(QueryRequestBase.QueryV6, handler.QueryVersion);
            Assert.IsTrue(handler.DecrementQueryVersion(
                QueryRequestBase.QueryV6));
            Assert.AreEqual(QueryRequestBase.QueryV5, handler.QueryVersion);
            Assert.IsTrue(handler.DecrementQueryVersion(
                QueryRequestBase.QueryV5));
            Assert.AreEqual(QueryRequestBase.QueryV4, handler.QueryVersion);
            Assert.IsTrue(handler.DecrementQueryVersion(
                QueryRequestBase.QueryV4));
            Assert.AreEqual(QueryRequestBase.QueryV3, handler.QueryVersion);
            Assert.IsFalse(handler.DecrementQueryVersion(
                QueryRequestBase.QueryV3));
        }

        [DataTestMethod]
        [DataRow((short)4, 1)]
        [DataRow((short)5, 2)]
        [DataRow((short)6, 2)]
        public void TestVirtualScanWireFormat(short queryVersion,
            int expectedTables)
        {
            using var client = CreateClient();
            Assert.AreEqual(1, new VirtualScan().TableResumeInfos.Length);
            var scan = new VirtualScan
            {
                ShardId = 7,
                PartitionId = 9,
                TableResumeInfos = new[]
                {
                    new VirtualScan.TableResumeInfo
                    {
                        CurrentIndexRange = 3, PrimaryKey = new byte[] { 11 },
                        MoveAfterResumeKey = true
                    },
                    new VirtualScan.TableResumeInfo
                    {
                        CurrentIndexRange = 5, PrimaryKey = new byte[] { 22 },
                        JoinPathMatched = true
                    }
                }
            };
            var request = new QueryRequest<RecordValue>(client,
                new PreparedStatement { ProxyStatement = new byte[] { 1 } },
                null) { QueryVersion = queryVersion, VirtualScan = scan };
            var serializer =
                new Oracle.NoSQL.SDK.NsonProtocol.RequestSerializer();
            using var encoded = new MemoryStream();
            serializer.SerializeQuery(encoded, request);
            encoded.Position = 0;
            var reader = NsonProtocol.GetNsonReader(encoded);
            reader.Next();

            // A resume map contains repeated field names and typed null binaries.
            var fields = new List<(string Name, DbType Type, FieldValue Value)>();
            NsonProtocol.ReadMap(reader, field =>
            {
                if (field != NsonProtocol.FieldNames.Payload)
                {
                    return false;
                }
                NsonProtocol.ReadMap(reader, payloadField =>
                {
                    if (payloadField != NsonProtocol.FieldNames.VirtualScan)
                    {
                        return false;
                    }
                    NsonProtocol.ReadMap(reader, resumeField =>
                    {
                        fields.Add((resumeField, reader.NsonType,
                            NsonProtocol.ReadFieldValue(reader)));
                        return true;
                    });
                    return true;
                });
                return true;
            });
            var tableCounts = fields.FindAll(field => field.Name ==
                NsonProtocol.FieldNames.VirtualScanNumTables);
            Assert.AreEqual(queryVersion >= QueryRequestBase.QueryV5 ? 1 : 0,
                tableCounts.Count);
            if (tableCounts.Count != 0)
            {
                Assert.AreEqual(expectedTables, tableCounts[0].Value.AsInt32);
            }
            var ranges = fields.FindAll(field => field.Name ==
                NsonProtocol.FieldNames.VirtualScanCurrentIndexRange);
            Assert.AreEqual(expectedTables, ranges.Count);

            using var response = new MemoryStream();
            var writer = NsonProtocol.GetNsonWriter(response);
            writer.StartMap();
            writer.StartArray(NsonProtocol.FieldNames.VirtualScans);
            writer.StartMap();
            foreach (var field in fields)
            {
                writer.WriteFieldName(field.Name);
                if (field.Type == DbType.Binary && field.Value == FieldValue.Null)
                {
                    writer.WriteByteArray(null);
                }
                else
                {
                    NsonProtocol.WriteFieldValue(writer, field.Value);
                }
            }
            writer.EndMap();
            writer.EndArray();
            writer.EndMap();
            response.Position = 0;
            var decoded = serializer.DeserializeQuery(response, request)
                .VirtualScans[0];
            Assert.AreEqual(scan.ShardId, decoded.ShardId);
            Assert.AreEqual(scan.PartitionId, decoded.PartitionId);
            Assert.AreEqual(expectedTables, decoded.TableResumeInfos.Length);
            for (var i = 0; i < expectedTables; i++)
            {
                var expected = scan.TableResumeInfos[i];
                var actual = decoded.TableResumeInfos[i];
                Assert.AreEqual(expected.CurrentIndexRange,
                    ranges[i].Value.AsInt32);
                Assert.AreEqual(expected.CurrentIndexRange,
                    actual.CurrentIndexRange);
                CollectionAssert.AreEqual(expected.PrimaryKey,
                    actual.PrimaryKey);
                Assert.AreEqual(expected.MoveAfterResumeKey,
                    actual.MoveAfterResumeKey);
                Assert.AreEqual(expected.JoinPathMatched,
                    actual.JoinPathMatched);
            }
        }

        [TestMethod]
        public void TestArrayCollectRegrouping()
        {
            var normal = new CollectAggregator(false, false);
            normal.Aggregate(new StringValue("one"));
            Assert.AreEqual(1, normal.Result.AsArrayValue.Count);
            Assert.AreEqual("one", normal.Result.AsArrayValue[0].AsString);
            var rawArray = new ArrayValue { 1, 2 };
            normal.Aggregate(rawArray);
            Assert.AreEqual(2, normal.Result.AsArrayValue.Count);
            Assert.AreSame(rawArray, normal.Result.AsArrayValue[1]);

            var regrouped = new CollectAggregator(false, true);
            var partial = new ArrayValue
            {
                new StringValue("one"), new StringValue("two")
            };
            regrouped.Aggregate(partial);
            Assert.AreEqual(2, regrouped.Result.AsArrayValue.Count);
            Assert.AreEqual("one", regrouped.Result.AsArrayValue[0].AsString);
            Assert.AreEqual("two", regrouped.Result.AsArrayValue[1].AsString);

            var distinct = new CollectDistinctAggregator(false, false);
            distinct.Aggregate(new ArrayValue
            {
                new IntegerValue(1), new IntegerValue(2),
                new IntegerValue(1)
            });
            Assert.AreEqual(2, distinct.Result.AsArrayValue.Count);
            Assert.AreEqual(1, distinct.Result.AsArrayValue[0].AsInt32);
            Assert.AreEqual(2, distinct.Result.AsArrayValue[1].AsInt32);
        }

        [DataTestMethod]
        [DataRow((short)5, true)]
        [DataRow((short)6, false)]
        [DataRow((short)6, true)]
        public void TestGroupRegroupingWireFlag(short version, bool regrouping)
        {
            using var stream = new MemoryStream();
            BinaryProtocol.WriteByte(stream, 65); // GROUP
            WriteBase(stream);
            WriteIntegerConstStep(stream);
            BinaryProtocol.WriteUnpackedInt32(stream, 0);
            WriteStringArray(stream, "values");
            BinaryProtocol.WriteUnpackedInt16(stream,
                (short)SQLFuncCode.ArrayCollect);
            BinaryProtocol.WriteBoolean(stream, false);
            BinaryProtocol.WriteBoolean(stream, false);
            BinaryProtocol.WriteBoolean(stream, false);
            if (version >= QueryRequestBase.QueryV6)
            {
                BinaryProtocol.WriteBoolean(stream, regrouping);
            }
            stream.Position = 0;
            var step = (GroupStep)PlanSerializer.DeserializeStep(stream, version);
            Assert.AreEqual(regrouping, step.IsRegrouping);
        }

        [TestMethod]
        public void TestV6DriverOperatorDeserialization()
        {
            Assert.IsInstanceOfType(DeserializeV6Step(3, stream =>
            {
                BinaryProtocol.WriteBoolean(stream, false);
                WriteStepArray(stream, 1, WriteIntegerConstStep);
            }), typeof(ArrayConstructorStep));

            Assert.IsInstanceOfType(DeserializeV6Step(3, stream =>
            {
                BinaryProtocol.WriteBoolean(stream, false);
                WriteStepArray(stream, 0, WriteIntegerConstStep);
            }), typeof(ArrayConstructorStep));

            Assert.IsInstanceOfType(DeserializeV6Step(5, stream =>
            {
                BinaryProtocol.WriteUnpackedInt16(stream,
                    (short)QueryFuncCode.Equal);
                WriteIntegerConstStep(stream);
                WriteIntegerConstStep(stream);
            }), typeof(ValueCompareStep));

            Assert.IsInstanceOfType(DeserializeV6Step(7, stream =>
            {
                BinaryProtocol.WriteUnpackedInt16(stream,
                    (short)QueryFuncCode.And);
                WriteStepArray(stream, 1, WriteBooleanConstStep);
            }), typeof(AndOrStep));

            Assert.IsInstanceOfType(DeserializeV6Step(19, stream =>
            {
                WriteStepArray(stream, 1, WriteBooleanConstStep);
                WriteStepArray(stream, 1, WriteIntegerConstStep);
                WriteIntegerConstStep(stream);
            }), typeof(CaseStep));

            Assert.IsInstanceOfType(DeserializeV6Step(26, stream =>
            {
                BinaryProtocol.WriteUnpackedInt16(stream,
                    (short)QueryFuncCode.IsNull);
                WriteIntegerConstStep(stream);
            }), typeof(IsNullStep));

            Assert.IsInstanceOfType(DeserializeV6Step(48, stream =>
            {
                BinaryProtocol.WriteUnpackedInt16(stream,
                    (short)QueryFuncCode.SeqSum);
                WriteIntegerConstStep(stream);
            }), typeof(SeqAggregateStep));
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public void TestArrayConstructorExecutesAndResets(int count)
        {
            using var client = CreateClient();
            var values = Enumerable.Repeat<FieldValue>(new IntegerValue(1), count)
                .ToArray();
            var step = new ArrayConstructorStep
            {
                ResultPosition = 0,
                ArgSteps = values.Select(value => (PlanStep)Constant(1, value))
                    .ToArray()
            };
            StringAssert.Contains(PlanFormatter.Format(step), "\"input iterators\" : [");
            AssertResult(CreateRuntime(client), step, new ArrayValue(values));
        }

        public static IEnumerable<object[]> ComparisonCases
        {
            get
            {
                var time = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
                yield return new object[] { new IntegerValue(11), new IntegerValue(10), 1 };
                // Native timestamps must compare chronologically despite JSON's trailing Z.
                yield return new object[] { new TimestampValue(time),
                    new TimestampValue(time.AddMilliseconds(100)), -1 };
                yield return new object[] { new TimestampValue(time), new TimestampValue(time), 0 };
                yield return new object[] { new StringValue("2026-01-01T00:00:00Z"),
                    new StringValue("2026-01-01T00:00:00.1Z"), 1 };
                yield return new object[] { FieldValue.Null, new IntegerValue(1), null };
                yield return new object[] { new IntegerValue(1), FieldValue.Null, null };
                yield return new object[] { new ArrayValue { FieldValue.Null },
                    new ArrayValue { FieldValue.Null }, null };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(ComparisonCases))]
        public void TestV6ComparisonAndCase(FieldValue left, FieldValue right,
            int? order)
        {
            using var client = CreateClient();
            var runtime = CreateRuntime(client);
            var operations = new[]
            {
                QueryFuncCode.Equal, QueryFuncCode.NotEqual,
                QueryFuncCode.LessThan, QueryFuncCode.LessOrEqual,
                QueryFuncCode.GreaterThan, QueryFuncCode.GreaterOrEqual
            };
            var expected = new[]
            {
                order == 0, order.HasValue && order != 0,
                order < 0, order <= 0, order > 0, order >= 0
            };
            for (var i = 0; i < operations.Length; i++)
            {
                var comparison = new ValueCompareStep
                {
                    ResultPosition = 0,
                    FuncCode = operations[i],
                    LeftStep = Constant(1, left),
                    RightStep = Constant(2, right)
                };
                StringAssert.Contains(PlanFormatter.Format(comparison), "\"input iterator\" :");
                AssertResult(runtime, comparison, !order.HasValue
                    ? FieldValue.Null
                    : expected[i] ? BooleanValue.True : BooleanValue.False);
                AssertResult(runtime, new CaseStep
                {
                    ResultPosition = 3,
                    ConditionSteps = new PlanStep[] { comparison },
                    ThenSteps = new PlanStep[] { Constant(4, new IntegerValue(1)) },
                    ElseStep = Constant(5, new IntegerValue(0))
                }, new IntegerValue(expected[i] ? 1 : 0));
            }
        }

        [TestMethod]
        public void TestV6BooleanAndNullOperators()
        {
            using var client = CreateClient();
            var runtime = CreateRuntime(client);
            foreach (var function in new[] { QueryFuncCode.And, QueryFuncCode.Or })
            {
                var decisive = function == QueryFuncCode.And
                    ? BooleanValue.False : BooleanValue.True;
                AssertResult(runtime, new AndOrStep
                {
                    ResultPosition = 0,
                    FuncCode = function,
                    ArgSteps = new PlanStep[]
                    {
                        Constant(1, FieldValue.Null), Constant(2, decisive)
                    }
                }, decisive);
            }
            foreach (var function in new[] { QueryFuncCode.IsNull, QueryFuncCode.IsNotNull })
            {
                AssertResult(runtime, new IsNullStep
                {
                    ResultPosition = 0,
                    FuncCode = function,
                    InputStep = Constant(1, FieldValue.Null)
                }, function == QueryFuncCode.IsNull ? BooleanValue.True : BooleanValue.False);
            }
        }

        public static IEnumerable<object[]> NumericAggregateCases
        {
            get
            {
                yield return new object[] { "integer promotion and ignored inputs",
                    new FieldValue[] { new IntegerValue(int.MaxValue), FieldValue.Null,
                        new StringValue("ignored"), new IntegerValue(1) },
                    new LongValue(2147483648L), new DoubleValue(1073741824) };
                yield return new object[] { "NUMBER with representable DOUBLE",
                    new FieldValue[] { new NumberValue(1m), new DoubleValue(0.5) },
                    new NumberValue(1.5m), new NumberValue(0.75m) };
                yield return new object[] { "NUMBER with large DOUBLE",
                    new FieldValue[] { new NumberValue(1m), new DoubleValue(1e100) },
                    new DoubleValue(1e100), new DoubleValue(5e99) };
                yield return new object[] { "NUMBER with tiny nonzero DOUBLE",
                    new FieldValue[] { new NumberValue(0m), new DoubleValue(1e-30) },
                    new DoubleValue(1e-30), new DoubleValue(5e-31) };
                yield return new object[] { "decimal overflow",
                    new FieldValue[] { new NumberValue(decimal.MaxValue),
                        new NumberValue(decimal.MaxValue) },
                    new DoubleValue((double)decimal.MaxValue * 2),
                    new DoubleValue((double)decimal.MaxValue) };
                yield return new object[] { "long overflow follows Java",
                    new FieldValue[] { new LongValue(long.MaxValue), new LongValue(1) },
                    new LongValue(long.MinValue), new DoubleValue((double)long.MinValue / 2) };
            }
        }

        [DataTestMethod]
        [DynamicData(nameof(NumericAggregateCases))]
        public void TestSequenceNumericPromotion(string scenario,
            FieldValue[] values, FieldValue sum, FieldValue average)
        {
            using var client = CreateClient();
            var runtime = CreateRuntime(client);
            var original = values.Select(value => value.ToJsonString()).ToArray();
            foreach (var input in new[] { values, values.Reverse().ToArray() })
            {
                AssertResult(runtime, Aggregate(QueryFuncCode.SeqSum, input), sum);
                AssertResult(runtime, Aggregate(QueryFuncCode.SeqAverage, input), average);
            }
            CollectionAssert.AreEqual(original,
                values.Select(value => value.ToJsonString()).ToArray(), scenario);
        }

        [TestMethod]
        public void TestSequenceAggregatesHandleEmptyAndNull()
        {
            using var client = CreateClient();
            var runtime = CreateRuntime(client);
            AssertResult(runtime, Aggregate(QueryFuncCode.SeqSum), null);
            AssertResult(runtime, Aggregate(QueryFuncCode.SeqSum,
                FieldValue.Null, new StringValue("ignored")), FieldValue.Null);
            foreach (var function in new[]
                { QueryFuncCode.SeqCount, QueryFuncCode.SeqMin, QueryFuncCode.SeqMax })
            {
                AssertResult(runtime, Aggregate(function,
                    new IntegerValue(3), FieldValue.Null), FieldValue.Null);
            }
            foreach (var (function, expected) in new[]
                {
                    (QueryFuncCode.SeqCountIgnoreNulls, (FieldValue)new LongValue(2)),
                    (QueryFuncCode.SeqCountNumbersIgnoreNulls, new LongValue(2)),
                    (QueryFuncCode.SeqMinIgnoreNulls, new IntegerValue(2)),
                    (QueryFuncCode.SeqMaxIgnoreNulls, new IntegerValue(3))
                })
            {
                AssertResult(runtime, Aggregate(function,
                    new IntegerValue(3), FieldValue.Null, new IntegerValue(2)), expected);
            }
        }

        [DataTestMethod]
        [DataRow(-1, 0)]
        [DataRow(-1, 2)]
        [DataRow(0, 0)]
        [DataRow(0, 2)]
        [DataRow(1, 2)]
        [DataRow(2, 2)]
        public void TestUnionSortAttributesFollowSortKeys(int fieldCount,
            int specCount)
        {
            var step = (UnionStep)DeserializeV6Step(90, stream =>
            {
                WriteStepArray(stream, 2, WriteReceiveStep);
                WriteSortInfo(stream, fieldCount, specCount);
            });
            if (fieldCount < 0)
            {
                Assert.IsNull(step.SortSpecs);
            }
            else
            {
                Assert.IsNotNull(step.SortSpecs);
                Assert.AreEqual(fieldCount, step.SortSpecs.Length);
                for (var i = 0; i < fieldCount; i++)
                {
                    Assert.AreEqual("field" + i, step.SortSpecs[i].FieldName);
                    Assert.AreEqual(i % 2 != 0, step.SortSpecs[i].IsDescending);
                }
            }
        }

        [DataTestMethod]
        [DataRow(1, 0)]
        [DataRow(2, 1)]
        public void TestUnionRejectsMissingSortAttributes(int fieldCount,
            int specCount)
        {
            Assert.ThrowsException<BadProtocolException>(() =>
                DeserializeV6Step(90, stream =>
                {
                    WriteStepArray(stream, 2, WriteReceiveStep);
                    WriteSortInfo(stream, fieldCount, specCount);
                }));
        }

        [TestMethod]
        public void TestReceiveStillRejectsMismatchedSortAttributes()
        {
            Assert.ThrowsException<BadProtocolException>(() =>
                DeserializeV6Step(17, stream =>
                {
                    BinaryProtocol.WriteUnpackedInt16(stream,
                        (short)DistributionKind.AllPartitions);
                    WriteSortInfo(stream, 1, 2);
                    BinaryProtocol.WritePackedInt32(stream, -1);
                }));
        }

        private static void WriteSortInfo(MemoryStream stream, int fieldCount,
            int specCount)
        {
            BinaryProtocol.WritePackedInt32(stream, fieldCount);
            for (var i = 0; i < fieldCount; i++)
            {
                BinaryProtocol.WriteString(stream, "field" + i);
            }
            BinaryProtocol.WritePackedInt32(stream, specCount);
            for (var i = 0; i < specCount; i++)
            {
                BinaryProtocol.WriteBoolean(stream, i % 2 != 0);
                BinaryProtocol.WriteBoolean(stream, false);
            }
        }

        private static void WriteReceiveStep(MemoryStream stream)
        {
            BinaryProtocol.WriteByte(stream, 17);
            WriteBase(stream);
            BinaryProtocol.WriteUnpackedInt16(stream,
                (short)DistributionKind.AllPartitions);
            WriteSortInfo(stream, -1, -1);
            BinaryProtocol.WritePackedInt32(stream, -1);
        }

        private static NoSQLClient CreateClient(string ns = null) =>
            new NoSQLClient(new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "http://localhost:8080",
                Namespace = ns
            });

        private static QueryRuntime CreateRuntime(NoSQLClient client) =>
            new QueryRuntime(client, new PreparedStatement
            {
                RegisterCount = 6,
                DriverQueryPlan = new ReceiveStep()
            });

        private static ConstStep Constant(int position, FieldValue value) =>
            new ConstStep { ResultPosition = position, Value = value };

        private static SeqAggregateStep Aggregate(QueryFuncCode function,
            params FieldValue[] values) => new SeqAggregateStep
            {
                ResultPosition = 0,
                FuncCode = function,
                // A real field projection supplies the sequence, including SQL NULL.
                InputStep = new FieldStep
                {
                    ResultPosition = 1,
                    FieldName = "value",
                    InputStep = Constant(2, new ArrayValue(values.Select(value =>
                        new MapValue { ["value"] = value })))
                }
            };

        private static void AssertResult(QueryRuntime runtime, PlanSyncStep step,
            FieldValue expected)
        {
            var iterator = step.CreateSyncIterator(runtime);
            for (var repeat = 0; repeat < 2; repeat++)
            {
                Assert.AreEqual(expected != null, iterator.Next(), step.Name);
                if (expected != null)
                {
                    Assert.AreEqual(expected.DbType, iterator.Result.DbType, step.Name);
                    Assert.AreEqual(expected.ToJsonString(),
                        iterator.Result.ToJsonString(), step.Name);
                }
                Assert.IsFalse(iterator.Next(), step.Name);
                iterator.Reset();
            }
        }

        private static PlanStep DeserializeV6Step(byte type,
            Action<MemoryStream> writeContent)
        {
            using var stream = new MemoryStream();
            BinaryProtocol.WriteByte(stream, type);
            WriteBase(stream);
            writeContent(stream);
            // Packed writers reserve maximum-width space beyond Position.
            stream.SetLength(stream.Position);
            stream.Position = 0;
            var step = PlanSerializer.DeserializeStep(stream,
                QueryRequestBase.QueryV6);
            Assert.AreEqual(stream.Length, stream.Position,
                "The complete plan payload must be consumed");
            return step;
        }

        private static void WriteStepArray(MemoryStream stream, int count,
            Action<MemoryStream> writeStep)
        {
            BinaryProtocol.WritePackedInt32(stream, count);
            for (var i = 0; i < count; i++)
            {
                writeStep(stream);
            }
        }

        private static void WriteStringArray(MemoryStream stream,
            params string[] values)
        {
            BinaryProtocol.WritePackedInt32(stream, values.Length);
            foreach (var value in values)
            {
                BinaryProtocol.WriteString(stream, value);
            }
        }

        private static void WriteIntegerConstStep(MemoryStream stream)
        {
            BinaryProtocol.WriteByte(stream, 0);
            WriteBase(stream);
            BinaryProtocol.WriteFieldValue(stream, new IntegerValue(1));
        }

        private static void WriteBooleanConstStep(MemoryStream stream)
        {
            BinaryProtocol.WriteByte(stream, 0);
            WriteBase(stream);
            BinaryProtocol.WriteFieldValue(stream, BooleanValue.True);
        }

        private static void WriteBase(MemoryStream stream)
        {
            BinaryProtocol.WriteUnpackedInt32(stream, 0);
            BinaryProtocol.WriteUnpackedInt32(stream, 0);
            for (var i = 0; i < 4; i++)
            {
                BinaryProtocol.WriteUnpackedInt32(stream, 0);
            }
        }
    }
}
