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
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Oracle.NoSQL.SDK.Query;
    using NsonProtocol = Oracle.NoSQL.SDK.NsonProtocol.Protocol;
    using Opcode = Oracle.NoSQL.SDK.BinaryProtocol.Opcode;

    [TestClass]
    public class QueryV6RegressionTests
    {
        private static NoSQLClient CreateClient(string ns = null) =>
            new NoSQLClient(new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "http://localhost:8080",
                Namespace = ns
            });

        private static PreparedStatement CreateStatement(
            params string[] stores)
        {
            var statement = new PreparedStatement
            {
                RegisterCount = 6,
                DriverQueryPlan = new ReceiveStep()
            };
            foreach (var store in stores)
            {
                statement.AddQueryBranch(new PreparedStatement.QueryBranch
                {
                    ProxyStatement = new byte[] { 1 },
                    StoreName = store
                });
            }
            return statement;
        }

        private static TimestampValue Timestamp(string text) =>
            new TimestampValue(DateTime.Parse(text, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal));

        [DataTestMethod]
        [DataRow("2026-01-01T00:00:00Z", "2026-01-01T00:00:00.1Z", -1)]
        [DataRow("2026-01-01T00:00:00.1Z", "2026-01-01T00:00:00Z", 1)]
        [DataRow("2026-01-01T00:00:00.1Z", "2026-01-01T00:00:00.11Z", -1)]
        [DataRow("2026-01-01T00:00:00.0000001Z", "2026-01-01T00:00:00.0000002Z", -1)]
        [DataRow("2025-12-31T23:59:59.9999999Z", "2026-01-01T00:00:00Z", -1)]
        [DataRow("2026-01-01T00:00:00.1000000Z", "2026-01-01T00:00:00.1Z", 0)]
        [DataRow("2026-01-01T05:30:00+05:30", "2026-01-01T00:00:00Z", 0)]
        public void TestTimestampComparisonAndCase(string left, string right,
            int order)
        {
            VerifyComparisons(Timestamp(left), Timestamp(right), order);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        public void TestStringComparisonsRemainLexical(bool leftTimestamp,
            bool rightTimestamp)
        {
            const string left = "2026-01-01T00:00:00Z";
            const string right = "2026-01-01T00:00:00.1Z";
            // Strings (including a string compared to a timestamp) are not
            // implicitly parsed as dates, matching Java's comparison rules.
            VerifyComparisons(leftTimestamp ? Timestamp(left) :
                    new StringValue(left),
                rightTimestamp ? Timestamp(right) : new StringValue(right), 1);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void TestTimestampComparisonPropagatesNull(bool nullOnLeft)
        {
            var timestamp = Timestamp("2026-01-01T00:00:00Z");
            VerifyComparisons(nullOnLeft ? FieldValue.Null : timestamp,
                nullOnLeft ? timestamp : FieldValue.Null, null);
        }

        [TestMethod]
        public void TestNestedTimestampComparisons()
        {
            VerifyComparisons(new ArrayValue { Timestamp("2026-01-01T00:00:00Z") },
                new ArrayValue { Timestamp("2026-01-01T00:00:00.1Z") }, -1);
            VerifyComparisons(new MapValue
                {
                    ["time"] = Timestamp("2026-01-01T00:00:00.1Z")
                }, new MapValue
                {
                    ["time"] = Timestamp("2026-01-01T00:00:00.1000000Z")
                }, 0, equalityOnly: true);
        }

        private static void VerifyComparisons(FieldValue left, FieldValue right,
            int? order, bool equalityOnly = false)
        {
            using var client = CreateClient();
            var runtime = new QueryRuntime(client, CreateStatement());
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
            for (var i = 0; i < (equalityOnly ? 2 : operations.Length); i++)
            {
                var comparison = new ValueCompareStep
                {
                    ResultPosition = 0,
                    FuncCode = operations[i],
                    LeftStep = new ConstStep { ResultPosition = 1, Value = left },
                    RightStep = new ConstStep { ResultPosition = 2, Value = right }
                };
                var iterator = comparison.CreateSyncIterator(runtime);
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    Assert.IsTrue(iterator.Next());
                    if (order.HasValue)
                    {
                        Assert.AreEqual(expected[i], iterator.Result.AsBoolean,
                            operations[i].ToString());
                    }
                    else
                    {
                        Assert.AreSame(FieldValue.Null, iterator.Result);
                    }
                    Assert.IsFalse(iterator.Next());
                    iterator.Reset();
                }
                var caseIterator = new CaseStep
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
                Assert.IsTrue(caseIterator.Next());
                Assert.AreEqual(expected[i] ? 1 : 0,
                    caseIterator.Result.AsInt32, operations[i].ToString());
                Assert.IsFalse(caseIterator.Next());
            }
        }

        [DataTestMethod]
        [DataRow("request", "branch", "config", "request")]
        [DataRow(null, "branch", "config", "branch")]
        [DataRow(null, "branch", null, "branch")]
        [DataRow("request", null, "config", "request")]
        [DataRow(null, null, "config", "config")]
        [DataRow(null, null, null, null)]
        public void TestPreparedNamespacePrecedence(string requested,
            string prepared, string configured, string expected)
        {
            using var client = CreateClient(configured);
            var statement = new PreparedStatement();
            statement.AddQueryBranch(new PreparedStatement.QueryBranch
            {
                ProxyStatement = new byte[] { 1 }, Namespace = prepared
            });
            statement.AddQueryBranch(new PreparedStatement.QueryBranch
            {
                ProxyStatement = new byte[] { 2 }, Namespace = prepared
            });
            foreach (var options in new[]
                {
                    new QueryOptions { Namespace = requested },
                    requested == null ? null : new QueryOptions { Namespace = requested }
                })
            {
                var request = new QueryRequest<RecordValue>(client, statement,
                    options);
                for (var branch = 0; branch < 2; branch++)
                {
                    request.UnionBranch = branch;
                    Assert.AreEqual(expected, request.Namespace);
                }
            }
            var unprepared = new QueryRequest<RecordValue>(client,
                "SELECT * FROM test", new QueryOptions { Namespace = requested });
            Assert.AreEqual(requested ?? configured, unprepared.Namespace);
        }

        [TestMethod]
        public void TestMissingTopologyRemainsMissingInSnapshot()
        {
            using var client = CreateClient();
            var statement = CreateStatement(null, "newStore");
            var runtime = new QueryRuntime(client, statement);
            client.SetQueryTopology(new TopologyInfo(42, new[] { 1 }));
            client.SetQueryTopology(new TopologyInfo(43, new[] { 2 }, "newStore"));
            for (var branch = 0; branch < 2; branch++)
            {
                runtime.ConstructionUnionBranch = branch;
                Assert.IsNull(runtime.GetConstructionTopology());
                var header = ReadHeader(client, statement, runtime);
                Assert.AreEqual(-1, header[NsonProtocol.FieldNames.TopoSeqNum].AsInt32);
                Assert.IsFalse(header.ContainsKey(
                    NsonProtocol.FieldNames.StoreTopologySequenceNumbers));
            }
            // Requests with no captured runtime still use the current cache.
            Assert.AreEqual(42, new QueryRequest<RecordValue>(client,
                statement, null).QueryTopologySequenceNumber);
        }

        [DataTestMethod]
        [DataRow(ServiceType.KVStore, "25.3.21", false)]
        [DataRow(ServiceType.KVStore, "26.0.99", false)]
        [DataRow(ServiceType.KVStore, "26.1", true)]
        [DataRow(ServiceType.KVStore, "26.1.4", true)]
        [DataRow(ServiceType.KVStore, "26.3.6", true)]
        [DataRow(ServiceType.KVStore, null, true)]
        [DataRow(ServiceType.CloudSim, "26.1.4", false)]
        [DataRow(ServiceType.Cloud, null, false)]
        public void TestUnionCapabilityGuard(ServiceType serviceType,
            string version, bool supported)
        {
            var kvVersion = version == null ? null : Version.Parse(version);
            if (supported)
            {
                QueryTestCapabilities.CheckUnionSupported(serviceType, kvVersion);
            }
            else
            {
                Assert.ThrowsException<AssertInconclusiveException>(() =>
                    QueryTestCapabilities.CheckUnionSupported(serviceType, kvVersion));
            }
        }

        [DataTestMethod]
        [DataRow(3, false)]
        [DataRow(4, false)]
        [DataRow(5, false)]
        [DataRow(6, true)]
        public void TestUnionProxyCapabilityGuard(int version, bool supported)
        {
            if (supported)
            {
                QueryTestCapabilities.CheckUnionQueryVersion((short)version);
            }
            else
            {
                Assert.ThrowsException<AssertInconclusiveException>(() =>
                    QueryTestCapabilities.CheckUnionQueryVersion((short)version));
            }
        }

        [TestMethod]
        [Timeout(30000)]
        public async Task TestConcurrentTopologyUpdatesUseCapturedSnapshots()
        {
            using var client = CreateClient();
            using var stop = new CancellationTokenSource();
            using var started = new ManualResetEventSlim();
            var statement = CreateStatement(null, "storeOne", "storeTwo",
                "unknownStore");
            var updater = Task.Run(() =>
            {
                for (var sequence = 1; !stop.IsCancellationRequested; sequence++)
                {
                    client.SetQueryTopology(new TopologyInfo(3 * sequence,
                        new[] { sequence }));
                    client.SetQueryTopology(new TopologyInfo(3 * sequence + 1,
                        new[] { sequence }, "storeOne"));
                    client.SetQueryTopology(new TopologyInfo(3 * sequence + 2,
                        new[] { sequence }, "storeTwo"));
                    started.Set();
                    Thread.Yield();
                }
            });
            try
            {
                Assert.IsTrue(started.Wait(TimeSpan.FromSeconds(5)));
                for (var attempt = 0; attempt < 2000; attempt++)
                {
                    var runtime = new QueryRuntime(client, statement);
                    for (var branch = 0; branch < statement.QueryBranches.Count;
                         branch++)
                    {
                        runtime.ConstructionUnionBranch = branch;
                        var store = statement.GetStoreName(branch);
                        var captured = store == null ? runtime.BaseTopology :
                            runtime.StoreTopologies.SingleOrDefault(t => t.StoreName == store);
                        Assert.AreSame(captured, runtime.GetConstructionTopology(),
                            "Branch must use the captured topology, not the live cache");
                        var header = ReadHeader(client, statement, runtime);
                        Assert.AreEqual(runtime.BaseTopology?.SequenceNumber ?? -1,
                            header[NsonProtocol.FieldNames.TopoSeqNum].AsInt32);
                        var named = header[NsonProtocol.FieldNames
                            .StoreTopologySequenceNumbers].AsArrayValue;
                        foreach (var topology in runtime.StoreTopologies)
                        {
                            var advertised = named.Single(v => v.AsMapValue[
                                NsonProtocol.FieldNames.StoreId].AsString == topology.StoreName);
                            Assert.AreEqual(topology.SequenceNumber,
                                advertised.AsMapValue[NsonProtocol.FieldNames.TopoSeqNum].AsInt32);
                        }
                    }
                }
            }
            finally
            {
                stop.Cancel();
                await updater;
            }
        }

        private static MapValue ReadHeader(NoSQLClient client,
            PreparedStatement statement, QueryRuntime runtime)
        {
            var request = new QueryRequest<RecordValue>(client, statement, null)
            {
                BaseTopology = runtime.BaseTopology,
                StoreTopologySnapshot = runtime.StoreTopologies
            };
            using var stream = new MemoryStream();
            var writer = NsonProtocol.GetNsonWriter(stream);
            writer.StartMap();
            NsonProtocol.WriteHeader(writer, Opcode.Query, request);
            writer.EndMap();
            stream.Position = 0;
            var reader = NsonProtocol.GetNsonReader(stream);
            reader.Next();
            return NsonProtocol.ReadFieldValue(reader).AsMapValue[
                NsonProtocol.FieldNames.Header].AsMapValue;
        }
    }
}
