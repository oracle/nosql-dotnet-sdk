/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 * https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Query;
    using Protocol = NsonProtocol.Protocol;

    [TestClass]
    public class QueryContinuationFailureTests
    {
        [TestMethod]
        public async Task TestRetryFailedContinuationKeepsRemainingRows()
        {
            var handler = new FailOnceResponseHandler();
            using var client = new NoSQLClient(new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "http://localhost:8080",
                // The application explicitly retries after the HTTP failure
                // escapes QueryAsync, using its last successful continuation.
                RetryHandler = NoSQLConfig.NoRetries
            }, handler);
            var statement = new PreparedStatement
            {
                RegisterCount = 1,
                OperationCode = QueryRequest.OperationCodeSelect,
                DriverQueryPlan = new ReceiveStep { ResultPosition = 0 }
            };
            statement.AddQueryBranch(new PreparedStatement.QueryBranch
            {
                ProxyStatement = new byte[] { 1 }
            });

            var first = await client.QueryAsync(statement,
                new QueryOptions { Limit = 1 });
            Assert.AreEqual(1, first.Rows.Count);
            Assert.AreEqual(1, first.Rows[0]["id"].AsInt32);
            Assert.IsNotNull(first.ContinuationKey);

            await Assert.ThrowsExceptionAsync<HttpRequestException>(() =>
                client.QueryAsync(statement, new QueryOptions
                {
                    ContinuationKey = first.ContinuationKey
                }));
            Assert.AreEqual(2, handler.RequestCount,
                "The failure must reach the caller before its explicit retry.");

            var continuation = first.ContinuationKey;
            var remainingRows = new List<int>();
            for (var batch = 0; batch < 4 && continuation != null; batch++)
            {
                var result = await client.QueryAsync(statement,
                    new QueryOptions { ContinuationKey = continuation });
                foreach (var row in result.Rows)
                {
                    remainingRows.Add(row["id"].AsInt32);
                }
                continuation = result.ContinuationKey;
            }

            CollectionAssert.AreEqual(new[] { 2 }, remainingRows,
                "A failed fetch must not turn the retry into end-of-query.");
            Assert.IsNull(continuation);
            Assert.AreEqual(3, handler.RequestCount);
        }

        [TestMethod]
        public async Task TestRetryFailedSortedContinuationPreservesBufferedRows()
        {
            var handler = new FailOnceResponseHandler(true);
            using var client = new NoSQLClient(new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "http://localhost:8080",
                RetryHandler = NoSQLConfig.NoRetries
            }, handler);
            client.SetQueryTopology(new TopologyInfo(1, new[] { 1, 2 }));
            var statement = new PreparedStatement
            {
                RegisterCount = 1,
                OperationCode = QueryRequest.OperationCodeSelect,
                DriverQueryPlan = new ReceiveStep
                {
                    ResultPosition = 0,
                    DistributionKind = DistributionKind.AllShards,
                    SortSpecs = new[] { new SortSpec("id", false, false) }
                }
            };
            statement.AddQueryBranch(new PreparedStatement.QueryBranch
            {
                ProxyStatement = new byte[] { 1 }
            });

            // The first shard supplies rows 1 and 2. Sorting must fetch a
            // result from the other shard before any row can be returned.
            var initial = await client.QueryAsync(statement,
                new QueryOptions { Limit = 2 });
            Assert.AreEqual(0, initial.Rows.Count);
            Assert.IsNotNull(initial.ContinuationKey);
            var first = await client.QueryAsync(statement, new QueryOptions
            {
                Limit = 1, ContinuationKey = initial.ContinuationKey
            });
            Assert.AreEqual(1, first.Rows.Count);
            Assert.AreEqual(1, first.Rows[0]["id"].AsInt32);
            Assert.IsNotNull(first.ContinuationKey);

            // Row 2 is already buffered; the next remote fetch then fails.
            await Assert.ThrowsExceptionAsync<HttpRequestException>(() =>
                client.QueryAsync(statement, new QueryOptions
                {
                    ContinuationKey = first.ContinuationKey
                }));
            Assert.AreEqual(3, handler.RequestCount);

            var retry = await client.QueryAsync(statement, new QueryOptions
            {
                ContinuationKey = first.ContinuationKey
            });
            Assert.AreEqual(1, retry.Rows.Count);
            Assert.AreEqual(2, retry.Rows[0]["id"].AsInt32);
            Assert.IsNotNull(retry.ContinuationKey,
                "Returning cached rows must not discard remaining shard results.");
            Assert.AreEqual(3, handler.RequestCount,
                "The cached batch is returned before another server fetch.");

            var last = await client.QueryAsync(statement, new QueryOptions
            {
                ContinuationKey = retry.ContinuationKey
            });
            Assert.AreEqual(2, last.Rows.Count);
            Assert.AreEqual(3, last.Rows[0]["id"].AsInt32);
            Assert.AreEqual(4, last.Rows[1]["id"].AsInt32);
            Assert.IsNull(last.ContinuationKey);
            Assert.AreEqual(4, handler.RequestCount);
        }

        private sealed class FailOnceResponseHandler : HttpMessageHandler
        {
            private readonly bool sorted;

            internal FailOnceResponseHandler(bool sorted = false)
            {
                this.sorted = sorted;
            }

            internal int RequestCount { get; private set; }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestCount++;
                if (RequestCount == (sorted ? 3 : 2))
                {
                    throw new HttpRequestException("Simulated transient fetch failure");
                }

                var ids = sorted
                    ? RequestCount == 1 ? new[] { 1, 2 } :
                        RequestCount == 2 ? new[] { 3 } : new[] { 4 }
                    : new[] { RequestCount == 1 ? 1 : 2 };
                using var stream = new MemoryStream();
                var writer = Protocol.GetNsonWriter(stream);
                writer.StartMap();
                writer.StartArray(Protocol.FieldNames.QueryResults);
                foreach (var id in ids)
                {
                    writer.StartMap();
                    writer.WriteInt32("id", id);
                    writer.EndMap();
                }
                writer.EndArray();
                if (RequestCount == 1)
                {
                    writer.WriteByteArray(Protocol.FieldNames.ContinuationKey,
                        new byte[] { 1 });
                    writer.WriteBoolean(Protocol.FieldNames.ReachedLimit, true);
                }
                writer.EndMap();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(stream.ToArray())
                });
            }
        }
    }
}
