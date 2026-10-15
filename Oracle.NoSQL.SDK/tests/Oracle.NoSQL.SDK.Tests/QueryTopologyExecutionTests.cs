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
    using Fields = NsonProtocol.Protocol.FieldNames;

    [TestClass]
    public class QueryTopologyExecutionTests
    {
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task TestUnionKeepsStoreSnapshotsAndVirtualScanRouting(
            bool sortedUnion)
        {
            var handler = new QueryResponseHandler();
            using var client = CreateClient(handler);
            client.SetQueryTopology(new TopologyInfo(4, new[] { 100 }));
            client.SetQueryTopology(new TopologyInfo(7, new[] { 11, 13 }, "storeOne"));
            client.SetQueryTopology(new TopologyInfo(9, new[] { 2, 12 }, "storeTwo"));
            handler.Respond = (payload, requestNumber) =>
            {
                var response = RowResponse(payload[Fields.ShardId].AsInt32);
                if (requestNumber == 1)
                {
                    // Later cache updates must not change this UNION's routes
                    // or topology headers, including its new virtual scanner.
                    client.SetQueryTopology(new TopologyInfo(40, new[] { 99 }));
                    client.SetQueryTopology(new TopologyInfo(70,
                        new[] { 101 }, "storeOne"));
                    client.SetQueryTopology(new TopologyInfo(90,
                        new[] { 102 }, "storeTwo"));
                    response[Fields.VirtualScans] = new ArrayValue
                    {
                        new MapValue
                        {
                            [Fields.VirtualScanSID] = 31,
                            [Fields.VirtualScanPID] = 1,
                            [Fields.VirtualScanNumTables] = 1,
                            [Fields.VirtualScanJoinPathMatched] = false
                        }
                    };
                }
                return response;
            };

            var rows = await ReadAll(client, CreateStatement(true, sortedUnion));
            CollectionAssert.AreEqual(sortedUnion ? new[] { 2, 11, 12, 13, 14 } :
                new[] { 11, 13, 14, 2, 12 }, rows);
            var payloads = handler.Requests.Select(request =>
                request[Fields.Payload].AsMapValue).ToArray();
            CollectionAssert.AreEquivalent(new[]
            {
                "storeOne:11", "storeOne:13", "storeOne:14", "storeTwo:2", "storeTwo:12"
            }, payloads.Select(payload => payload[Fields.StoreId].AsString + ":" +
                payload[Fields.ShardId].AsInt32).ToArray());
            var virtualRequest = payloads.Single(payload =>
                payload.ContainsKey(Fields.VirtualScan));
            Assert.AreEqual(14, virtualRequest[Fields.ShardId].AsInt32);
            Assert.AreEqual(31, virtualRequest[Fields.VirtualScan].AsMapValue[
                Fields.VirtualScanSID].AsInt32);
            foreach (var request in handler.Requests)
            {
                var header = request[Fields.Header].AsMapValue;
                Assert.AreEqual(4, header[Fields.TopoSeqNum].AsInt32);
                var stores = header[Fields.StoreTopologySequenceNumbers].AsArrayValue
                    .Select(value => value.AsMapValue).ToDictionary(
                        value => value[Fields.StoreId].AsString,
                        value => value[Fields.TopoSeqNum].AsInt32);
                Assert.AreEqual(2, stores.Count);
                Assert.AreEqual(7, stores["storeOne"]);
                Assert.AreEqual(9, stores["storeTwo"]);
            }
        }

        [TestMethod]
        public async Task TestRetryFailedUnionContinuationKeepsRemainingBranches()
        {
            var handler = new QueryResponseHandler
            {
                Respond = (payload, requestNumber) =>
                {
                    if (requestNumber == 2)
                    {
                        throw new HttpRequestException(
                            "Simulated transient fetch failure");
                    }
                    var branch = payload[Fields.PreparedQuery].AsByteArray[0];
                    var id = branch == 2 ? 3 : requestNumber == 1 ? 1 : 2;
                    var response = RowResponse(id);
                    if (requestNumber == 1)
                    {
                        response[Fields.ContinuationKey] =
                            new BinaryValue(new byte[] { 1 });
                        response[Fields.ReachedLimit] = true;
                    }
                    return response;
                }
            };
            using var client = CreateClient(handler);
            var statement = CreateStatement();
            // A no-bind UNION must also execute with driver tracing enabled.
            var first = await client.QueryAsync(statement,
                new QueryOptions { Limit = 1, TraceLevel = 1 });
            Assert.AreEqual(1, first.Rows.Count);
            Assert.AreEqual(1, first.Rows[0]["id"].AsInt32);
            Assert.IsNotNull(first.ContinuationKey);
            await Assert.ThrowsExceptionAsync<HttpRequestException>(() =>
                client.QueryAsync(statement, new QueryOptions
                {
                    ContinuationKey = first.ContinuationKey
                }));
            Assert.AreEqual(2, handler.Requests.Count,
                "The failure must reach the caller before its explicit retry.");

            // Retry using the last successful continuation. ReadAll requires
            // completion within four calls, even if the first returns no rows.
            CollectionAssert.AreEqual(new[] { 2, 3 }, await ReadAll(client,
                statement, first.ContinuationKey, null, 4));
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 2 }, handler.Requests
                .Select(request => (int)request[Fields.Payload].AsMapValue[
                    Fields.PreparedQuery].AsByteArray[0]).ToArray());
        }

        [TestMethod]
        public void TestQueryStoreTopologyPreservesDefaultAndRejectsEmptyShards()
        {
            using var client = CreateClient(new QueryResponseHandler());
            var legacy = new TopologyInfo(4, new[] { 1 });
            client.SetQueryTopology(legacy);
            var topology = new MapValue
            {
                [Fields.StoreId] = "storeOne",
                [Fields.ProxyTopoSeqNum] = 7,
                [Fields.ShardIds] = new ArrayValue { 12, 34 }
            };
            void ReadTopology()
            {
                using var response = Protocol.GetMemoryStreamWithVisibleBuffer(Encode(new MapValue
                {
                    [Fields.StoreTopologyInfo] = new ArrayValue { topology }
                }));
                new NsonProtocol.RequestSerializer().DeserializeQuery(response,
                    new QueryRequest<RecordValue>(client, CreateStatement(), null));
            }
            ReadTopology();
            var cached = client.StoreTopologies.Single();
            Assert.AreEqual("storeOne", cached.StoreName);
            Assert.AreEqual(7, cached.SequenceNumber);
            CollectionAssert.AreEqual(new[] { 12, 34 }, cached.ShardIds.ToArray());
            Assert.AreSame(legacy, client.QueryTopology);
            topology[Fields.ShardIds] = new ArrayValue();
            Assert.ThrowsException<BadProtocolException>(ReadTopology);
            Assert.AreSame(cached, client.StoreTopologies.Single(),
                "Invalid topology must not replace the cached shard list");
        }

        private static NoSQLClient CreateClient(HttpMessageHandler handler) =>
            new NoSQLClient(new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "http://localhost:8080",
                RetryHandler = NoSQLConfig.NoRetries
            }, handler);

        private static PreparedStatement CreateStatement(bool shardReceives = false,
            bool sortedUnion = false)
        {
            var sort = new[] { new SortSpec("id", false, false) };
            var statement = new PreparedStatement
            {
                RegisterCount = 3,
                OperationCode = QueryRequest.OperationCodeSelect,
                DriverQueryPlan = new UnionStep
                {
                    ResultPosition = 2,
                    BranchSteps = Enumerable.Range(0, 2).Select(index =>
                        (PlanStep)new ReceiveStep
                        {
                            ResultPosition = index,
                            DistributionKind = DistributionKind.AllShards,
                            SortSpecs = shardReceives ? sort : null
                        }).ToArray(),
                    SortSpecs = sortedUnion ? sort : null
                }
            };
            for (var i = 0; i < 2; i++)
            {
                statement.AddQueryBranch(new PreparedStatement.QueryBranch
                {
                    ProxyStatement = new[] { (byte)(i + 1) },
                    StoreName = shardReceives ?
                        (i == 0 ? "storeOne" : "storeTwo") : null
                });
            }
            return statement;
        }

        private static async Task<int[]> ReadAll(NoSQLClient client,
            PreparedStatement statement, QueryContinuationKey continuation = null,
            int? limit = 1, int maxCalls = 20)
        {
            var rows = new List<int>();
            for (var batch = 0; batch < maxCalls; batch++)
            {
                var result = await client.QueryAsync(statement, new QueryOptions
                {
                    Limit = limit, ContinuationKey = continuation
                });
                rows.AddRange(result.Rows.Select(row => row["id"].AsInt32));
                continuation = result.ContinuationKey;
                if (continuation == null)
                {
                    return rows.ToArray();
                }
            }
            Assert.Fail($"Query did not finish within {maxCalls} continuation calls");
            return null;
        }

        private static MapValue RowResponse(int id) => new MapValue
        {
            [Fields.QueryResults] = new ArrayValue { new MapValue { ["id"] = id } }
        };

        private static byte[] Encode(MapValue response)
        {
            using var stream = new MemoryStream();
            Protocol.WriteFieldValue(Protocol.GetNsonWriter(stream), response);
            return stream.ToArray();
        }

        private sealed class QueryResponseHandler : HttpMessageHandler
        {
            internal List<MapValue> Requests { get; } = new List<MapValue>();
            internal Func<MapValue, int, MapValue> Respond { get; set; }

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
                var response = Respond(root[Fields.Payload].AsMapValue, Requests.Count);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(Encode(response))
                };
            }
        }
    }
}
