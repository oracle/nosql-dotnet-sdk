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
                new TableField("doc", DataType.Json),
                new TableField("missing", DataType.Integer) },
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
                    new ArrayValue { new MapValue(), new IntegerValue(99),
                        FieldValue.JsonNull,
                        new MapValue { ["key"] = first + 2 } }
                },
                new MapValue(),
                new MapValue { ["key"] = FieldValue.JsonNull },
                FieldValue.Null
            };
        }

        [TestMethod]
        public async Task TestAggregateFieldProjectionMatchesServerAsync()
        {
            await client.PrepareAsync($"SELECT * FROM {Table.Name}");
            if (client.ProtocolHandler.QueryVersion < QueryRequestBase.QueryV6)
            {
                Assert.Inconclusive("Driver field projection requires V6");
            }
            var plan = await client.PrepareAsync(
                "SELECT t.bucket AS bucket, " +
                "array_collect(t.doc).key AS projected, " +
                "seq_sum(array_collect(t.doc).key) AS total " +
                $"FROM {Table.Name} t GROUP BY t.bucket ORDER BY t.bucket");
            var formatted = PlanFormatter.Format(plan.DriverQueryPlan);
            StringAssert.Contains(formatted, "FIELD_STEP");
            StringAssert.Contains(formatted, "ARRAY_CONSTRUCTOR");
            StringAssert.Contains(formatted, "SEQ_SUM");
            var control = await client.PrepareAsync(
                "DECLARE $docs JSON; SELECT $docs.key AS projected, " +
                "seq_sum($docs.key) AS total " +
                $"FROM {Table.Name} t WHERE t.id = 1");
            Assert.IsNull(control.DriverQueryPlan,
                "The expected values must be computed by the server.");

            var actual = await ReadRowsAsync(plan);
            Assert.AreEqual(2, actual.Count);
            for (var bucket = 0; bucket < 2; ++bucket)
            {
                // ARRAY_COLLECT omits SQL NULL and has unspecified ordering.
                var documents = new ArrayValue();
                foreach (var doc in CreateDocuments(bucket))
                {
                    if (doc != FieldValue.Null) documents.Add(doc);
                }
                control.Variables["$docs"] = documents;
                var expected = (await ReadRowsAsync(control)).Single();
                Assert.AreEqual(bucket, actual[bucket]["bucket"].AsInt32);
                Assert.AreEqual(bucket * 30 + 6, expected["total"].ToInt32());
                Assert.AreEqual(4, expected["projected"].AsArrayValue.Count);
                CollectionAssert.AreEquivalent(
                    ProjectionItems(expected["projected"]),
                    ProjectionItems(actual[bucket]["projected"]));
                Assert.AreEqual(expected["total"].ToInt64(),
                    actual[bucket]["total"].ToInt64());
            }
        }

        [TestMethod]
        public async Task TestUnionCountJsonNullSqlNullAndMissingFieldAsync()
        {
            await CheckUnionSupportedAsync(Table.Name);
            // Bucket 0 excluding the nested array contains a number, a
            // missing field, JSON null, and SQL NULL: COUNT includes two.
            var source = $"SELECT t.doc.key AS v FROM {Table.Name} t " +
                "WHERE t.bucket = 0 AND t.id != 2";
            var union = await client.PrepareAsync(
                $"SELECT count($u.v) AS total FROM ({source} UNION ALL " +
                $"{source}) $u");
            Assert.AreEqual(4L, (await ReadRowsAsync(union)).Single()
                ["total"].ToInt64());
        }

        [TestMethod]
        public async Task TestDriverSizeSqlNullAfterUnionAggregationAsync()
        {
            await CheckUnionSupportedAsync(Table.Name);
            // The unpopulated typed column makes MIN return SQL NULL.
            // A NULL literal in this SQL dialect represents JSON null.
            var source = $"SELECT id, missing FROM {Table.Name}";
            var statement = await client.PrepareAsync(
                "SELECT size(CASE WHEN count(*) > 0 THEN min($u.missing) " +
                "ELSE array_collect($u.id) END) AS result FROM (" +
                source + " UNION ALL " + source + ") $u");
            StringAssert.Contains(PlanFormatter.Format(
                statement.DriverQueryPlan), "FN_SIZE");
            Assert.AreSame(FieldValue.Null,
                (await ReadRowsAsync(statement)).Single()["result"]);
        }

        private static string[] ProjectionItems(FieldValue value) =>
            value.AsArrayValue.Select(item =>
                item.DbType + ":" + item.ToJsonString()).ToArray();

        private static async Task<List<RecordValue>> ReadRowsAsync(
            PreparedStatement statement)
        {
            var rows = new List<RecordValue>();
            QueryContinuationKey continuation = null;
            var calls = 0;
            do
            {
                Assert.IsTrue(++calls <= 200, "Query did not finish");
                var result = await client.QueryAsync(statement,
                    new QueryOptions
                    {
                        Limit = 1,
                        ContinuationKey = continuation,
                        Consistency = Consistency.Absolute
                    });
                rows.AddRange(result.Rows);
                continuation = result.ContinuationKey;
            } while (continuation != null);
            return rows;
        }
    }
}
