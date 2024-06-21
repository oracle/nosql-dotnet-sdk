/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

// ReSharper disable StringLiteralTypo
// ReSharper disable CommentTypo
namespace Oracle.NoSQL.SDK.Tests
{
    using System;
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
            new IndexInfo("idx_start_date", new[] {"startDate"}),
            new IndexInfo("idx_info_address",
                new[] { "info.street", "info.bldgNo" },
                new [] { "String", "Integer" })
        };

        internal static TableInfo GetSimpleTableWithName(string name) =>
            SimpleTable.CloneWithTableName(name);

        internal static TableInfo GetSimpleTableWithLimits(
            TableLimits tableLimits) =>
            SimpleTable.CloneWithTableLimits(tableLimits);

        private const string AllTypesTableName = TableNamePrefix + "AllTypes";

        internal static readonly TableInfo AllTypesTable = new TableInfo(
            AllTypesTableName,
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
                new TableField("colIden", new IdentityFieldType(
                    DataType.Long))
            },
            new[] { "shardId", "pkString" },
            1
        )
        {
            TTL = TimeToLive.OfDays(3),
            DependentTableNames = new [] { $"{AllTypesTableName}.childTable" }
        };

        internal static TableInfo GetAllTypesTableWithName(string name) =>
            AllTypesTable.CloneWithTableName(name);

        internal static readonly TableInfo AllTypesChildTable = new TableInfo(
            AllTypesTable, AllTypesTable.Name + ".childTable", new[]
            {
                new TableField("childId", DataType.Long),
                new TableField("colInteger", DataType.Integer),
                new TableField("colNumber", DataType.Number),
                new TableField("colJSON", DataType.Json)
            }, new[] { "childId" })
        {
            // Pending release of the KV bug fix TTL can be changed to differ
            // from TTL of the parent table.
            TTL = TimeToLive.OfDays(3)
        };

        // Note that currently sorting on enumeration columns is only possible
        // if enumeration constants are defined in alphabetic order (because
        // server sorts on ordinals). See EnumColumnValues.

        // For secondary indexes, we use colIden to give unique sort order.

        // Currently only used by external tests.
        internal static readonly IndexInfo[] AllTypesTableIndexes = new[]
        {
            new IndexInfo("idenIdx", new [] { "colIden" }),
            new IndexInfo("tsIdenIdx", new [] { "colTimestamp", "colIden" }),
            new IndexInfo("recFldStrIdx", new [] { "colRecord.fldString" }),
            new IndexInfo("recFldStrIdenIdx",
                new [] { "colRecord.fldString", "colIden" }),
            new IndexInfo("array2JsonYIdx", new [] { "colArray2[].y" },
                new [] { "INTEGER" }),
            new IndexInfo("enumIdx", new [] { "colEnum" }),
            new IndexInfo("JsonLocIdx", new [] { "colJSON.location" },
                new [] { "point" }),
            new IndexInfo("jsonXzbIdx",
                new [] { "colJSON.x", "colJSON.z", "colJSON.b" },
                new [] { "STRING", "STRING", "BOOLEAN" }),
            new IndexInfo("numIdx", new [] { "colNumber" }),
            new IndexInfo("numIdenIdx", new [] { "colNumber", "colIden" }),
            new IndexInfo("num2IdenIdx", new [] { "colNumber2", "colIden" }),
            new IndexInfo("jsonUIdenIdx", new [] { "colJSON.u", "colIden" },
                new [] { "anyAtomic", null }),
            new IndexInfo("jsonXuzIdenIdx",
                new [] { "colJSON.x", "colJSON.u", "colJSON.z", "colIden"},
                new [] { "anyAtomic", "anyAtomic", "STRING", null }),
            new IndexInfo("enumJson2zkxaIdx",
                new [] { "colEnum", "colJSON2.z.k", "colJSON2.x.a" },
                new [] { null, "anyAtomic", "anyAtomic" })
        };

        internal static readonly IndexInfo[] AllTypesChildTableIndexes = new[]
        {
            new IndexInfo("numColIdx", new[] { "colNumber" })
        };

        static TestTables()
        {
            AllTypesTable.IdentityField = AllTypesTable.Fields[^1];
        }
    }

}
