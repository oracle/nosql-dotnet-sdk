/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 * https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.IO;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using NsonProtocol;

    [TestClass]
    public class StoreTopologyProtocolTests
    {
        private static NoSQLClient CreateClient() => new NoSQLClient(
            new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "http://localhost:8080"
            });

        private static void ReadResponse(NoSQLClient client, string storeName,
            Action<NsonWriter> writeShards, bool omitStoreName = false)
        {
            using var stream = new MemoryStream();
            var writer = Protocol.GetNsonWriter(stream);
            writer.StartMap();
            writer.StartArray(Protocol.FieldNames.StoreTopologyInfo);
            writer.StartMap();
            if (!omitStoreName)
            {
                writer.WriteString(Protocol.FieldNames.StoreId, storeName);
            }
            writer.WriteInt32(Protocol.FieldNames.ProxyTopoSeqNum, 7);
            writeShards(writer);
            writer.EndMap();
            writer.EndArray();
            writer.EndMap();
            stream.Position = 0;

            var request = new GetRequest<RecordValue>(client, "table",
                new MapValue { ["id"] = 1 }, null);
            new RequestSerializer().DeserializeGet(stream, request);
        }

        private static void WriteValidShards(NsonWriter writer)
        {
            writer.StartArray(Protocol.FieldNames.ShardIds);
            writer.WriteInt32(12);
            writer.WriteInt32(34);
            writer.EndArray();
        }

        [DataTestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow(" ")]
        [DataRow("\t\n")]
        public void TestRejectInvalidStoreIds(string storeName)
        {
            using var client = CreateClient();
            Assert.ThrowsException<BadProtocolException>(() =>
                ReadResponse(client, storeName, WriteValidShards));
            Assert.AreEqual(0, client.StoreTopologies.Count);
        }

        [TestMethod]
        public void TestRejectMissingStoreId()
        {
            using var client = CreateClient();
            Assert.ThrowsException<BadProtocolException>(() =>
                ReadResponse(client, null, WriteValidShards, true));
            Assert.AreEqual(0, client.StoreTopologies.Count);
        }

        [DataTestMethod]
        [DataRow(0)] // Missing field.
        [DataRow(1)] // Explicit null.
        [DataRow(2)] // Empty array.
        public void TestRejectMissingNullOrEmptyShardIds(int encoding)
        {
            using var client = CreateClient();
            var cached = new TopologyInfo(4, new[] { 1 }, "storeOne");
            client.SetQueryTopology(cached);

            Assert.ThrowsException<BadProtocolException>(() =>
                ReadResponse(client, "storeOne", writer =>
                {
                    if (encoding == 1)
                    {
                        writer.WriteNull(Protocol.FieldNames.ShardIds);
                    }
                    else if (encoding == 2)
                    {
                        writer.StartArray(Protocol.FieldNames.ShardIds);
                        writer.EndArray();
                    }
                }));
            Assert.AreSame(cached, client.StoreTopologies.Single(),
                "Invalid topology must not replace the cached shard list");
        }

        [TestMethod]
        public void TestReadValidStoreTopologyPreservesLegacyTopology()
        {
            using var client = CreateClient();
            var legacy = new TopologyInfo(4, new[] { 1 });
            client.SetQueryTopology(legacy);

            ReadResponse(client, "storeOne", WriteValidShards);

            var topology = client.StoreTopologies.Single();
            Assert.AreEqual("storeOne", topology.StoreName);
            Assert.AreEqual(7, topology.SequenceNumber);
            CollectionAssert.AreEqual(new[] { 12, 34 },
                topology.ShardIds.ToArray());
            Assert.AreSame(legacy, client.QueryTopology);
        }
    }
}
