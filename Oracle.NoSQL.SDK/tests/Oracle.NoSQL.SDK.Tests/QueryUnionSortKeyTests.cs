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
    public class QueryUnionSortKeyTests
    {
        [DataTestMethod]
        [DataRow(false, false, false, 1)]
        [DataRow(false, false, true, 1)]
        [DataRow(false, true, false, 1)]
        [DataRow(false, true, true, 1)]
        [DataRow(true, false, false, 1)]
        [DataRow(true, false, true, 1)]
        [DataRow(true, true, false, 1)]
        [DataRow(true, true, true, 1)]
        public async Task TestUnionPreservesMissingSortKeys(bool sortedReceive,
            bool descending, bool nullsFirst, int batchSize)
        {
            var specialIds = descending ? new[] { 3, 2, 1 } :
                new[] { 1, 2, 3 };
            var expectedIds = nullsFirst ? specialIds.Concat(new[] { 4 }) :
                new[] { 4 }.Concat(specialIds);
            var specs = new[]
            {
                new SortSpec("key", descending, nullsFirst),
                new SortSpec("id", false, false)
            };
            var handler = new SortRowsHandler(sortedReceive, specs);
            using var client = CreateClient(handler);
            var rows = await ReadAll(client,
                CreateStatement(sortedReceive, specs), batchSize);

            CollectionAssert.AreEqual(expectedIds.SelectMany(id =>
                    new[] { id, id }).ToArray(),
                rows.Select(row => row["id"].AsInt32).ToArray());
            foreach (var row in rows)
            {
                // Preserve internal EMPTY only until the final public row.
                Assert.AreNotSame(FieldValue.Empty, row["key"]);
                if (row["id"].AsInt32 == 1 || row["id"].AsInt32 == 3)
                {
                    Assert.AreSame(FieldValue.Null, row["key"]);
                }
                else if (row["id"].AsInt32 == 2)
                {
                    Assert.AreSame(FieldValue.JsonNull, row["key"]);
                }
            }
            Assert.AreEqual(2, handler.RequestCount);
        }

        [DataTestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public async Task TestGroupingAboveUnionRetainsMissingSemantics(
            bool sortedReceive, bool distinct)
        {
            var specs = new[] { new SortSpec("key", false, false) };
            var handler = new SortRowsHandler(sortedReceive, specs);
            using var client = CreateClient(handler);
            var statement = CreateStatement(sortedReceive, specs);
            statement.DriverQueryPlan = new GroupStep
            {
                ResultPosition = 5,
                InputStep = statement.DriverQueryPlan,
                GroupingColumnCount = 1,
                ColumnNames = distinct ? new[] { "key" } :
                    new[] { "key", "count" },
                AggregateFuncCodes = distinct ? Array.Empty<SQLFuncCode>() :
                    new[] { SQLFuncCode.CountStar },
                IsDistinct = distinct
            };

            var rows = await ReadAll(client, statement, 1);

            // DISTINCT coalesces EMPTY and SQL NULL; GROUP BY excludes EMPTY.
            Assert.AreEqual(3, rows.Count);
            CollectionAssert.AreEquivalent(new[]
                { DbType.Null, DbType.JsonNull, DbType.Integer },
                rows.Select(row => row["key"].DbType).ToArray());
            if (!distinct)
            {
                foreach (var row in rows)
                {
                    Assert.AreEqual(2L, row["count"].ToInt64(),
                        "Missing keys must not be added to the SQL NULL group.");
                }
            }
        }

        private static NoSQLClient CreateClient(HttpMessageHandler handler)
        {
            var client = new NoSQLClient(new NoSQLConfig
            {
                ServiceType = ServiceType.CloudSim,
                Endpoint = "http://localhost:8080"
            }, handler);
            client.SetQueryTopology(new TopologyInfo(1, new[] { 1 }));
            return client;
        }

        private static PreparedStatement CreateStatement(bool sortedReceive,
            SortSpec[] specs)
        {
            var branches = new PlanStep[2];
            for (var i = 0; i < branches.Length; i++)
            {
                var receive = new ReceiveStep
                {
                    ResultPosition = 2 * i,
                    DistributionKind = DistributionKind.AllShards,
                    SortSpecs = sortedReceive ? specs : null
                };
                branches[i] = sortedReceive ? (PlanStep)receive : new SortStep
                {
                    ResultPosition = 2 * i + 1,
                    InputStep = receive,
                    SortSpecs = specs
                };
            }
            var statement = new PreparedStatement
            {
                RegisterCount = 6,
                OperationCode = QueryRequest.OperationCodeSelect,
                DriverQueryPlan = new UnionStep
                {
                    ResultPosition = 4,
                    BranchSteps = branches,
                    SortSpecs = specs
                }
            };
            foreach (var branch in branches)
            {
                statement.AddQueryBranch(new PreparedStatement.QueryBranch
                {
                    ProxyStatement = new byte[] { 1 }
                });
            }
            return statement;
        }

        private static async Task<List<RecordValue>> ReadAll(NoSQLClient client,
            PreparedStatement statement, int batchSize)
        {
            var rows = new List<RecordValue>();
            QueryContinuationKey continuation = null;
            for (var batch = 0; batch < 30; batch++)
            {
                var result = await client.QueryAsync(statement, new QueryOptions
                {
                    Limit = batchSize,
                    ContinuationKey = continuation
                });
                rows.AddRange(result.Rows);
                continuation = result.ContinuationKey;
                if (continuation == null)
                {
                    return rows;
                }
            }
            Assert.Fail("UNION did not finish within 30 continuation calls.");
            return null;
        }

        private sealed class SortRowsHandler : HttpMessageHandler
        {
            private readonly bool sortedReceive;
            private readonly SortSpec[] specs;

            internal int RequestCount { get; private set; }

            internal SortRowsHandler(bool sortedReceive, SortSpec[] specs)
            {
                this.sortedReceive = sortedReceive;
                this.specs = specs;
            }

            protected override Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                RequestCount++;
                var values = new FieldValue[]
                {
                    FieldValue.Empty, FieldValue.JsonNull, FieldValue.Null,
                    new IntegerValue(0)
                };
                var rows = values.Select((value, i) => new RecordValue
                {
                    ["id"] = i + 1, ["key"] = value, ["count"] = 1
                }).ToArray();
                if (sortedReceive)
                {
                    Array.Sort(rows, (left, right) =>
                        Query.Utils.CompareRows(left, right, specs));
                }
                using var output = new MemoryStream();
                var writer = Protocol.GetNsonWriter(output);
                writer.StartMap();
                writer.StartArray(Protocol.FieldNames.QueryResults);
                foreach (var row in rows)
                {
                    Protocol.WriteFieldValue(writer, row);
                }
                writer.EndArray();
                writer.EndMap();
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new ByteArrayContent(output.ToArray())
                });
            }
        }
    }
}
