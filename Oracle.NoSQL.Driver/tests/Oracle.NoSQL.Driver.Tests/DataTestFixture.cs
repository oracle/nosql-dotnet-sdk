/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver.Tests
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;
    using System.Linq;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Utils;
    using static TestSchemas;
    using static TestTables;

    public class DataRow : RecordValue
    {
        private TimeToLive? ttl;
        private TimeToLive? originalTTL;

        internal int Id { get; }

        // TTL value may be changed by testing Put operations with supplied
        // TTL, so we save the original value to be used to re-insert the row.
        internal TimeToLive? TTL
        {
            get => ttl;
            set
            {
                if (ttl.HasValue && !originalTTL.HasValue)
                {
                    originalTTL = ttl;
                }

                ttl = value;
            }
        }

        internal RowVersion Version { get; set; }

        internal DateTime PutTime { get; set; }

        internal DataRow(int id, TimeToLive? ttl = null)
        {
            Id = id;
            TTL = ttl;
        }

        internal void Reset()
        {
            ttl = originalTTL;
            Version = null;
        }
    }

    public class DataPK : MapValue
    {
        internal int Id { get; }

        internal DataPK(int id)
        {
            Id = id;
        }
    }

    internal abstract class DataRowFactory
    {
        internal const int DefaultRowsPerShard = 20;

        internal DataRowFactory(int rowsPerShard = DefaultRowsPerShard)
        {
            RowsPerShard = rowsPerShard;
        }

        internal int RowsPerShard { get; }

        internal abstract int MaxRowKB { get; }

        internal abstract DataRow MakeRow(int id);

        internal abstract DataRow MakeModifiedRow(DataRow row);

        internal int GetRowIdFromShard(int shardId, int rowIndex = 0) =>
            shardId * RowsPerShard + rowIndex;

        internal DataRow MakeRowFromShard(int shardId, int rowIndex = 0) =>
            MakeRow(GetRowIdFromShard(shardId, rowIndex));
    }

    internal class AllTypesRowFactory : DataRowFactory
    {
        private static readonly FieldValue[] SpecialDoubles =
        {
            double.PositiveInfinity,
            double.NegativeInfinity,
            double.NaN,
            0,
            FieldValue.Null
        };

        private static readonly string[] EnumColValues;
        private readonly DateTime nowTime;
        private int modifySeq;

        private static readonly string[] CopyColumns =
        {
            "colFloat", "colDouble", "colNumber", "colBinary",
            "colFixedBinary", "colEnum", "colTimestamp", "colRecord",
            "colArray", "colArray2", "colMap", "colMap2", "colJSON"
        };

        static AllTypesRowFactory()
        {
            TableField enumField = Array.Find(AllTypesTable.Fields,
                field => field.Name == "colEnum");
            Assert.IsNotNull(enumField);
            Assert.IsTrue(enumField.FieldType is EnumFieldType);
            EnumColValues = ((EnumFieldType)enumField.FieldType).Values;
        }

        internal AllTypesRowFactory(int rowsPerShard = DefaultRowsPerShard) :
            base(rowsPerShard)
        {
            nowTime = DateTime.UtcNow;
            modifySeq = 0;
        }

        private FieldValue MakeJsonValue(int i)
        {
            if (i % 7 == 0)
            {
                return FieldValue.Null;
            }

            var value = new MapValue
            {
                ["x"] = "a",
                ["y"] = 10 + i,
                ["u"] = i % 4 == 0 ? 100.01 + i :
                    i % 4 == 1 ? "hello" + i :
                    i % 4 == 2 ? i % 10 < 5 : (FieldValue)nowTime.ToString(
                        CultureInfo.InvariantCulture),
                ["z"] = (nowTime + TimeSpan.FromMilliseconds(
                        (i % 6) * 123456)).ToString("O"),
                ["location"] = FieldValue.JsonNull
            };

            if (i % 3 != 0)
            {
                value["b"] = i % 3 == 2;
            }

            return value;
        }

        private FieldValue MakeJsonValue2(int i)
        {
            return MakeJsonValue(i);
        }

        private static byte[] FillBytes(byte value, int length)
        {
            var result = new byte[length];
            Array.Fill(result, value);
            return result;
        }

        private decimal MakeNumberValue(int i)
        {
            return i * (decimal)10e-28;
        }

        private decimal MakeNumberValue2(int i)
        {
            return decimal.MaxValue - i;
        }

        private decimal MakeNumberValue3(int i)
        {
            return (decimal)i / 31;
        }

        internal override int MaxRowKB => 4;

        internal DateTime ReferenceTime => nowTime;

        internal override DataRow MakeRow(int id) =>
            new DataRow(id)
            {
                TTL = id % 2 == 0
                    ? id % 4 == 0 ? TimeToLive.OfHours(id + 1) :
                    TimeToLive.OfDays((id + 1) % 6)
                    : (TimeToLive?)null,
                ["shardId"] = id / RowsPerShard,
                ["pkString"] = RepeatString("id", id % 20) + id,
                ["colBoolean"] = id % 2 == 0 ? id % 4 == 0 : FieldValue.Null,
                ["colInteger"] =
                    id % 8 != 0 ? 0x70000000 + id : FieldValue.Null,
                ["colLong"] = (id + 1) % 8 != 0
                    ? long.MinValue + id * 99
                    : FieldValue.Null,
                ["colFloat"] = id % 2 != 0
                    ? (1 + 0.0001 * id) * 1e38
                    : FieldValue.Null,
                ["colDouble"] = id % RowsPerShard > RowsPerShard / 2
                    ? SpecialDoubles[id % SpecialDoubles.Length]
                    : (id % 4 == 0 ? -1 : 1) * 1e-308 * (id + 1) * 0.00012345,
                ["colNumber"] = id % 7 != 0 ? MakeNumberValue(id) :
                    FieldValue.Null,
                ["colNumber2"] = id % 8 == 0 ? MakeNumberValue2(id) :
                    FieldValue.Null,
                ["colBinary"] = id % 8 != 0 ?
                    FillBytes((byte)id, (id - 1) * 7 % 256) :
                    FieldValue.Null,
                ["colFixedBinary"] = Encoding.UTF8.GetBytes(string.Concat(
                    Enumerable.Repeat(id.ToString(), 64))).Take(64).ToArray(),
                ["colEnum"] = id + 2 % 4 != 0
                    ? EnumColValues[id % EnumColValues.Length]
                    : FieldValue.Null,
                ["colTimestamp"] = (id + 2) % 8 != 0
                    ? nowTime + TimeSpan.FromMilliseconds(
                        (id % 2 != 0 ? -1 : 1) * id * 1000000000 + id)
                    : FieldValue.Null,
                ["colRecord"] = id % 8 != 0
                    ? new RecordValue
                    {
                        ["fldString"] = id % 4 != 0
                            ? string.Concat(Enumerable.Repeat("a", id % 5))
                            : FieldValue.Null,
                        ["fldNumber"] = (id + 1) % 4 != 0 ?
                            MakeNumberValue3(id) : FieldValue.Null,
                        ["fldArray"] = (id + 2) % 4 != 0
                            ? new ArrayValue(Enumerable.Repeat(
                                new IntegerValue(id), id % 10))
                            : FieldValue.Null
                    }
                    : FieldValue.Null,
                ["colArray"] = (id + 2) % 8 != 0
                    ? new ArrayValue(
                        Enumerable.Repeat(new TimestampValue(nowTime +
                            TimeSpan.FromSeconds(id)), id % 20))
                    : FieldValue.Null,
                ["colArray2"] = (id + 3) % 16 != 0
                    ? new ArrayValue(
                        from i in Enumerable.Range(0, id % 5 * 3)
                        select MakeJsonValue(id + i))
                    : FieldValue.Null,
                ["colMap"] = (id + 4) % 8 != 0
                    ? new MapValue(
                        from i in Enumerable.Range(0, id % 7)
                        select
                            new KeyValuePair<string, FieldValue>(
                                $"key{i}", long.MaxValue - i))
                    : FieldValue.Null,
                ["colMap2"] = (id + 5) % 8 != 0
                    ? new MapValue(
                        from i in Enumerable.Range(0, id % 10)
                        select
                            new KeyValuePair<string, FieldValue>(
                                RepeatString("abc", i),
                                FillBytes((byte)i, i)))
                    : FieldValue.Null,
                ["colJSON"] = MakeJsonValue(id),
                ["colJSON2"] = MakeJsonValue2(id)
            };

        internal override DataRow MakeModifiedRow(DataRow row)
        {
            var seq = ++modifySeq;
            var modifiedRow = DeepCopy(row);
            var row2 = MakeRow(row.Id + seq + 20);

            var value = modifiedRow["colBoolean"];
            if (value != FieldValue.Null)
            {
                modifiedRow["colBoolean"] = !value.AsBoolean;
            }

            value = modifiedRow["colInteger"];
            if (value != FieldValue.Null)
            {
                modifiedRow["colInteger"] = value.AsInt32 + 1;
            }

            value = modifiedRow["colLong"];
            if (value != FieldValue.Null)
            {
                modifiedRow["colLong"] = value.AsInt64 + 1;
            }

            foreach(var column in CopyColumns)
            {
                modifiedRow[column] = row2[column];
            }

            return modifiedRow;
        }

    }

    internal class DataTestFixture
    {
        private readonly DataRowFactory factory;

        internal DataTestFixture(TableInfo table, DataRowFactory factory,
            int rowCount, IndexInfo[] indexes = null)
        {
            Table = table;
            Indexes = indexes;
            this.factory = factory;
            Rows = new DataRow[rowCount];
            for (var i = 0; i < rowCount; i++)
            {
                Rows[i] = factory.MakeRow(i);
            }
        }

        internal TableInfo Table { get; }

        internal IndexInfo[] Indexes { get; }

        internal DataRow[] Rows { get; }

        internal int RowCount => Rows.Length;

        internal bool HasRow(int id) => id >= 0 && id < Rows.Length;

        internal DataRow GetRow(int id, bool returnNull = false)
        {
            if (!HasRow(id))
            {
                if (returnNull)
                {
                    return null;
                }
                Assert.Fail($"Row with id {id} is not found");
            }

            return Rows[id];
        }

        internal DataRow MakeRow(int id) => factory.MakeRow(id);

        internal DataRow MakeModifiedRow(DataRow row) =>
            factory.MakeModifiedRow(row);

        internal int GetRowIdFromShard(int shardId, int rowIndex = 0) =>
            factory.GetRowIdFromShard(shardId, rowIndex);

        internal DataRow MakeRowFromShard(int shardId, int rowIndex = 0) =>
            factory.MakeRowFromShard(shardId, rowIndex);

        internal int RowIdStart => 0;

        internal int RowIdEnd => Rows.Length;

        internal int RowsPerShard => factory.RowsPerShard;

        internal int MaxRowKB => factory.MaxRowKB;
    }

}
