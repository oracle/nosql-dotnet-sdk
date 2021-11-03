/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using SDK.BinaryProtocol;
    using static Utils;
    using static SizeOf;

    internal partial class ReceiveIterator
    {
        private class SimpleResult
        {
            protected readonly ReceiveIterator iterator;
            private int index;

            protected bool HasLocalResults =>
                Rows != null && index < Rows.Count;

            internal bool HasResults =>
                HasLocalResults || ContinuationKey != null;

            internal bool HasRemoteResults =>
                Rows == null || ContinuationKey != null;

            internal IList<RecordValue> Rows { get; set; }

            internal byte[] ContinuationKey { get; set; }

            internal RecordValue Current
            {
                get
                {
                    Debug.Assert(Rows != null && index < Rows.Count);
                    return Rows[index];
                }
            }

            internal SimpleResult(ReceiveIterator iterator)
            {
                this.iterator = iterator;
            }

            internal RecordValue Next()
            {
                if (Rows == null || index >= Rows.Count)
                {
                    return null;
                }

                var row = Rows[index];
                Rows[index++] = null; // Release memory for the row
                return row;
            }

            internal void SetQueryResult(QueryResult<RecordValue> result)
            {
                // Established during deserialization
                Debug.Assert(result.Rows is RecordValue[]);

                Rows = (RecordValue[])result.Rows;
                ContinuationKey = result.ContinuationKey?.Bytes;
                index = 0;
            }
        }

        // Shard or partition result
        private class PartialResult : SimpleResult, IComparable<PartialResult>
        {
            private static readonly long PartialResultOverhead =
                IntPtr.Size * 3 + 16 + SortedSetEntryOverhead;

            internal int Id { get; } // Shard or partition id

            internal long Memory { get; private set; }

            internal PartialResult(ReceiveIterator iterator, int id) :
                base(iterator)
            {
                Id = id;
            }

            internal PartialResult(ReceiveIterator iterator, int partitionId,
                IList<RecordValue> rows, byte[] continuationKey) :
                this(iterator, partitionId)
            {
                Rows = rows;
                ContinuationKey = continuationKey;
                SetMemoryStats();
            }

            internal void SetMemoryStats()
            {
                Memory = PartialResultOverhead + GetMemorySize(ArrayOverhead,
                    Rows.Count, Rows);
                iterator.totalRows += Rows.Count;
                iterator.totalMemory += Memory;
                iterator.runtime.TotalMemory += Memory;
            }

            public int CompareTo(PartialResult other)
            {
                if (!HasLocalResults)
                {
                    return other.HasLocalResults ? -1 : Id.CompareTo(other.Id);
                }

                if (!other.HasLocalResults)
                {
                    return 1;
                }

                var result = CompareRows(Current, other.Current,
                    iterator.step.SortSpecs);
                return result != 0 ? result : Id.CompareTo(other.Id);
            }
        }

        private class Duplicates : IEqualityComparer<byte[]>
        {
            private readonly ReceiveIterator iterator;
            private readonly HashSet<byte[]> set;
            private readonly MemoryStream stream;

            internal Duplicates(ReceiveIterator iterator)
            {
                this.iterator = iterator;
                set = new HashSet<byte[]>(this);
                stream = new MemoryStream();
            }

            public bool Equals(byte[] array1, byte[] array2)
            {
                Debug.Assert(array1 != null && array2 != null);
                return BinaryValue.ByteArraysEqual(array1, array2);
            }

            public int GetHashCode(byte[] array)
            {
                Debug.Assert(array != null);
                return BinaryValue.GetByteArrayHashCode(array);
            }

            internal long Memory { get; private set; }

            private static void WritePrimaryKeyField(MemoryStream stream,
                string name, FieldValue value)
            {
                switch (value.DbType)
                {
                    case DbType.Integer:
                        Protocol.WritePackedInt32(stream, value.AsInt32);
                        break;
                    case DbType.Long:
                        Protocol.WritePackedInt64(stream, value.AsInt64);
                        break;
                    case DbType.Double:
                        Protocol.WriteDouble(stream, value.AsDouble);
                        break;
                    case DbType.Number:
                    {
                        var bits32 = decimal.GetBits(value.AsDecimal);
                        Debug.Assert(bits32?.Length == 4);
                        // Since the length of bits32 is always 4, we only
                        // need to write the values themselves
                        foreach (var bit32 in bits32)
                        {
                            Protocol.WriteUnpackedInt32(stream, bit32);
                        }
                    }
                        break;
                    case DbType.String:
                        Protocol.WriteString(stream, value.AsString);
                        break;
                    case DbType.Timestamp:
                        Protocol.WriteDateTime(stream, value.AsDateTime);
                        break;
                    default:
                        throw new InvalidOperationException(
                            "Query: unexpected type for primary key field: " +
                            $"{value.DbType} for field {name}");
                }
            }

            private byte[] PrimaryKeyToBytes(RecordValue row)
            {
                stream.SetLength(0);
                foreach (var name in iterator.step.PrimaryKeyFields)
                {
                    WritePrimaryKeyField(stream, name, row[name]);
                }

                return stream.ToArray();
            }

            internal bool IsDuplicate(RecordValue row)
            {
                byte[] value = PrimaryKeyToBytes(row);
                if (!set.Add(value))
                {
                    return true;
                }

                var size = GetHashSetEntrySize(GetByteArraySize(value));
                Memory += size;
                iterator.runtime.TotalMemory += size;
                return false;
            }

        }
    }
}
