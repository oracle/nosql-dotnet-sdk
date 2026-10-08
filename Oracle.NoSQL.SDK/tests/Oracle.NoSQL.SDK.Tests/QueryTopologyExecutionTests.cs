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
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Query;
    using Protocol = NsonProtocol.Protocol;

    [TestClass]
    public class QueryTopologyExecutionTests
    {
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task TestSingleNamedStoreUsesItsShards(bool hasDefault)
        {
            var handler = new ShardResponseHandler();
            using var client = CreateClient(handler);
            if (hasDefault)
            {
                client.SetQueryTopology(new TopologyInfo(4, new[] { 1 }));
            }
            client.SetQueryTopology(new TopologyInfo(7, new[] { 11, 13 },
                "storeOne"));
            var statement = CreateStatement(new[] { "storeOne" }, false);

            var rows = await ReadAll(client, statement);

            CollectionAssert.AreEqual(new[] { 11, 13 }, rows);
            CollectionAssert.AreEquivalent(new[] { "storeOne:11", "storeOne:13" },
                GetRoutes(handler));
            AssertHeaders(handler, hasDefault ? 4 : -1,
                new Dictionary<string, int> { ["storeOne"] = 7 });
        }

        [TestMethod]
        public async Task TestMissingNamedTopologyDoesNotUseDefaultShards()
        {
            var handler = new ShardResponseHandler();
            using var client = CreateClient(handler);
            client.SetQueryTopology(new TopologyInfo(4, new[] { 1 }));

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() =>
                ReadAll(client, CreateStatement(new[] { "unknown" }, false)));
            Assert.AreEqual(0, handler.Requests.Count);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task TestVirtualScanUsesNamedStoreTopology(bool hasDefault)
        {
            var handler = new ShardResponseHandler { AddVirtualScan = true };
            using var client = CreateClient(handler);
            if (hasDefault)
            {
                client.SetQueryTopology(new TopologyInfo(4, new[] { 100 }));
            }
            client.SetQueryTopology(new TopologyInfo(7, new[] { 11, 13 },
                "storeOne"));

            var rows = await ReadAll(client,
                CreateStatement(new[] { "storeOne" }, false));

            CollectionAssert.AreEqual(new[] { 11, 13, 14 }, rows);
            var virtualRequest = handler.Requests.Select(request =>
                    request[Protocol.FieldNames.Payload].AsMapValue)
                .Single(payload => payload.ContainsKey(Protocol.FieldNames.VirtualScan));
            Assert.AreEqual(14, virtualRequest[Protocol.FieldNames.ShardId].AsInt32);
            Assert.AreEqual(31, virtualRequest[Protocol.FieldNames.VirtualScan]
                .AsMapValue[Protocol.FieldNames.VirtualScanSID].AsInt32);
            AssertHeaders(handler, hasDefault ? 4 : -1,
                new Dictionary<string, int> { ["storeOne"] = 7 });
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task TestUnionKeepsDefaultAndStoreSnapshotsSeparate(
            bool hasDefault, bool sortedUnion)
        {
            var handler = new ShardResponseHandler();
            using var client = CreateClient(handler);
            if (hasDefault)
            {
                client.SetQueryTopology(new TopologyInfo(4, new[] { 1 }));
            }
            client.SetQueryTopology(new TopologyInfo(7, new[] { 11, 13 },
                "storeOne"));
            client.SetQueryTopology(new TopologyInfo(9, new[] { 2, 12 },
                "storeTwo"));
            handler.AfterFirstRequest = () =>
            {
                // A different request may refresh any cache while this query
                // continues. Neither routing nor advertised versions may move.
                client.SetQueryTopology(new TopologyInfo(40, new[] { 99 }));
                client.SetQueryTopology(new TopologyInfo(70, new[] { 101 },
                    "storeOne"));
                client.SetQueryTopology(new TopologyInfo(90, new[] { 102 },
                    "storeTwo"));
            };

            var rows = await ReadAll(client, CreateStatement(
                new[] { "storeOne", "storeTwo" }, sortedUnion));

            CollectionAssert.AreEqual(sortedUnion ? new[] { 2, 11, 12, 13 } :
                new[] { 11, 13, 2, 12 }, rows);
            CollectionAssert.AreEquivalent(new[]
            {
                "storeOne:11", "storeOne:13", "storeTwo:2", "storeTwo:12"
            }, GetRoutes(handler));
            AssertHeaders(handler, hasDefault ? 4 : -1,
                new Dictionary<string, int>
                {
                    ["storeOne"] = 7, ["storeTwo"] = 9
                });
        }

        private static NoSQLClient CreateClient(HttpMessageHandler handler) =>
            new NoSQLClient(new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "http://localhost:8080"
            }, handler);

        private static PreparedStatement CreateStatement(string[] stores,
            bool sortedUnion)
        {
            var steps = stores.Select((_, index) => (PlanStep)new ReceiveStep
            {
                ResultPosition = index,
                DistributionKind = DistributionKind.AllShards,
                SortSpecs = new[] { new SortSpec("id", false, false) }
            }).ToArray();
            var statement = new PreparedStatement
            {
                RegisterCount = steps.Length + 1,
                VariableNames = Array.Empty<string>(),
                OperationCode = QueryRequest.OperationCodeSelect,
                DriverQueryPlan = steps.Length == 1 ? steps[0] : new UnionStep
                {
                    ResultPosition = steps.Length,
                    BranchSteps = steps,
                    SortSpecs = sortedUnion ?
                        new[] { new SortSpec("id", false, false) } : null
                }
            };
            foreach (var store in stores)
            {
                statement.AddQueryBranch(new PreparedStatement.QueryBranch
                {
                    ProxyStatement = new byte[] { 1 }, StoreName = store
                });
            }
            return statement;
        }

        private static async Task<int[]> ReadAll(NoSQLClient client,
            PreparedStatement statement)
        {
            var rows = new List<int>();
            QueryContinuationKey continuation = null;
            for (var batch = 0; batch < 20; batch++)
            {
                var result = await client.QueryAsync(statement, new QueryOptions
                {
                    Limit = 1, ContinuationKey = continuation
                });
                rows.AddRange(result.Rows.Select(row => row["id"].AsInt32));
                continuation = result.ContinuationKey;
                if (continuation == null)
                {
                    return rows.ToArray();
                }
            }
            Assert.Fail("Query did not finish within 20 continuation calls");
            return null;
        }

        private static string[] GetRoutes(ShardResponseHandler handler) =>
            handler.Requests.Select(request =>
            {
                var payload = request[Protocol.FieldNames.Payload].AsMapValue;
                return payload[Protocol.FieldNames.StoreId].AsString + ":" +
                    payload[Protocol.FieldNames.ShardId].AsInt32;
            }).ToArray();

        private static void AssertHeaders(ShardResponseHandler handler,
            int defaultSequence, IDictionary<string, int> storeSequences)
        {
            foreach (var request in handler.Requests)
            {
                var header = request[Protocol.FieldNames.Header].AsMapValue;
                Assert.AreEqual(defaultSequence,
                    header[Protocol.FieldNames.TopoSeqNum].AsInt32,
                    "The legacy header must describe the captured default store");
                var stores = header[Protocol.FieldNames.StoreTopologySequenceNumbers]
                    .AsArrayValue.Select(value => value.AsMapValue).ToDictionary(
                        value => value[Protocol.FieldNames.StoreId].AsString,
                        value => value[Protocol.FieldNames.TopoSeqNum].AsInt32);
                Assert.AreEqual(storeSequences.Count, stores.Count);
                foreach (var pair in storeSequences)
                {
                    Assert.AreEqual(pair.Value, stores[pair.Key]);
                }
            }
        }

        private sealed class ShardResponseHandler : HttpMessageHandler
        {
            internal List<MapValue> Requests { get; } = new List<MapValue>();
            internal Action AfterFirstRequest { get; set; }
            internal bool AddVirtualScan { get; set; }

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var bytes = await request.Content.ReadAsByteArrayAsync(
                    cancellationToken);
                using var input = Protocol.GetMemoryStreamWithVisibleBuffer(bytes);
                input.Position = sizeof(short);
                var reader = Protocol.GetNsonReader(input);
                reader.Next();
                var root = Protocol.ReadFieldValue(reader).AsMapValue;
                Requests.Add(root);
                if (Requests.Count == 1)
                {
                    AfterFirstRequest?.Invoke();
                }
                var shardId = root[Protocol.FieldNames.Payload].AsMapValue[
                    Protocol.FieldNames.ShardId].AsInt32;
                using var output = new MemoryStream();
                var writer = Protocol.GetNsonWriter(output);
                writer.StartMap();
                writer.StartArray(Protocol.FieldNames.QueryResults);
                writer.StartMap();
                writer.WriteInt32("id", shardId);
                writer.EndMap();
                writer.EndArray();
                if (AddVirtualScan && Requests.Count == 1)
                {
                    writer.StartArray(Protocol.FieldNames.VirtualScans);
                    writer.StartMap();
                    writer.WriteInt32(Protocol.FieldNames.VirtualScanSID, 31);
                    writer.WriteInt32(Protocol.FieldNames.VirtualScanPID, 1);
                    writer.WriteInt32(Protocol.FieldNames.VirtualScanNumTables, 1);
                    writer.WriteBoolean(Protocol.FieldNames.VirtualScanJoinPathMatched,
                        false);
                    writer.EndMap();
                    writer.EndArray();
                }
                writer.EndMap();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(output.ToArray())
                };
            }
        }
    }
}
