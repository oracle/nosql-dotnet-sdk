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
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Oracle.NoSQL.SDK.Query;
    using static TestSchemas;
    using static TestTables;
    using static Utils;

    [TestClass]
    public class QueryNullLiveTests : DataTestBase<QueryNullLiveTests>
    {
        private static readonly TableInfo Table = new TableInfo(
            TableNamePrefix + "QueryNull" + Guid.NewGuid().ToString("N")
                .Substring(0, 8), DefaultTableLimits,
            new[] { new TableField("id", DataType.Integer),
                new TableField("doc", DataType.Json),
                new TableField("missing", DataType.Integer),
                new TableField("smallNumber", DataType.Number),
                new TableField("smallDouble", DataType.Double),
                new TableField("largeDouble", DataType.Double) },
            new[] { "id" }, 1);

        [ClassInitialize]
        public static async Task InitializeClass(TestContext context)
        {
            ClassInitialize(context);
            await CreateTableAsync(Table);
            await client.PutAsync(Table.Name, new MapValue
                {
                    ["id"] = 1,
                    ["doc"] = new MapValue { ["v"] = FieldValue.JsonNull },
                    ["smallNumber"] = new NumberValue(1e-20m),
                    ["smallDouble"] = new DoubleValue(1e-20),
                    ["largeDouble"] = new DoubleValue(1e20)
                });
            await client.PutAsync(Table.Name, new MapValue
                { ["id"] = 2, ["doc"] = new MapValue { ["v"] = 1 } });
            await client.PutAsync(Table.Name, new MapValue
                { ["id"] = 3, ["doc"] = FieldValue.Null });
            await client.PutAsync(Table.Name, new MapValue
                { ["id"] = 4, ["doc"] = new MapValue() });
        }

        [ClassCleanup]
        public static async Task CleanupClass()
        {
            await DropTableAsync(Table);
            ClassCleanup();
        }

        [TestMethod]
        public async Task TestCountJsonNullSqlNullAndMissingFieldAsync()
        {
            await CheckUnionSupportedAsync(Table.Name);
            var source = $"SELECT t.doc.v AS v FROM {Table.Name} t";
            var ordinary = await client.PrepareAsync(
                $"SELECT count(t.doc.v) AS total FROM {Table.Name} t");
            var union = await client.PrepareAsync(
                $"SELECT count($u.v) AS total FROM ({source} UNION ALL " +
                $"{source}) $u");
            Assert.AreEqual(2L, (await ReadSingleRowAsync(ordinary))
                ["total"].ToInt64());
            Assert.AreEqual(4L, (await ReadSingleRowAsync(union))
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
            // Keep this expression in the driver: proxy-only evaluation
            // would not exercise FuncSizeIterator's SQL NULL handling.
            StringAssert.Contains(PlanFormatter.Format(
                statement.DriverQueryPlan), "FN_SIZE");
            Assert.AreSame(FieldValue.Null,
                (await ReadSingleRowAsync(statement))["result"]);
        }

        [TestMethod]
        public async Task TestDriverArithmeticPromotionAfterUnionAsync()
        {
            await CheckUnionSupportedAsync(Table.Name);
            var source = $"SELECT id FROM {Table.Name}";
            var statement = await client.PrepareAsync(
                "SELECT sum($u.id) / count(*) * 1.0 AS mean FROM (" +
                source + " UNION ALL " + source + ") $u");
            StringAssert.Contains(PlanFormatter.Format(
                statement.DriverQueryPlan), "MULT_DIV");
            Assert.AreEqual(2.5,
                (await ReadSingleRowAsync(statement))["mean"].ToDouble());
        }

        [DataTestMethod]
        [DataRow("*", "/", "smallDouble")]
        [DataRow("/", "*", "largeDouble")]
        public async Task TestDriverArithmeticRecoversAfterIntermediateUnderflowAsync(
            string firstOperation, string secondOperation, string doubleColumn)
        {
            var statement = await client.PrepareAsync(
                $"SELECT min(t.smallNumber) {firstOperation} " +
                $"min(t.{doubleColumn}) {secondOperation} " +
                $"min(t.{doubleColumn}) AS result FROM {Table.Name} t");
            StringAssert.Contains(PlanFormatter.Format(
                statement.DriverQueryPlan), "MULT_DIV");
            Assert.AreEqual(1e-20,
                (await ReadSingleRowAsync(statement))["result"].ToDouble(),
                1e-32);
        }

        private static async Task<RecordValue> ReadSingleRowAsync(
            PreparedStatement statement)
        {
            var rows = new List<RecordValue>();
            QueryContinuationKey continuation = null;
            var calls = 0;
            do
            {
                Assert.IsTrue(++calls <= 50, "Query did not finish");
                var result = await client.QueryAsync(statement,
                    new QueryOptions
                    {
                        Limit = 1,
                        ContinuationKey = continuation,
                        Consistency = Consistency.Absolute,
                        TraceLevel = 1
                    });
                rows.AddRange(result.Rows);
                continuation = result.ContinuationKey;
            } while (continuation != null);
            Assert.AreEqual(1, rows.Count);
            return rows[0];
        }
    }
}
