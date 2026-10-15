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
        public async Task TestRetryFailedUnionContinuationKeepsRemainingBranches()
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
                RegisterCount = 3,
                OperationCode = QueryRequest.OperationCodeSelect,
                DriverQueryPlan = new UnionStep
                {
                    ResultPosition = 2,
                    BranchSteps = new PlanStep[]
                    {
                        new ReceiveStep { ResultPosition = 0 },
                        new ReceiveStep { ResultPosition = 1 }
                    }
                }
            };
            statement.AddQueryBranch(new PreparedStatement.QueryBranch
            {
                ProxyStatement = new byte[] { 1 }
            });

            statement.AddQueryBranch(new PreparedStatement.QueryBranch
            {
                ProxyStatement = new byte[] { 2 }
            });

            var first = await client.QueryAsync(statement,
                new QueryOptions
                {
                    Limit = 1,
                    // A no-bind UNION must also execute with driver tracing.
                    TraceLevel = 1
                });
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

            CollectionAssert.AreEqual(new[] { 2, 3 },
                remainingRows,
                "A failed fetch must not discard the current or later UNION branch.");
            Assert.IsNull(continuation);
            CollectionAssert.AreEqual(new[] { 1, 1, 1, 2 },
                handler.RequestBranches);
            Assert.AreEqual(4, handler.RequestCount);
        }

        private sealed class FailOnceResponseHandler : HttpMessageHandler
        {
            internal int RequestCount { get; private set; }
            internal List<int> RequestBranches { get; } = new List<int>();

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var bytes = await request.Content
                    .ReadAsByteArrayAsync(cancellationToken);
                using var input = Protocol.GetMemoryStreamWithVisibleBuffer(bytes);
                input.Position = sizeof(short);
                var reader = Protocol.GetNsonReader(input);
                reader.Next();
                var payload = Protocol.ReadFieldValue(reader).AsMapValue[
                    Protocol.FieldNames.Payload].AsMapValue;
                var branch = payload[Protocol.FieldNames.PreparedQuery]
                    .AsByteArray[0];
                RequestBranches.Add(branch);
                RequestCount++;
                if (RequestCount == 2)
                {
                    throw new HttpRequestException("Simulated transient fetch failure");
                }

                var id = branch == 2 ? 3 : RequestCount == 1 ? 1 : 2;
                using var stream = new MemoryStream();
                var writer = Protocol.GetNsonWriter(stream);
                writer.StartMap();
                writer.StartArray(Protocol.FieldNames.QueryResults);
                writer.StartMap();
                writer.WriteInt32("id", id);
                writer.EndMap();
                writer.EndArray();
                if (RequestCount == 1)
                {
                    writer.WriteByteArray(Protocol.FieldNames.ContinuationKey,
                        new byte[] { 1 });
                    writer.WriteBoolean(Protocol.FieldNames.ReachedLimit, true);
                }
                writer.EndMap();
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(stream.ToArray())
                };
            }
        }
    }
}
