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
    using System.Net;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Oracle.NoSQL.SDK.Query;
    using NsonProtocol = Oracle.NoSQL.SDK.NsonProtocol.Protocol;

    [TestClass]
    public class QueryTraceBatchNumberTests
    {
        [TestMethod]
        public async Task TestBatchCounterContinuesWithFreshOptions()
        {
            var handler = new QueryResponseHandler();
            using var client = new NoSQLClient(new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "http://localhost:8080"
            }, handler);
            var statement = new PreparedStatement
            {
                RegisterCount = 1,
                VariableNames = Array.Empty<string>(),
                DriverQueryPlan = new ReceiveStep { ResultPosition = 0 }
            };
            statement.AddQueryBranch(new PreparedStatement.QueryBranch
            {
                ProxyStatement = new byte[] { 1 }
            });

            var first = await client.QueryAsync<RecordValue>(statement,
                new QueryOptions { Limit = 1, TraceLevel = 1 });
            Assert.IsNotNull(first.ContinuationKey);

            // Deliberately use a new options instance, as callers commonly do
            // when advancing a query with its continuation key.
            var second = await client.QueryAsync<RecordValue>(statement,
                new QueryOptions
                {
                    Limit = 1,
                    TraceLevel = 1,
                    ContinuationKey = first.ContinuationKey
                });

            Assert.AreEqual(1, first.Rows.Count);
            Assert.AreEqual(1, second.Rows.Count);
            CollectionAssert.AreEqual(new[] { 1, 2 }, handler.BatchCounters);
        }

        private sealed class QueryResponseHandler : HttpMessageHandler
        {
            internal List<int> BatchCounters { get; } = new List<int>();

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request,
                CancellationToken cancellationToken)
            {
                var requestBytes = await request.Content
                    .ReadAsByteArrayAsync(cancellationToken);
                using var requestStream = NsonProtocol
                    .GetMemoryStreamWithVisibleBuffer(requestBytes);
                requestStream.Position = sizeof(short);
                var reader = NsonProtocol.GetNsonReader(requestStream);
                reader.Next();
                var requestMap = NsonProtocol.ReadFieldValue(reader)
                    .AsMapValue;
                var payload = requestMap[NsonProtocol.FieldNames.Payload]
                    .AsMapValue;
                BatchCounters.Add(payload[
                    NsonProtocol.FieldNames.BatchCounter].AsInt32);

                var responseBytes = CreateResponse(BatchCounters.Count);
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(responseBytes)
                };
            }

            private static byte[] CreateResponse(int batch)
            {
                using var stream = new MemoryStream();
                var writer = NsonProtocol.GetNsonWriter(stream);
                writer.StartMap();
                writer.StartArray(NsonProtocol.FieldNames.QueryResults);
                writer.StartMap();
                writer.WriteInt32("id", batch);
                writer.EndMap();
                writer.EndArray();
                if (batch == 1)
                {
                    writer.WriteByteArray(
                        NsonProtocol.FieldNames.ContinuationKey,
                        new byte[] { 1 });
                }
                writer.WriteBoolean(NsonProtocol.FieldNames.ReachedLimit,
                    true);
                writer.EndMap();
                return stream.ToArray();
            }
        }
    }
}
