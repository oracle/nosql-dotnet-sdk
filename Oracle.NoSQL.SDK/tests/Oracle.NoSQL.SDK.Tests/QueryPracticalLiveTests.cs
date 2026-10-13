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
    using static Utils;

    [TestClass]
    public class QueryPracticalLiveTests : DataTestBase<QueryPracticalLiveTests>
    {
        private static readonly TableInfo Table = new TableInfo(
            TableNamePrefix + "QueryPractical" + Guid.NewGuid().ToString("N")
                .Substring(0, 8), DefaultTableLimits,
            new[] { new TableField("id", DataType.Integer),
                new TableField("doc", DataType.Json),
                new TableField("ts", new TimestampFieldType(7)) },
            new[] { "id" }, 1);

        [ClassInitialize]
        public static async Task InitializeClass(TestContext context)
        {
            ClassInitialize(context);
            await CreateTableAsync(Table);
            for (var id = 1; id <= 16; ++id)
            {
                FieldValue doc = (id % 4) switch
                {
                    0 => new MapValue(),
                    1 => new MapValue { ["key"] = FieldValue.JsonNull },
                    2 => new MapValue { ["key"] = id % 3 },
                    _ => FieldValue.Null
                };
                await client.PutAsync(Table.Name, new MapValue
                {
                    ["id"] = id,
                    ["doc"] = doc,
                    ["ts"] = new TimestampValue(new DateTime(2026, 1, 1,
                        0, 0, 0, DateTimeKind.Utc))
                });
            }
        }

        [ClassCleanup]
        public static async Task CleanupClass()
        {
            await DropTableAsync(Table);
            ClassCleanup();
        }

        [DataTestMethod]
        [DataRow("ASC NULLS FIRST", true, 1)]
        [DataRow("ASC NULLS FIRST", true, 5)]
        [DataRow("ASC NULLS FIRST", false, 1)]
        [DataRow("ASC NULLS FIRST", false, 5)]
        [DataRow("ASC NULLS LAST", true, 1)]
        [DataRow("ASC NULLS LAST", true, 5)]
        [DataRow("ASC NULLS LAST", false, 1)]
        [DataRow("ASC NULLS LAST", false, 5)]
        [DataRow("DESC NULLS FIRST", true, 1)]
        [DataRow("DESC NULLS FIRST", true, 5)]
        [DataRow("DESC NULLS FIRST", false, 1)]
        [DataRow("DESC NULLS FIRST", false, 5)]
        [DataRow("DESC NULLS LAST", true, 1)]
        [DataRow("DESC NULLS LAST", true, 5)]
        [DataRow("DESC NULLS LAST", false, 1)]
        [DataRow("DESC NULLS LAST", false, 5)]
        public async Task TestUnionMissingJsonSortKeysAsync(string order,
            bool prepared, int batchSize)
        {
            await CheckUnionSupportedAsync(Table.Name);
            var control = await ReadRowsAsync(
                $"SELECT t.id,t.doc.key AS sortKey FROM {Table.Name} t " +
                $"ORDER BY t.doc.key {order},t.id", true, batchSize);
            var expected = control.SelectMany(row =>
                Enumerable.Repeat(row["id"].AsInt32, 3)).ToArray();
            var branch = $"SELECT id,doc FROM {Table.Name}";
            var sql = "SELECT $u.id,$u.doc.key AS sortKey FROM (" + branch +
                " UNION ALL " + branch + " UNION ALL " + branch +
                $") $u ORDER BY $u.doc.key {order},$u.id";
            var plan = await client.PrepareAsync(sql);
            StringAssert.Contains(PlanFormatter.Format(plan.DriverQueryPlan),
                "UNION");
            StringAssert.Contains(PlanFormatter.Format(plan.DriverQueryPlan),
                "SORT");

            var all = await ReadRowsAsync(sql, prepared, batchSize);
            CollectionAssert.AreEqual(expected,
                all.Select(row => row["id"].AsInt32).ToArray());
            // EMPTY is internal; public rows retain the existing SQL NULL
            // representation of missing JSON properties.
            foreach (var row in all)
            {
                var kind = row["id"].AsInt32 % 4;
                if (kind == 0 || kind == 3)
                {
                    Assert.AreSame(FieldValue.Null, row["sortKey"]);
                }
                else if (kind == 1)
                {
                    Assert.AreSame(FieldValue.JsonNull, row["sortKey"]);
                }
            }
            foreach (var offset in new[] { 0, 1, 23 })
            {
                var page = await ReadRowsAsync(sql +
                    $" LIMIT 5 OFFSET {offset}", prepared, batchSize);
                CollectionAssert.AreEqual(expected.Skip(offset).Take(5)
                    .ToArray(), page.Select(row => row["id"].AsInt32)
                    .ToArray());
            }
        }

        [DataTestMethod]
        [DataRow("2026-01-01T00:00:00Z", true, false)]
        [DataRow("2026-01-01T00:00:00.000Z", true, false)]
        [DataRow("2026-01-01T00:00:00.1Z", false, true)]
        [DataRow("2025-12-31T23:59:59.9Z", false, false)]
        [DataRow("2026-01-01T05:30:00+05:30", true, false)]
        [DataRow("2025-12-31T18:30:00-05:30", true, false)]
        [DataRow("2026-01-01T00:00:00", true, false)]
        [DataRow("2026-01-01", true, false)]
        [DataRow("2026-1-1T0:0:0", true, false)]
        [DataRow("2026-01-01T00", true, false)]
        [DataRow("2026-01-01T00:00", true, false)]
        public async Task TestTimestampJsonCutoffMatchesServerAsync(
            string cutoff, bool equal, bool less)
        {
            await client.PrepareAsync($"SELECT * FROM {Table.Name}");
            if (client.ProtocolHandler.QueryVersion < QueryRequestBase.QueryV6)
            {
                Assert.Inconclusive(
                    "Timestamp expression tests require query protocol V6");
            }
            var projection = "{0} = $cutoff AS eq, {0} < $cutoff AS lt, " +
                "$cutoff = {0} AS reverseEq, $cutoff > {0} AS reverseGt";
            var driver = await client.PrepareAsync("DECLARE $cutoff JSON; " +
                "SELECT " + string.Format(projection, "min(t.ts)") +
                $" FROM {Table.Name} t");
            StringAssert.Contains(PlanFormatter.Format(driver.DriverQueryPlan),
                "LESS_THAN");
            var server = await client.PrepareAsync("DECLARE $cutoff JSON; " +
                "SELECT " + string.Format(projection, "t.ts") +
                $" FROM {Table.Name} t WHERE t.id = 1");
            driver.Variables["$cutoff"] = cutoff;
            server.Variables["$cutoff"] = cutoff;
            var actual = (await ReadRowsAsync(driver, null, 1)).Single();
            var expected = (await ReadRowsAsync(server, null, 1)).Single();
            Assert.AreEqual(equal, expected["eq"].AsBoolean);
            Assert.AreEqual(less, expected["lt"].AsBoolean);
            foreach (var field in new[] { "eq", "lt", "reverseEq", "reverseGt" })
            {
                Assert.AreEqual(expected[field].AsBoolean,
                    actual[field].AsBoolean, field + " cutoff=" + cutoff);
            }
        }

        private static async Task<List<RecordValue>> ReadRowsAsync(string sql,
            bool prepared, int batchSize) => await ReadRowsAsync(
            prepared ? await client.PrepareAsync(sql) : null, sql, batchSize);

        private static async Task<List<RecordValue>> ReadRowsAsync(
            PreparedStatement statement, string sql, int batchSize)
        {
            var rows = new List<RecordValue>();
            QueryContinuationKey continuation = null;
            var calls = 0;
            do
            {
                Assert.IsTrue(++calls <= 500, "Query did not finish");
                var options = new QueryOptions
                {
                    Limit = batchSize,
                    ContinuationKey = continuation,
                    Consistency = Consistency.Absolute,
                    TraceLevel = 1
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
