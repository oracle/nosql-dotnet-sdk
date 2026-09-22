/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using NsonProtocol;
    using Query;

    [TestClass]
    public class TopologyTests
    {
        private static NoSQLClient MakeClient() => new NoSQLClient(
            new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "localhost:8080"
            });

        private static TopologyInfo Topology(string store, int sequence,
            params int[] shards) => new TopologyInfo(sequence, shards, store);

        private static GetRequest<RecordValue> GetRequest(NoSQLClient client,
            string table = "table") => new GetRequest<RecordValue>(client,
                table, new MapValue { ["id"] = 1 }, null);

        private static PreparedStatement Prepared(string store = null) =>
            new PreparedStatement
            {
                SQLText = "select * from table",
                ProxyStatement = new byte[] { 1, 2, 3 },
                TableName = "table",
                StoreName = store,
                OperationCode = QueryRequest.OperationCodeSelect,
                DriverQueryPlan = new ReceiveStep(),
                RegisterCount = 1
            };

        private static MemoryStream Response(Action<NsonWriter> write)
        {
            var stream = new MemoryStream();
            var writer = Protocol.GetNsonWriter(stream);
            writer.StartMap();
            write(writer);
            writer.EndMap();
            stream.Position = 0;
            return stream;
        }

        private static void WriteTopology(NsonWriter writer,
            TopologyInfo topology, string field = null)
        {
            if (field != null)
            {
                writer.WriteFieldName(field);
            }
            writer.StartMap();
            if (topology.StoreName != null)
            {
                writer.WriteString("sid", topology.StoreName);
            }
            writer.WriteInt32("pn", topology.SequenceNumber);
            writer.StartArray("sa");
            foreach (var shard in topology.ShardIds)
            {
                writer.WriteInt32(shard);
            }
            writer.EndArray();
            writer.EndMap();
        }

        private static void WriteStores(NsonWriter writer,
            params TopologyInfo[] topologies)
        {
            writer.StartArray("stp");
            foreach (var topology in topologies)
            {
                WriteTopology(writer, topology);
            }
            writer.EndArray();
        }

        private static MapValue Serialize(Request request)
        {
            using var stream = new MemoryStream();
            request.Serialize(new RequestSerializer(), stream);
            stream.Position = 0;
            var reader = Protocol.GetNsonReader(stream);
            reader.Next();
            return Protocol.ReadMapValue(reader);
        }

        private static Dictionary<string, int> StoreSequences(MapValue request)
            => request["h"]["sts"].AsArrayValue.ToDictionary(
                entry => entry["sid"].AsString,
                entry => entry["ts"].AsInt32);

        private static void ReadGet(NoSQLClient client,
            Action<NsonWriter> write)
        {
            using var response = Response(write);
            new RequestSerializer().DeserializeGet(response,
                GetRequest(client));
        }

        private static void InstallTransport(NoSQLClient client,
            HttpMessageHandler handler)
        {
            // Replace only HTTP; retain real serialization, response handling,
            // query execution, retries, and continuation behavior.
            var sdkClientField = typeof(NoSQLClient).GetField("client",
                BindingFlags.Instance | BindingFlags.NonPublic);
            var sdkClient = sdkClientField.GetValue(client);
            var httpClientField = sdkClient.GetType().GetField("client",
                BindingFlags.Instance | BindingFlags.NonPublic);
            ((HttpClient)httpClientField.GetValue(sdkClient)).Dispose();
            httpClientField.SetValue(sdkClient, new HttpClient(handler)
            {
                BaseAddress = new Uri("http://localhost:8080"),
                Timeout = Timeout.InfiniteTimeSpan
            });
        }

        [TestMethod]
        public void CacheKeepsNewestTopologyIndependentlyPerStore()
        {
            using var client = MakeClient();
            var storeA = Topology("A", 100, 1, 2);
            var storeB = Topology("B", 2, 7, 8);
            client.SetStoreTopologies(new[] { storeA, storeB });
            client.SetStoreTopologies(new[]
            {
                Topology("A", 99, 99), Topology("B", 2, 99)
            });
            var snapshot = client.GetQueryTopologySnapshot();
            Assert.AreSame(storeA, snapshot.StoreTopologies["A"]);
            Assert.AreSame(storeB, snapshot.StoreTopologies["B"]);
            Assert.IsNull(snapshot.LegacyTopology);

            using var otherClient = MakeClient();
            Assert.AreEqual(0,
                otherClient.GetQueryTopologySnapshot().StoreTopologies.Count);
        }

        [TestMethod]
        public void ConcurrentUpdatesAndSnapshotsPreserveNewestSequence()
        {
            using var client = MakeClient();
            Parallel.For(1, 201, sequence =>
            {
                client.SetStoreTopologies(new[]
                {
                    Topology("A", sequence, sequence),
                    Topology("B", 201 - sequence, sequence)
                });
                var snapshot = client.GetQueryTopologySnapshot();
                Assert.IsTrue(snapshot.StoreTopologies["A"].SequenceNumber >=
                    sequence);
            });
            var final = client.GetQueryTopologySnapshot();
            Assert.AreEqual(200, final.StoreTopologies["A"].SequenceNumber);
            Assert.AreEqual(200, final.StoreTopologies["B"].SequenceNumber);
        }

        [TestMethod]
        public void SnapshotsAndShardListsCannotBeChangedByCacheUpdates()
        {
            using var client = MakeClient();
            var shards = new[] { 1, 2 };
            client.SetStoreTopologies(new[] { Topology("A", 1, shards) });
            var snapshot = client.GetQueryTopologySnapshot();
            shards[0] = 99;
            client.SetStoreTopologies(new[] { Topology("A", 2, 3, 4) });
            var cached = snapshot.StoreTopologies["A"];
            CollectionAssert.AreEqual(new[] { 1, 2 }, cached.ShardIds.ToArray());
            Assert.ThrowsException<NotSupportedException>(() =>
                ((IList<int>)cached.ShardIds)[0] = 99);
            Assert.ThrowsException<NotSupportedException>(() =>
                ((IDictionary<string, TopologyInfo>)snapshot.StoreTopologies)
                    .Clear());
        }

        [TestMethod]
        public void EmptyCacheAdvertisesPerStoreSupportAndLegacySentinel()
        {
            using var client = MakeClient();
            var request = Serialize(GetRequest(client));
            Assert.AreEqual(-1, request["h"]["ts"].AsInt32);
            Assert.AreEqual(0, request["h"]["sts"].AsArrayValue.Count);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void WriteManyAdvertisesAndRefreshesStoreTopology(bool multiTable)
        {
            using var client = MakeClient();
            var row = new MapValue { ["id"] = 1 };
            var operations = new WriteOperationCollection();
            if (multiTable)
            {
                operations.AddPut("table", row);
            }
            else
            {
                operations.AddPut(row);
            }
            var request = new WriteManyRequest<RecordValue>(client,
                multiTable ? null : "table", operations, null);
            Assert.AreEqual(0, StoreSequences(Serialize(request)).Count);
            client.SetQueryTopology(Topology(null, 7, 99));
            client.SetStoreTopologies(new[] { Topology("A", 1, 1) });
            var wire = Serialize(request);
            Assert.AreEqual(7, wire["h"]["ts"].AsInt32);
            Assert.AreEqual(1, StoreSequences(wire)["A"]);

            using var response = Response(writer =>
            {
                writer.StartArray("ws");
                writer.StartMap();
                writer.WriteBoolean("ss", true);
                writer.EndMap();
                writer.EndArray();
                WriteStores(writer, Topology("A", 2, 2));
            });
            new RequestSerializer().DeserializeWriteMany<RecordValue>(response,
                request);
            Assert.AreEqual(2, client.GetQueryTopologySnapshot()
                .StoreTopologies["A"].SequenceNumber);
            Assert.AreEqual(7, client.QueryTopologySequenceNumber);
        }

        [TestMethod]
        public void HeadersAdvertiseEveryStoreAlongsideLegacySequence()
        {
            using var client = MakeClient();
            client.SetQueryTopology(Topology(null, 7, 1));
            client.SetStoreTopologies(new[]
            {
                Topology("A", 100, 1), Topology("B", 2, 7)
            });
            foreach (var table in new[] { "tableA", "tableB" })
            {
                var wire = Serialize(GetRequest(client, table));
                Assert.AreEqual(7, wire["h"]["ts"].AsInt32);
                var sequences = StoreSequences(wire);
                Assert.AreEqual(2, sequences.Count);
                Assert.AreEqual(100, sequences["A"]);
                Assert.AreEqual(2, sequences["B"]);
            }
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void StoreResponsesTakePrecedenceOverLegacyRegardlessOfOrder(
            bool storeFirst, bool emptyStores)
        {
            using var client = MakeClient();
            var legacy = Topology(null, 7, 1);
            client.SetQueryTopology(legacy);
            ReadGet(client, writer =>
            {
                var stores = emptyStores ? Array.Empty<TopologyInfo>() :
                    new[] { Topology("A", 3, 5) };
                if (storeFirst)
                {
                    WriteStores(writer, stores);
                }
                WriteTopology(writer, Topology(null, 99, 99), "tp");
                if (!storeFirst)
                {
                    WriteStores(writer, stores);
                }
            });
            Assert.AreSame(legacy, client.QueryTopology);
            Assert.AreEqual(emptyStores ? 0 : 1,
                client.GetQueryTopologySnapshot().StoreTopologies.Count);
        }

        [TestMethod]
        public void LegacyResponsesRemainIndependentFromStoreCache()
        {
            using var client = MakeClient();
            client.SetStoreTopologies(new[] { Topology("A", 1, 5) });
            ReadGet(client, writer =>
                WriteTopology(writer, Topology(null, 7, 1, 2), "tp"));
            ReadGet(client, writer =>
                WriteTopology(writer, Topology(null, 6, 99), "tp"));
            Assert.AreEqual(7, client.QueryTopologySequenceNumber);
            Assert.AreEqual(1, client.GetQueryTopologySnapshot()
                .StoreTopologies["A"].SequenceNumber);
        }

        [DataTestMethod]
        [DataRow("missingStore")]
        [DataRow("emptyStore")]
        [DataRow("missingSequence")]
        [DataRow("negativeSequence")]
        [DataRow("missingShards")]
        [DataRow("emptyShards")]
        [DataRow("wrongStoreType")]
        public void InvalidTopologyDoesNotPartiallyUpdateCache(string invalid)
        {
            using var client = MakeClient();
            Assert.ThrowsException<BadProtocolException>(() =>
                ReadGet(client, writer =>
                {
                    writer.StartArray("stp");
                    WriteTopology(writer, Topology("valid", 1, 1));
                    writer.StartMap();
                    if (invalid == "wrongStoreType")
                    {
                        writer.WriteInt32("sid", 5);
                    }
                    else if (invalid != "missingStore")
                    {
                        writer.WriteString("sid",
                            invalid == "emptyStore" ? " " : "invalid");
                    }
                    if (invalid != "missingSequence")
                    {
                        writer.WriteInt32("pn",
                            invalid == "negativeSequence" ? -1 : 1);
                    }
                    if (invalid != "missingShards")
                    {
                        writer.StartArray("sa");
                        if (invalid != "emptyShards")
                        {
                            writer.WriteInt32(1);
                        }
                        writer.EndArray();
                    }
                    writer.EndMap();
                    writer.EndArray();
                }));
            Assert.AreEqual(0,
                client.GetQueryTopologySnapshot().StoreTopologies.Count);
        }

        [TestMethod]
        public void RejectsTooManyStoreTopologiesBeforeReadingEntries()
        {
            using var client = MakeClient();
            var exception = Assert.ThrowsException<BadProtocolException>(() =>
                ReadGet(client, writer =>
                {
                    writer.StartArray("stp");
                    for (var i = 0; i < 10_001; i++)
                    {
                        writer.StartMap();
                        writer.EndMap();
                    }
                    writer.EndArray();
                }));
            StringAssert.Contains(exception.Message,
                "Too many store topology entries");
            Assert.AreEqual(0,
                client.GetQueryTopologySnapshot().StoreTopologies.Count);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void InvalidQueryV3TopologyDoesNotPublishLegacyResponseTopology(
            bool query)
        {
            using var client = MakeClient();
            using var response = Response(writer =>
            {
                WriteTopology(writer, Topology(null, 1, 1), "tp");
                writer.WriteByteArray("pq", new byte[] { 1 });
                // A flat V3 topology without shard IDs is invalid.
                writer.WriteInt32("pn", 2);
            });
            var serializer = new RequestSerializer();
            Assert.ThrowsException<BadProtocolException>(() =>
            {
                if (query)
                {
                    serializer.DeserializeQuery(response,
                        new QueryRequest<RecordValue>(client,
                            "select * from table", null)
                        {
                            QueryVersion = QueryRequestBase.QueryV3
                        });
                }
                else
                {
                    serializer.DeserializePrepare(response,
                        new PrepareRequest(client, "select * from table", null)
                        {
                            QueryVersion = QueryRequestBase.QueryV3
                        });
                }
            });
            Assert.IsNull(client.QueryTopology);
        }

        [TestMethod]
        public void ErrorResponseDoesNotPopulateTopologyCache()
        {
            using var client = MakeClient();
            Assert.ThrowsException<BadProtocolException>(() =>
                ReadGet(client, writer =>
                {
                    WriteStores(writer, Topology("A", 1, 1));
                    writer.WriteInt32("e", 9999);
                    writer.WriteString("x", "test error");
                }));
            Assert.AreEqual(0,
                client.GetQueryTopologySnapshot().StoreTopologies.Count);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void InvalidPreparedResponseDoesNotUpdateTopologyCache(
            bool query, bool perStore)
        {
            using var client = MakeClient();
            using var response = Response(writer =>
            {
                if (perStore)
                {
                    WriteStores(writer, Topology("A", 1, 1));
                }
                else
                {
                    WriteTopology(writer, Topology(null, 1, 1), "tp");
                }
                // No server prepared statement: reject the entire response.
                writer.WriteString("n", "table");
            });
            var serializer = new RequestSerializer();
            Assert.ThrowsException<BadProtocolException>(() =>
            {
                if (query)
                {
                    serializer.DeserializeQuery(response,
                        new QueryRequest<RecordValue>(client,
                            "select * from table", null));
                }
                else
                {
                    serializer.DeserializePrepare(response,
                        new PrepareRequest(client, "select * from table", null));
                }
            });
            Assert.IsNull(client.QueryTopology);
            Assert.AreEqual(0,
                client.GetQueryTopologySnapshot().StoreTopologies.Count);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void PrepareResolvesStoreWithFlatOrBranchMetadataInAnyOrder(
            bool branchFormat, bool storeFirst)
        {
            using var client = MakeClient();
            using var response = Response(writer =>
            {
                if (storeFirst)
                {
                    WriteQueryStore(writer, "A");
                }
                if (branchFormat)
                {
                    writer.StartArray("qb");
                    writer.StartMap();
                }
                writer.WriteByteArray("pq", new byte[] { 1, 2, 3 });
                writer.WriteString("n", "table");
                writer.WriteString("ns", "namespace");
                if (branchFormat)
                {
                    writer.EndMap();
                    writer.EndArray();
                }
                WriteStores(writer, Topology("A", 2, 5));
                if (!storeFirst)
                {
                    WriteQueryStore(writer, "A");
                }
            });
            var prepared = new RequestSerializer().DeserializePrepare(response,
                new PrepareRequest(client, "select * from table", null));
            Assert.AreEqual("A", prepared.StoreName);
            Assert.AreEqual("table", prepared.TableName);
            Assert.AreEqual("namespace", prepared.Namespace);
            Assert.AreEqual("A", prepared.CopyStatement().StoreName);
            Assert.AreEqual(2, client.GetQueryTopologySnapshot()
                .StoreTopologies["A"].SequenceNumber);

            var request = new QueryRequest<RecordValue>(client, prepared, null);
            Assert.AreEqual("A", Serialize(request)["p"]["sid"].AsString);
        }

        private static void WriteQueryStore(NsonWriter writer,
            params string[] stores)
        {
            writer.StartArray("qbs");
            foreach (var store in stores)
            {
                writer.WriteString(store);
            }
            writer.EndArray();
        }

        [TestMethod]
        public void FirstQueryResponsePreservesStoreInContinuation()
        {
            using var client = MakeClient();
            var request = new QueryRequest<RecordValue>(client,
                "select * from table", null);
            using var response = Response(writer =>
            {
                WriteQueryStore(writer, "A");
                WriteStores(writer, Topology("A", 1, 5));
                writer.WriteByteArray("pq", new byte[] { 1 });
                writer.WriteString("n", "table");
                writer.StartArray("qr");
                writer.EndArray();
                writer.WriteByteArray("ck", new byte[] { 2 });
            });
            var result = new RequestSerializer().DeserializeQuery(response,
                request);
            request.ApplyResult(result);
            Assert.AreEqual("A", request.PreparedStatement.StoreName);
            Assert.AreSame(request.PreparedStatement,
                result.ContinuationKey.PreparedStatement);
            Assert.AreEqual("A", Serialize(request)["p"]["sid"].AsString);
        }

        [DataTestMethod]
        [DataRow(0)]
        [DataRow(2)]
        public void RejectsUnsupportedStoreAndBranchCounts(int count)
        {
            using var client = MakeClient();
            foreach (var field in new[] { "qbs", "qb" })
            {
                using var response = Response(writer =>
                {
                    writer.WriteByteArray("pq", new byte[] { 1 });
                    writer.StartArray(field);
                    for (var i = 0; i < count; i++)
                    {
                        if (field == "qbs")
                        {
                            writer.WriteString("A");
                        }
                        else
                        {
                            writer.StartMap();
                            writer.WriteByteArray("pq", new byte[] { 1 });
                            writer.EndMap();
                        }
                    }
                    writer.EndArray();
                });
                Assert.ThrowsException<BadProtocolException>(() =>
                    new RequestSerializer().DeserializePrepare(response,
                        new PrepareRequest(client, "select * from table", null)));
            }
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow(" ")]
        public void RejectsEmptyQueryStoreName(string store)
        {
            using var client = MakeClient();
            using var response = Response(writer =>
            {
                writer.WriteByteArray("pq", new byte[] { 1 });
                WriteQueryStore(writer, store);
            });
            Assert.ThrowsException<BadProtocolException>(() =>
                new RequestSerializer().DeserializePrepare(response,
                    new PrepareRequest(client, "select * from table", null)));
        }

        [TestMethod]
        public void AdvancedQueryUsesItsStoreAndFreezesAllOutgoingSequences()
        {
            using var client = MakeClient();
            client.SetQueryTopology(Topology(null, 7, 99));
            client.SetStoreTopologies(new[]
            {
                Topology("A", 10, 1, 2), Topology("B", 100, 5, 6)
            });
            var prepared = Prepared("A");
            var runtime = new QueryRuntime(client, prepared)
            {
                Request = new QueryRequest<RecordValue>(client, prepared, null)
            };
            client.SetStoreTopologies(new[] { Topology("A", 11, 3, 4) });
            client.SetQueryTopology(Topology(null, 8, 100));

            // Exercise the actual internal-fetch construction used by queries.
            var iterator = new ReceiveIterator(runtime, new ReceiveStep());
            var field = typeof(ReceiveIterator).GetField("queryRequest",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(field);
            var internalRequest = (QueryRequest<RecordValue>)field.GetValue(
                iterator);
            var wire = Serialize(internalRequest);
            CollectionAssert.AreEqual(new[] { 1, 2 },
                runtime.BaseTopology.ShardIds.ToArray());
            Assert.AreEqual(7, wire["h"]["ts"].AsInt32);
            Assert.AreEqual(10, StoreSequences(wire)["A"]);
            Assert.AreEqual(100, StoreSequences(wire)["B"]);
            Assert.AreEqual("A", wire["p"]["sid"].AsString);

            var nextExecution = new QueryRuntime(client, prepared);
            Assert.AreEqual(11, nextExecution.BaseTopology.SequenceNumber);
        }

        [TestMethod]
        public void MissingStoreRequiresReprepareEvenWhenOtherTopologiesExist()
        {
            using var client = MakeClient();
            client.SetQueryTopology(Topology(null, 7, 1));
            client.SetStoreTopologies(new[] { Topology("B", 100, 5) });
            var exception = Assert.ThrowsException<PrepareQueryException>(() =>
                new QueryRuntime(client, Prepared("A")));
            StringAssert.Contains(exception.Message, "Prepare the query again");
            Assert.IsFalse(exception.IsRetryable);
        }

        [TestMethod]
        public void LegacyQueryRequiresLegacyTopologyAndNeverGuessesStore()
        {
            using var client = MakeClient();
            client.SetStoreTopologies(new[] { Topology("A", 100, 5) });
            Assert.ThrowsException<PrepareQueryException>(() =>
                new QueryRuntime(client, Prepared()));
            client.SetQueryTopology(Topology(null, 7, 1));
            var runtime = new QueryRuntime(client, Prepared());
            Assert.AreEqual(7, runtime.BaseTopology.SequenceNumber);
            Assert.IsNull(runtime.BaseTopology.StoreName);
            var wire = Serialize(new QueryRequest<RecordValue>(client,
                Prepared(), null));
            Assert.IsFalse(wire["p"].AsMapValue.ContainsKey("sid"));
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void QueryV3UsesLegacyTopologyUnlessStoreTopologiesArePresent(
            bool perStore, bool query)
        {
            using var client = MakeClient();
            using var response = Response(writer =>
            {
                writer.WriteByteArray("pq", new byte[] { 1 });
                writer.WriteInt32("pn", 5);
                writer.StartArray("sa");
                writer.WriteInt32(1);
                writer.EndArray();
                if (perStore)
                {
                    WriteStores(writer, Topology("A", 2, 7));
                }
            });
            PreparedStatement prepared;
            if (query)
            {
                var request = new QueryRequest<RecordValue>(client,
                    "select * from table", null)
                {
                    QueryVersion = QueryRequestBase.QueryV3
                };
                prepared = new RequestSerializer().DeserializeQuery(response,
                    request).PreparedStatement;
            }
            else
            {
                var request = new PrepareRequest(client,
                    "select * from table", null)
                {
                    QueryVersion = QueryRequestBase.QueryV3
                };
                prepared = new RequestSerializer().DeserializePrepare(response,
                    request);
            }
            Assert.IsNull(prepared.StoreName);
            Assert.AreEqual(perStore ? -1 : 5,
                client.QueryTopologySequenceNumber);
            Assert.AreEqual(perStore ? 1 : 0,
                client.GetQueryTopologySnapshot().StoreTopologies.Count);
        }

        [TestMethod]
        public void BinarySerializationIsUnaffectedByStoreCache()
        {
            using var client = MakeClient();
            client.SetQueryTopology(Topology(null, 5, 1));
            var request = new QueryRequest<RecordValue>(client, Prepared(), null)
            {
                QueryVersion = QueryRequestBase.QueryV3
            };
            var serializer = new BinaryProtocol.RequestSerializer();
            request.Init();
            using var before = new MemoryStream();
            serializer.SerializeQuery(before, request);
            client.SetStoreTopologies(new[] { Topology("A", 100, 5) });
            using var after = new MemoryStream();
            serializer.SerializeQuery(after, request);
            CollectionAssert.AreEqual(before.ToArray(), after.ToArray());
        }

        [TestMethod]
        public void BinaryProtocolRejectsStoreAwarePreparedQuery()
        {
            using var client = MakeClient();
            var request = new QueryRequest<RecordValue>(client,
                Prepared("A"), null) { QueryVersion = QueryRequestBase.QueryV3 };
            request.Init();
            using var stream = new MemoryStream();
            Assert.ThrowsException<PrepareQueryException>(() =>
                new BinaryProtocol.RequestSerializer().SerializeQuery(stream,
                    request));
            Assert.AreEqual(0L, stream.Length);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        [Timeout(10000)]
        public async Task ProtocolDowngradeRequiresReprepareBeforeSendingBinary(
            bool simple, bool alreadyDowngraded)
        {
            using var client = MakeClient();
            client.SetStoreTopologies(new[] { Topology("A", 1, 1) });
            using var handler = new StubResponseHandler(_ => new byte[] { 24 });
            InstallTransport(client, handler);
            var prepared = Prepared("A");
            if (simple)
            {
                prepared.DriverQueryPlan = null;
            }
            if (alreadyDowngraded)
            {
                Assert.IsTrue(client.ProtocolHandler.DecrementSerialVersion(4));
            }
            await Assert.ThrowsExceptionAsync<PrepareQueryException>(() =>
                client.QueryAsync(prepared));
            Assert.AreEqual(alreadyDowngraded ? 0 : 1, handler.Requests.Count);
            Assert.AreEqual(3, client.ProtocolHandler.SerialVersion);
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task AllShardScansRetainTheirStoreSnapshotAfterCacheUpdate()
        {
            using var client = MakeClient();
            client.SetQueryTopology(Topology(null, 7, 99));
            client.SetStoreTopologies(new[]
            {
                Topology("A", 10, 1, 2), Topology("B", 100, 5, 6)
            });
            using var handler = new StubResponseHandler(request =>
            {
                using var response = Response(writer =>
                {
                    writer.StartArray("qr");
                    writer.StartMap();
                    writer.WriteInt32("id", request["p"]["si"].AsInt32);
                    writer.EndMap();
                    writer.EndArray();
                    WriteStores(writer, Topology("A", 11, 3, 4));
                });
                return response.ToArray();
            });
            InstallTransport(client, handler);
            var prepared = Prepared("A");
            prepared.DriverQueryPlan = new ReceiveStep
            {
                DistributionKind = DistributionKind.AllShards,
                SortSpecs = Array.Empty<SortSpec>()
            };
            var rows = new List<int>();
            await foreach (var result in client.GetQueryAsyncEnumerable(prepared))
            {
                rows.AddRange(result.Rows.Select(row => row["id"].AsInt32));
            }
            CollectionAssert.AreEqual(new[] { 1, 2 }, rows);
            Assert.AreEqual(2, handler.Requests.Count);
            foreach (var request in handler.Requests)
            {
                Assert.AreEqual("A", request["p"]["sid"].AsString);
                Assert.AreEqual(10, StoreSequences(request)["A"]);
                Assert.AreEqual(100, StoreSequences(request)["B"]);
                Assert.AreEqual(7, request["h"]["ts"].AsInt32);
            }
            Assert.AreEqual(11, client.GetQueryTopologySnapshot()
                .StoreTopologies["A"].SequenceNumber);
        }

        [TestMethod]
        public void DisposingClientClearsBothCaches()
        {
            using var client = MakeClient();
            client.SetQueryTopology(Topology(null, 1, 1));
            client.SetStoreTopologies(new[] { Topology("A", 1, 1) });
            client.Dispose();
            var snapshot = client.GetQueryTopologySnapshot();
            Assert.IsNull(snapshot.LegacyTopology);
            Assert.AreEqual(0, snapshot.StoreTopologies.Count);
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task QueryBatchesUsePostPrepareSnapshotThroughHttpPath()
        {
            using var client = MakeClient();
            using var handler = new QueryResponseHandler();
            InstallTransport(client, handler);

            var first = await client.QueryAsync("select * from table");
            Assert.AreEqual(1, first.Rows.Single()["id"].AsInt32);
            Assert.IsNotNull(first.ContinuationKey);
            Assert.AreEqual(11, client.GetQueryTopologySnapshot()
                .StoreTopologies["A"].SequenceNumber);
            var runtime = first.ContinuationKey.Runtime;
            Assert.AreEqual(10, runtime.BaseTopology.SequenceNumber);
            Assert.AreSame(runtime.TopologySnapshot,
                runtime.Request.ExecutionTopology);

            var second = await client.QueryAsync("select * from table",
                new QueryOptions { ContinuationKey = first.ContinuationKey });
            Assert.AreEqual(2, second.Rows.Single()["id"].AsInt32);
            Assert.IsNull(second.ContinuationKey);

            // A fresh execution must pick up the update from the first batch.
            await client.QueryAsync(runtime.PreparedStatement);
            Assert.AreEqual(4, handler.Requests.Count);
            Assert.AreEqual(0, StoreSequences(handler.Requests[0]).Count);
            foreach (var request in handler.Requests.Skip(1).Take(2))
            {
                Assert.AreEqual("A", request["p"]["sid"].AsString);
                Assert.AreEqual(10, StoreSequences(request)["A"]);
                Assert.AreEqual(100, StoreSequences(request)["B"]);
                Assert.AreEqual(-1, request["h"]["ts"].AsInt32);
            }
            Assert.AreEqual(11, StoreSequences(handler.Requests[3])["A"]);
        }

        [TestMethod]
        [Timeout(10000)]
        public async Task ContinuationRejectsProtocolDowngrade()
        {
            using var client = MakeClient();
            using var handler = new QueryResponseHandler(3);
            InstallTransport(client, handler);
            var first = await client.QueryAsync("select * from table");
            Assert.IsNotNull(first.ContinuationKey);
            await Assert.ThrowsExceptionAsync<PrepareQueryException>(() =>
                client.QueryAsync("select * from table", new QueryOptions
                {
                    ContinuationKey = first.ContinuationKey
                }));
            Assert.AreEqual(3, handler.Requests.Count);
        }

        private sealed class StubResponseHandler : HttpMessageHandler
        {
            private readonly Func<MapValue, byte[]> respond;

            internal List<MapValue> Requests { get; } = new List<MapValue>();

            internal StubResponseHandler(Func<MapValue, byte[]> respond)
            {
                this.respond = respond;
            }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var bytes = await request.Content.ReadAsByteArrayAsync(
                    cancellationToken);
                using var stream = Protocol.GetMemoryStreamWithVisibleBuffer(bytes);
                Assert.AreEqual(4,
                    BinaryProtocol.Protocol.ReadUnpackedInt16(stream));
                var reader = Protocol.GetNsonReader(stream);
                reader.Next();
                var wire = Protocol.ReadMapValue(reader);
                Requests.Add(wire);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(respond(wire))
                };
            }
        }

        private sealed class QueryResponseHandler : HttpMessageHandler
        {
            private readonly int unsupportedAt;

            internal QueryResponseHandler(int unsupportedAt = 0)
            {
                this.unsupportedAt = unsupportedAt;
            }

            internal List<MapValue> Requests { get; } = new List<MapValue>();

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var bytes = await request.Content.ReadAsByteArrayAsync(
                    cancellationToken);
                using var stream = Protocol.GetMemoryStreamWithVisibleBuffer(
                    bytes);
                Assert.AreEqual(4,
                    BinaryProtocol.Protocol.ReadUnpackedInt16(stream));
                var reader = Protocol.GetNsonReader(stream);
                reader.Next();
                Requests.Add(Protocol.ReadMapValue(reader));
                if (Requests.Count == unsupportedAt)
                {
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new ByteArrayContent(new byte[] { 24 })
                    };
                }
                using var response = Response(writer =>
                {
                    if (Requests.Count == 1)
                    {
                        writer.WriteByteArray("pq", new byte[] { 1 });
                        writer.WriteByteArray("dq", ReceivePlan());
                        writer.WriteString("n", "table");
                        writer.WriteInt32("qo", QueryRequest.OperationCodeSelect);
                        WriteQueryStore(writer, "A");
                        WriteStores(writer, Topology("A", 10, 1, 2),
                            Topology("B", 100, 5, 6));
                        writer.StartArray("qr");
                        writer.EndArray();
                    }
                    else
                    {
                        writer.StartArray("qr");
                        writer.StartMap();
                        writer.WriteInt32("id", Requests.Count - 1);
                        writer.EndMap();
                        writer.EndArray();
                        if (Requests.Count == 2)
                        {
                            writer.WriteBoolean("re", true);
                            writer.WriteByteArray("ck", new byte[] { 1 });
                            WriteStores(writer, Topology("A", 11, 3, 4));
                        }
                    }
                });
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(response.ToArray())
                };
            }

            private static byte[] ReceivePlan()
            {
                using var stream = new MemoryStream();
                BinaryProtocol.Protocol.WriteByte(stream, 17); // RECV
                // Result register, iterator state, and four source positions.
                for (var i = 0; i < 6; i++)
                {
                    BinaryProtocol.Protocol.WriteUnpackedInt32(stream, 0);
                }
                BinaryProtocol.Protocol.WriteUnpackedInt16(stream,
                    (short)DistributionKind.AllPartitions);
                // No sort fields, sort specs, or primary-key fields.
                for (var i = 0; i < 3; i++)
                {
                    BinaryProtocol.Protocol.WritePackedInt32(stream, -1);
                }
                BinaryProtocol.Protocol.WriteUnpackedInt32(stream, 1); // states
                BinaryProtocol.Protocol.WriteUnpackedInt32(stream, 1); // registers
                BinaryProtocol.Protocol.WriteUnpackedInt32(stream, 0); // variables
                return stream.ToArray();
            }
        }
    }
}
