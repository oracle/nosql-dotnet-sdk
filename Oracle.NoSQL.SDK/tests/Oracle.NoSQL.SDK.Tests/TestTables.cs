/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using static TestSchemas;

    internal static class TestTables
    {
        internal const string TableNamePrefix = "YevTest";

        internal static readonly string[] EnumColumnValues =
        {
            "enumVal1", "enumVal2", "enumVal3", "enumVal4", "enumVal5"
        };

        internal static readonly TableLimits DefaultTableLimits =
            new TableLimits(1000, 500, 100);

        internal static readonly TableLimits DefaultOnDemandTableLimits =
            new TableLimits(100);

        internal static readonly TableInfo SimpleTable = new TableInfo(
            TableNamePrefix + "T1",
            DefaultTableLimits,
            new[]
            {
                new TableField("id", DataType.Integer),
                new TableField("lastName", DataType.String),
                new TableField("firstName", DataType.String),
                new TableField("info", DataType.Json),
                new TableField("startDate", new TimestampFieldType(3))
            },
            new[] {"id"}
        );

        internal static readonly IndexInfo[] SimpleTableIndexes =
        {
            new IndexInfo("idx_name", new[] {"lastName", "firstName"}),
            new IndexInfo("idx_start_date", new[] {"startDate"})
        };

        internal static TableInfo GetSimpleTableWithName(string name) =>
            new TableInfo(name, SimpleTable.TableLimits, SimpleTable.Fields,
                SimpleTable.PrimaryKey);

        internal static readonly TableInfo AllTypesTable = new TableInfo(
            TableNamePrefix + "AllTypes",
            DefaultTableLimits,
            new[]
            {
                new TableField("shardId", DataType.Integer),
                new TableField("pkString", DataType.String),
                new TableField("colBoolean", DataType.Boolean),
                new TableField("colInteger", DataType.Integer),
                new TableField("colLong", DataType.Long),
                new TableField("colFloat", DataType.Float),
                new TableField("colDouble", DataType.Double),
                new TableField("colNumber", DataType.Number),
                new TableField("colNumber2", DataType.Number),
                new TableField("colBinary", DataType.Binary),
                new TableField("colFixedBinary", new FieldType(
                    DataType.Binary, "BINARY(64)")),
                new TableField("colEnum", new EnumFieldType("enumVal1",
                    "enumVal2", "enumVal3", "enumVal4", "enumVal5")),
                new TableField("colTimestamp", new TimestampFieldType(9)),
                new TableField("colRecord", new RecordFieldType(
                    new TableField("fldString", DataType.String),
                    new TableField("fldNumber", DataType.Number),
                    new TableField("fldArray", new ArrayFieldType(
                        DataType.Integer)))),
                new TableField("colArray", new ArrayFieldType(
                    new TimestampFieldType(6))),
                new TableField("colArray2", new ArrayFieldType(
                    DataType.Json)),
                new TableField("colMap", new MapFieldType(DataType.Long)),
                new TableField("colMap2",
                    new MapFieldType(DataType.Binary)),
                new TableField("colJSON", DataType.Json),
                new TableField("colJSON2", DataType.Json),
                new TableField("colIdentity", new IdentityFieldType(
                    DataType.Long))
            },
            new[] { "shardId", "pkString" },
            1
        )
        {
            TTL = TimeToLive.OfDays(3)
        };

        static TestTables()
        {
            AllTypesTable.IdentityField = AllTypesTable.Fields[^1];
        }
    }

}
