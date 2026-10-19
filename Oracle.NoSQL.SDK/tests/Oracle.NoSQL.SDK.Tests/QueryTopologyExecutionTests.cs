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
        public async Task TestUnionKeepsTopologyAndVirtualScanRouting(
            bool sortedUnion)
        {
            var handler = new QueryResponseHandler();
            using var client = CreateClient(handler);
            client.SetQueryTopology(new TopologyInfo(4, new[] { 11, 13 }));
            handler.Respond = (payload, requestNumber) =>
            {
                var response = RowResponse(payload[Fields.ShardId].AsInt32);
                if (requestNumber == 1)
                {
                    // Later cache updates must not change this UNION's routes
                    // or topology headers, including its new virtual scanner.
                    client.SetQueryTopology(new TopologyInfo(40, new[] { 99 }));
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
            CollectionAssert.AreEqual(sortedUnion ? new[] { 11, 11, 13, 13, 14 } :
                new[] { 11, 13, 14, 11, 13 }, rows);
            var payloads = handler.Requests.Select(request =>
                request[Fields.Payload].AsMapValue).ToArray();
            CollectionAssert.AreEquivalent(new[]
            {
                "1:11", "1:13", "1:14", "2:11", "2:13"
            }, payloads.Select(payload => payload[Fields.PreparedQuery].AsByteArray[0] +
                ":" + payload[Fields.ShardId].AsInt32).ToArray());
            var virtualRequest = payloads.Single(payload =>
                payload.ContainsKey(Fields.VirtualScan));
            Assert.AreEqual(14, virtualRequest[Fields.ShardId].AsInt32);
            Assert.AreEqual(31, virtualRequest[Fields.VirtualScan].AsMapValue[
                Fields.VirtualScanSID].AsInt32);
            foreach (var request in handler.Requests)
            {
                var header = request[Fields.Header].AsMapValue;
                Assert.AreEqual(4, header[Fields.TopoSeqNum].AsInt32);
                // Same-store UNION must not advertise per-store support.
                Assert.IsFalse(header.ContainsKey("sts"));
                Assert.IsFalse(request[Fields.Payload].AsMapValue.ContainsKey("sid"));
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

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task TestRetryUnionAfterBufferedRows(bool sorted)
        {
            var firstBranchFetches = 0;
            var handler = new QueryResponseHandler
            {
                Respond = (payload, _) =>
                {
                    var branch = payload[Fields.PreparedQuery].AsByteArray[0];
                    var shard = payload[Fields.ShardId].AsInt32;
                    if (branch == 1 && shard == 11 && ++firstBranchFetches == 2)
                        throw new HttpRequestException("Failure after buffered row 2");
                    var ids = branch == 2 ? new[] { shard == 11 ? 3 : 4 } :
                        shard == 13 ? new[] { 9 } :
                        firstBranchFetches == 1 ? new[] { 1, 2 } : new[] { 5 };
                    if (payload.TryGetValue(Fields.NumberLimit, out var limit))
                        Assert.IsTrue(limit.AsInt32 == 0 || ids.Length <= limit.AsInt32);
                    var response = new MapValue
                    {
                        [Fields.QueryResults] = new ArrayValue(ids.Select(id =>
                            (FieldValue)new MapValue { ["id"] = id }))
                    };
                    if (branch == 1 && shard == 11 && firstBranchFetches == 1)
                    {
                        response[Fields.ContinuationKey] = new BinaryValue(new byte[] { 1 });
                        response[Fields.ReachedLimit] = true;
                    }
                    return response;
                }
            };
            using var client = CreateClient(handler);
            client.SetQueryTopology(new TopologyInfo(4, new[] { 11, 13 }));
            var statement = CreateStatement(shardReceives: true, sortedUnion: sorted);
            QueryResult<RecordValue> first;
            // The first fetch buffers two rows while other shards are pending.
            // Later calls use Limit=1 to leave a row buffered before the failure.
            var options = new QueryOptions { Limit = 2 };
            var calls = 0;
            do
            {
                Assert.IsTrue(++calls <= 5);
                first = await client.QueryAsync(statement, options);
                options.ContinuationKey = first.ContinuationKey;
                options.Limit = 1;
            } while (first.Rows.Count == 0 && first.ContinuationKey != null);
            CollectionAssert.AreEqual(new[] { 1 }, first.Rows.Select(row => row["id"].AsInt32).ToArray());
            Assert.IsNotNull(first.ContinuationKey);
            var resume = new QueryOptions { ContinuationKey = first.ContinuationKey };
            await Assert.ThrowsExceptionAsync<HttpRequestException>(() => client.QueryAsync(statement, resume));
            Assert.AreEqual(2, firstBranchFetches);
            var requestsAtFailure = handler.Requests.Count;
            var retry = await client.QueryAsync(statement, resume);
            CollectionAssert.AreEqual(new[] { 2 }, retry.Rows.Select(row => row["id"].AsInt32).ToArray());
            Assert.IsNotNull(retry.ContinuationKey);
            Assert.AreEqual(requestsAtFailure, handler.Requests.Count,
                "Returning buffered rows must not fetch or replay them.");
            var rest = await ReadAll(client, statement, retry.ContinuationKey, null, 8);
            CollectionAssert.AreEqual(sorted ? new[] { 3, 4, 5, 9 } : new[] { 5, 9, 3, 4 }, rest);
        }

        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task TestQueryVersionFallbackThroughRequests(bool rejectAll)
        {
            var handler = new QueryResponseHandler
            {
                Respond = (payload, number) =>
                {
                    Assert.IsTrue(number <= 4, "Protocol fallback did not terminate");
                    return rejectAll || payload[Fields.QueryVersion].AsInt32 > 4
                        ? new MapValue { ["e"] = 27, ["x"] = "Unsupported query version" }
                        : new MapValue { [Fields.PreparedQuery] = new BinaryValue(new byte[] { 1 }),
                            [Fields.TableName] = "T" };
                }
            };
            using var client = CreateClient(handler);
            if (rejectAll)
            {
                var error = await Assert.ThrowsExceptionAsync<UnsupportedQueryVersionException>(
                    () => client.PrepareAsync("SELECT * FROM T"));
                StringAssert.Contains(error.Message, "version 3");
            }
            else
            {
                var statement = await client.PrepareAsync("SELECT * FROM T");
                CollectionAssert.AreEqual(new byte[] { 1 }, statement.ProxyStatement);
            }
            CollectionAssert.AreEqual(rejectAll ? new[] { 6, 5, 4, 3 } : new[] { 6, 5, 4 },
                handler.Requests.Select(request => request[Fields.Payload].AsMapValue[
                    Fields.QueryVersion].AsInt32).ToArray());
        }

        [DataTestMethod]
        [DataRow((short)4)]
        [DataRow((short)5)]
        [DataRow((short)6)]
        public void TestPreparedBranchesUseDefaultTopology(short queryVersion)
        {
            using var client = CreateClient(new QueryResponseHandler());
            var branchCount = queryVersion >= QueryRequestBase.QueryV6 ? 2 : 1;
            var branches = new ArrayValue();
            for (var i = 0; i < branchCount; i++)
            {
                branches.Add(new MapValue
                {
                    // Legacy fields may arrive before the prepared bytes.
                    [Fields.Namespace] = "namespace" + i,
                    [Fields.TableName] = "table" + i,
                    [Fields.PreparedQuery] = new BinaryValue(new[] { (byte)i })
                });
            }
            var response = branchCount == 1 ? branches[0].AsMapValue :
                new MapValue { [Fields.QueryBranches] = branches };
            response[Fields.TopologyInfo] = new MapValue
            {
                [Fields.ProxyTopoSeqNum] = 4,
                [Fields.ShardIds] = new ArrayValue { 11, 13 }
            };
            using var stream = Protocol.GetMemoryStreamWithVisibleBuffer(Encode(response));
            var statement = new NsonProtocol.RequestSerializer().DeserializePrepare(
                stream, new PrepareRequest(client, "SELECT * FROM table0", null)
                {
                    QueryVersion = queryVersion
                });
            Assert.AreEqual(branchCount, statement.QueryBranches.Count);
            for (var i = 0; i < branchCount; i++)
            {
                Assert.AreEqual((byte)i, statement.GetProxyStatement(i)[0]);
                Assert.AreEqual("namespace" + i, statement.GetNamespace(i));
                Assert.AreEqual("table" + i, statement.GetTableName(i));
            }
            Assert.AreEqual(4, client.QueryTopology.SequenceNumber);
            CollectionAssert.AreEqual(new[] { 11, 13 }, client.QueryTopology.ShardIds.ToArray());
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
                    ProxyStatement = new[] { (byte)(i + 1) }
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
