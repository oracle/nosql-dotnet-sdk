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
                new TableField("ts", new TimestampFieldType(7)) },
            new[] { "id" }, 1);

        [ClassInitialize]
        public static async Task InitializeClass(TestContext context)
        {
            ClassInitialize(context);
            await CreateTableAsync(Table);
            for (var id = 1; id <= 16; ++id)
            {
                await client.PutAsync(Table.Name, new MapValue
                {
                    ["id"] = id,
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
        [DataRow("2026-01-01T00:00:00Z", true, false)]
        [DataRow("2026-01-01T00:00:00.1Z", false, true)]
        [DataRow("2025-12-31T23:59:59.9Z", false, false)]
        [DataRow("2026-01-01T05:30:00+05:30", true, false)]
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
