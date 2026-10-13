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
    using System.Linq;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Oracle.NoSQL.SDK.Query;
    using static TestSchemas;
    using static TestTables;

    [TestClass]
    public class QueryFieldStepLiveTests : DataTestBase<QueryFieldStepLiveTests>
    {
        private static readonly TableInfo Table = new TableInfo(
            TableNamePrefix + "QueryFieldStep" + Guid.NewGuid().ToString("N")
                .Substring(0, 8), DefaultTableLimits,
            new[] { new TableField("id", DataType.Integer),
                new TableField("bucket", DataType.Integer),
                new TableField("doc", DataType.Json) },
            new[] { "id" }, 1);

        [ClassInitialize]
        public static async Task InitializeClass(TestContext context)
        {
            ClassInitialize(context);
            await CreateTableAsync(Table);
            var id = 0;
            for (var bucket = 0; bucket < 2; ++bucket)
            {
                foreach (var doc in CreateDocuments(bucket))
                {
                    await client.PutAsync(Table.Name, new MapValue
                    {
                        ["id"] = ++id, ["bucket"] = bucket, ["doc"] = doc
                    });
                }
            }
        }

        [ClassCleanup]
        public static async Task CleanupClass()
        {
            await DropTableAsync(Table);
            ClassCleanup();
        }

        private static FieldValue[] CreateDocuments(int bucket)
        {
            var first = bucket * 10 + 1;
            return new FieldValue[]
            {
                new MapValue { ["key"] = first },
                new ArrayValue
                {
                    new MapValue { ["key"] = first + 1 },
                    new ArrayValue
                    {
                        new MapValue(), new IntegerValue(99),
                        FieldValue.JsonNull,
                        new MapValue { ["key"] = first + 2 }
                    },
                    new ArrayValue()
                },
                new MapValue(), new StringValue("ignored"),
                FieldValue.JsonNull, FieldValue.Null,
                new MapValue { ["key"] = FieldValue.JsonNull }
            };
        }

        [DataTestMethod]
        [DataRow(true, 1)]
        [DataRow(false, 1)]
        [DataRow(true, 3)]
        [DataRow(false, 3)]
        public async Task TestAggregateFieldProjectionMatchesServerAsync(
            bool prepared, int batchSize)
        {
            await CheckV6SupportedAsync();
            var sql = "SELECT t.bucket AS bucket, " +
                "array_collect(t.doc).key AS projected, " +
                "seq_sum(array_collect(t.doc).key) AS total " +
                $"FROM {Table.Name} t GROUP BY t.bucket ORDER BY t.bucket";
            var plan = await client.PrepareAsync(sql);
            var formatted = PlanFormatter.Format(plan.DriverQueryPlan);
            StringAssert.Contains(formatted, "FIELD_STEP");
            StringAssert.Contains(formatted, "ARRAY_CONSTRUCTOR");
            StringAssert.Contains(formatted, "SEQ_SUM");

            var actual = await ReadRowsAsync(prepared ? plan : null,
                sql, batchSize);
            Assert.AreEqual(2, actual.Count);
            for (var bucket = 0; bucket < 2; ++bucket)
            {
                // A primary-key lookup evaluates the same field projection
                // entirely in KV. ARRAY_COLLECT omits SQL NULL inputs.
                var control = await client.PrepareAsync(
                    "DECLARE $docs JSON; SELECT $docs.key AS projected, " +
                    "seq_sum($docs.key) AS total " +
                    $"FROM {Table.Name} t WHERE t.id = 1");
                Assert.IsNull(control.DriverQueryPlan,
                    "The expected values must be computed by the server.");
                var documents = new ArrayValue();
                foreach (var doc in CreateDocuments(bucket))
                {
                    if (doc != FieldValue.Null)
                    {
                        documents.Add(doc);
                    }
                }
                control.Variables["$docs"] = documents;
                var expected = (await ReadRowsAsync(control, null, 1)).Single();
                Assert.AreEqual(bucket, actual[bucket]["bucket"].AsInt32);
                Assert.AreEqual(bucket * 30 + 6,
                    expected["total"].ToInt32());
                Assert.AreEqual(4, expected["projected"].AsArrayValue.Count);
                AssertProjectionEquivalent(expected["projected"],
                    actual[bucket]["projected"]);
                Assert.AreEqual(expected["total"].ToInt64(),
                    actual[bucket]["total"].ToInt64());
            }
        }

        [TestMethod]
        public async Task TestCaseBoundJsonFieldProjectionMatchesServerAsync()
        {
            await CheckV6SupportedAsync();
            var driver = await client.PrepareAsync(
                "DECLARE $summary JSON; SELECT t.bucket AS bucket, " +
                "(CASE WHEN count(*) > 0 THEN $summary END).key AS projected " +
                $"FROM {Table.Name} t GROUP BY t.bucket ORDER BY t.bucket");
            var formatted = PlanFormatter.Format(driver.DriverQueryPlan);
            StringAssert.Contains(formatted, "CASE");
            StringAssert.Contains(formatted, "FIELD_STEP");
            var control = await client.PrepareAsync(
                "DECLARE $summary JSON; SELECT " +
                "(CASE WHEN t.id > 0 THEN $summary END).key AS projected " +
                $"FROM {Table.Name} t WHERE t.id = 1");
            Assert.IsNull(control.DriverQueryPlan,
                "The expected values must be computed by the server.");

            var summaries = new FieldValue[]
            {
                new MapValue { ["key"] = 42 },
                new MapValue { ["key"] = FieldValue.JsonNull },
                new MapValue { ["key"] = new ArrayValue { 7, 11 } },
                new MapValue(), new IntegerValue(17),
                new StringValue("ignored"), FieldValue.JsonNull,
                FieldValue.Null,
                new ArrayValue(),
                new ArrayValue
                {
                    new MapValue(), new IntegerValue(99), FieldValue.JsonNull,
                    new MapValue { ["key"] = 7 },
                    new ArrayValue { new MapValue { ["key"] = 11 } }
                }
            };
            foreach (var summary in summaries)
            {
                driver.Variables["$summary"] = summary;
                control.Variables["$summary"] = summary;
                var expected = (await ReadRowsAsync(control, null, 1))
                    .Single()["projected"];
                var actual = await ReadRowsAsync(driver, null, 1);
                Assert.AreEqual(2, actual.Count);
                for (var bucket = 0; bucket < actual.Count; ++bucket)
                {
                    Assert.AreEqual(bucket, actual[bucket]["bucket"].AsInt32);
                    AssertProjectionEquivalent(expected,
                        actual[bucket]["projected"]);
                }
            }
        }

        private static void AssertProjectionEquivalent(FieldValue expected,
            FieldValue actual)
        {
            Assert.AreEqual(expected.DbType, actual.DbType);
            if (expected is ArrayValue expectedArray)
            {
                // ARRAY_COLLECT ordering is unspecified across partitions.
                CollectionAssert.AreEquivalent(expectedArray.Select(
                    value => value.DbType + ":" + value.ToJsonString())
                    .ToArray(), actual.AsArrayValue.Select(
                    value => value.DbType + ":" + value.ToJsonString())
                    .ToArray());
            }
            else
            {
                Assert.AreEqual(expected.ToJsonString(), actual.ToJsonString());
            }
        }

        private static async Task CheckV6SupportedAsync()
        {
            await client.PrepareAsync($"SELECT * FROM {Table.Name}");
            if (client.ProtocolHandler.QueryVersion < QueryRequestBase.QueryV6)
            {
                Assert.Inconclusive(
                    "Driver field projection tests require query protocol V6");
            }
        }

        private static async Task<List<RecordValue>> ReadRowsAsync(
            PreparedStatement statement, string sql, int batchSize)
        {
            var rows = new List<RecordValue>();
            QueryContinuationKey continuation = null;
            var calls = 0;
            do
            {
                Assert.IsTrue(++calls <= 200, "Query did not finish");
                var options = new QueryOptions
                {
                    Limit = batchSize,
                    ContinuationKey = continuation,
                    Consistency = Consistency.Absolute
                };
                var result = statement == null
                    ? await client.QueryAsync(sql, options)
                    : await client.QueryAsync(statement, options);
                rows.AddRange(result.Rows);
                continuation = result.ContinuationKey;
            } while (continuation != null);
            return rows;
        }
    }
}
