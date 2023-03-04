/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Windows.Markup;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using NsonProtocol;
    using static Utils;

    public static class TestSchemas
    {
        internal enum DataType
        {
            Unknown,
            Boolean,
            Integer,
            Long,
            Float,
            Double,
            Number,
            String,
            Enum,
            Timestamp,
            Binary,
            Array,
            Map,
            Record,
            Json
        }

        internal class FieldType
        {
            internal FieldType(DataType dataType, string spec = null)
            {
                DataType = dataType;
                Spec = spec;
            }

            internal DataType DataType { get; }

            internal string Spec { get; }

            internal string DataTypeName => DataType.ToString().ToUpper();

            internal virtual bool IsIdentity => false;

            public static implicit operator FieldType(DataType dataType) =>
                new FieldType(dataType);

            public override string ToString() => Spec ?? DataTypeName;
        }

        internal abstract class CollectionFieldType : FieldType
        {
            internal CollectionFieldType(DataType dataType,
                FieldType elementType) : base(dataType)
            {
                ElementType = elementType;
            }

            internal FieldType ElementType { get; }

            public override string ToString() =>
                $"{DataTypeName}({ElementType})";
        }

        internal class ArrayFieldType : CollectionFieldType
        {
            internal ArrayFieldType(FieldType elementType) :
                base(DataType.Array, elementType)
            {
            }
        }

        internal class MapFieldType : CollectionFieldType
        {
            internal MapFieldType(FieldType elementType) :
                base(DataType.Map, elementType)
            {
            }
        }

        internal class RecordFieldType : FieldType
        {
            internal RecordFieldType(params TableField[] fields) :
                base(DataType.Record)
            {
                Fields = fields;
            }

            // For both parent and child tables.
            internal RecordFieldType(IReadOnlyList<TableField> parentFields,
                IReadOnlyList<TableField> fields) : base(DataType.Record)
            {
                Fields = parentFields?.Concat(fields).ToArray() ?? fields;
            }

            internal RecordFieldType(IReadOnlyList<TableField> fields) :
                this(null, fields)
            {
            }

            internal IReadOnlyList<TableField> Fields { get; }

            public override string ToString() =>
                $"{DataTypeName}({FieldsToString(Fields)})";
        }

        internal class EnumFieldType : FieldType
        {
            internal EnumFieldType(params string[] values) :
                base(DataType.Enum)
            {
                Values = values;
            }

            internal string[] Values { get; }

            public override string ToString() =>
                $"ENUM({string.Join(FieldSeparator, Values)})";
        }

        internal class TimestampFieldType : FieldType
        {
            internal TimestampFieldType(int precision) :
                base(DataType.Timestamp, $"TIMESTAMP({precision})")
            {
                Precision = precision;
            }

            internal int Precision { get; }
        }

        internal class IdentityFieldType : FieldType
        {
            internal IdentityFieldType(DataType dataType, bool generatedAlways = true) :
                base(dataType)
            {
                GeneratedAlways = generatedAlways;
            }

            internal bool GeneratedAlways { get; }

            internal override bool IsIdentity => true;

            public override string ToString() =>
                $"{DataTypeName} GENERATED " +
                $"{(GeneratedAlways ? "ALWAYS" : "BY DEFAULT")} AS IDENTITY";
        }

        internal class TableField
        {
            internal TableField(string name, FieldType fieldType)
            {
                Name = name;
                FieldType = fieldType;
            }

            internal string Name { get; }

            internal FieldType FieldType { get; }

            public override string ToString() => $"{Name} {FieldType}";
        }

        public class TableInfo
        {
            private TableInfo(TableInfo parent, string name,
                TableLimits tableLimits, TableField[] fields,
                string[] primaryKey, int shardKeySize)
            {
                Parent = parent;
                Name = name;
                TableLimits = tableLimits;
                Fields = fields;
                PrimaryKey = primaryKey;
                ShardKeySize = shardKeySize;
                RecordType = new RecordFieldType(
                    parent?.GetPrimaryKeyFields(), fields);
            }

            internal TableInfo(string name, TableLimits tableLimits,
                TableField[] fields, string[] primaryKey,
                int shardKeySize = 0) : this(null, name, tableLimits, fields,
                primaryKey, shardKeySize)
            {
            }

            // For child tables
            internal TableInfo(TableInfo parent, string name,
                TableField[] fields, string[] primaryKey) : this(parent, name,
                null, fields, primaryKey, 0)
            {
            }

            // For child tables
            internal TableInfo Parent { get; }

            internal string Name { get; }

            internal TableLimits TableLimits { get; }

            internal TableField[] Fields { get; }

            internal string[] PrimaryKey { get; }

            internal int ShardKeySize { get; }

            internal TimeToLive? TTL { get; set; }

            internal TableField IdentityField { get; set; }

            internal RecordFieldType RecordType { get; }

            internal TableField GetField(string name)
            {
                var result = Array.Find(Fields, elem => elem.Name == name);
                Assert.IsNotNull(result); // test self-check
                return result;
            }

            internal TableField[] GetPrimaryKeyFields()
            {
                var result = new TableField[PrimaryKey.Length];
                for (var i = 0; i < result.Length; i++)
                {
                    result[i] = GetField(PrimaryKey[i]);
                }

                return result;
            }
        }

        public class IndexInfo
        {
            internal IndexInfo(string name, string[] fieldNames,
                string[] fieldTypes = null)
            {
                Name = name;
                FieldNames = fieldNames;
                FieldTypes = fieldTypes;
            }

            internal string Name { get; }

            internal string[] FieldNames { get; }

            // only for JSON typed indexes
            internal string[] FieldTypes { get; }
        }

        internal const string FieldSeparator = ", ";

        private static string FieldsToString(
            IReadOnlyList<TableField> fields) =>
            string.Join(FieldSeparator,
                from field in fields select field.ToString());

        private static string PrimaryKeyToString(string[] primaryKey,
            int shardKeySize)
        {
            if (shardKeySize == 0)
            {
                return string.Join(FieldSeparator, primaryKey);
            }

            var pk1 = string.Join(FieldSeparator, primaryKey, 0,
                shardKeySize);
            var pk2 = string.Join(FieldSeparator, primaryKey, shardKeySize,
                primaryKey.Length - shardKeySize);
            return $"SHARD({pk1}), {pk2}";
        }

        internal static string MakeCreateTable(TableInfo table,
            bool ifNotExists = false)
        {
            var ifNotExistsStr = ifNotExists ? "IF NOT EXISTS " : string.Empty;
            var ttlStr = table.TTL.HasValue ?
                $" USING TTL {table.TTL}" : string.Empty;
            var fieldsStr = FieldsToString(table.Fields);
            var primaryKeyStr =
                PrimaryKeyToString(table.PrimaryKey, table.ShardKeySize);
            return $"CREATE TABLE {ifNotExistsStr}{table.Name}" +
                   $"({fieldsStr}, PRIMARY KEY ({primaryKeyStr})){ttlStr}";
        }

        internal static string MakeDropTable(TableInfo table,
            bool ifExists = false)
        {
            var ifExistsStr = ifExists ? "IF EXISTS " : string.Empty;
            return $"DROP TABLE {ifExistsStr}{table.Name}";
        }

        internal static string MakeCreateIndex(TableInfo table,
            IndexInfo index, bool ifNotExists = false)
        {
            var ifNotExistsStr = ifNotExists ? "IF NOT EXISTS " : string.Empty;
            // support JSON typed indexes
            var fieldInfos = index.FieldTypes == null
                ? index.FieldNames
                : (from i in Enumerable.Range(0, index.FieldNames.Length)
                    select index.FieldTypes[i] == null
                        ? index.FieldNames[i]
                        : $"{index.FieldNames[i]} AS {index.FieldTypes[i]}")
                .ToArray();

            var fields = string.Join(FieldSeparator, fieldInfos);
            return $"CREATE INDEX {ifNotExistsStr}{index.Name} ON " +
                   $"{table.Name}({fields})";
        }

        internal static string MakeDropIndex(TableInfo table,
            IndexInfo index) => $"DROP INDEX {index.Name} ON {table.Name}";

        internal static string MakeAddField(TableInfo table,
            TableField field) =>
            $"ALTER TABLE {table.Name} (ADD {field.Name} {field.FieldType})";

        internal static string MakeDropField(TableInfo table,
            TableField field) =>
            $"ALTER TABLE {table.Name} (DROP {field.Name})";

        internal static string MakeAlterTTL(TableInfo table,
            TimeToLive ttl) =>
            $"ALTER TABLE {table.Name} USING TTL {ttl}";

        internal static MapValue DoMakePrimaryKey(TableInfo table,
            MapValue row, MapValue pk)
        {
            if (table.Parent != null)
            {
                DoMakePrimaryKey(table.Parent, row, pk);
            }

            foreach (var pkField in table.PrimaryKey)
            {
                pk[pkField] = row[pkField];
            }

            return pk;
        }

        internal static MapValue MakePrimaryKey(TableInfo table,
            MapValue row) => DoMakePrimaryKey(table, row, new MapValue());

        // This function will keep the row id with the primary key.
        internal static MapValue MakeDataPK(TableInfo table, DataRow row) =>
            DoMakePrimaryKey(table, row, new DataPK(row.Id));
    }
}
