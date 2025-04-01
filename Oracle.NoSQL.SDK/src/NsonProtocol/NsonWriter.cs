/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.NsonProtocol
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Globalization;
    using NsonType = DbType;
    using BinaryProtocol = BinaryProtocol.Protocol;

    internal class NsonWriter
    {
        private readonly MemoryStream stream;
        private readonly Stack<int> offsetStack;
        private readonly Stack<int> sizeStack;

        private void IncrementSize()
        {
            if (sizeStack.Count != 0)
            {
                var size = sizeStack.Pop();
                sizeStack.Push(size + 1);
            }
        }

        private void StartComplexValue()
        {
            var off = (int)stream.Position;
            // size in bytes
            BinaryProtocol.WriteUnpackedInt32(stream, 0);
            // number of elements
            BinaryProtocol.WriteUnpackedInt32(stream, 0);
            offsetStack.Push(off);
            sizeStack.Push(0);
        }

        private void EndComplexValue()
        {
            var off = offsetStack.Pop();
            var size = sizeStack.Pop();
            var current = (int)stream.Position;
            stream.Seek(off, SeekOrigin.Begin);
            // Write byte size and number of elements into the space reserved.
            // Object starts at off + 4.
            BinaryProtocol.WriteUnpackedInt32(stream, current - off - 4);
            BinaryProtocol.WriteUnpackedInt32(stream, size);
            stream.Seek(current, SeekOrigin.Begin);
            IncrementSize();
        }

        internal NsonWriter(MemoryStream stream)
        {
            this.stream = stream;
            offsetStack = new Stack<int>();
            sizeStack = new Stack<int>();
        }

        internal void WriteFieldName(string fieldName)
        {
            BinaryProtocol.WriteString(stream, fieldName);
        }

        internal void WriteBoolean(bool value)
        {
            stream.WriteByte((byte)NsonType.Boolean);
            BinaryProtocol.WriteBoolean(stream, value);
            IncrementSize();
        }

        internal void WriteBoolean(string fieldName, bool value)
        {
            WriteFieldName(fieldName);
            WriteBoolean(value);
        }

        internal void WriteInt32(int value)
        {
            stream.WriteByte((byte)NsonType.Integer);
            BinaryProtocol.WritePackedInt32(stream, value);
            IncrementSize();
        }

        internal void WriteInt32(string fieldName, int value)
        {
            WriteFieldName(fieldName);
            WriteInt32(value);
        }

        internal void WriteInt64(long value)
        {
            stream.WriteByte((byte)NsonType.Long);
            BinaryProtocol.WritePackedInt64(stream, value);
            IncrementSize();
        }

        internal void WriteInt64(string fieldName, long value)
        {
            WriteFieldName(fieldName);
            WriteInt64(value);
        }

        internal void WriteDouble(double value)
        {
            stream.WriteByte((byte)NsonType.Double);
            BinaryProtocol.WriteDouble(stream, value);
            IncrementSize();
        }

        internal void WriteDouble(string fieldName, double value)
        {
            WriteFieldName(fieldName);
            WriteDouble(value);
        }

        internal void WriteString(string value)
        {
            stream.WriteByte((byte)NsonType.String);
            BinaryProtocol.WriteString(stream, value);
            IncrementSize();
        }

        internal void WriteString(string fieldName, string value)
        {
            WriteFieldName(fieldName);
            WriteString(value);
        }

        internal void WriteByteArray(byte[] value)
        {
            stream.WriteByte((byte)NsonType.Binary);
            BinaryProtocol.WriteByteArray(stream, value);
            IncrementSize();
        }

        internal void WriteByteArray(string fieldName, byte[] value)
        {
            WriteFieldName(fieldName);
            WriteByteArray(value);
        }

        internal void WriteDateTime(DateTime value)
        {
            stream.WriteByte((byte)NsonType.Timestamp);
            BinaryProtocol.WriteDateTime(stream, value);
            IncrementSize();
        }

        internal void WriteDateTime(string fieldName, DateTime value)
        {
            WriteFieldName(fieldName);
            WriteDateTime(value);
        }

        internal void WriteDecimal(decimal value)
        {
            stream.WriteByte((byte)NsonType.Number);
            BinaryProtocol.WriteString(stream,
                value.ToString(CultureInfo.InvariantCulture));
            IncrementSize();
        }

        internal void WriteDecimal(string fieldName, decimal value)
        {
            WriteFieldName(fieldName);
            WriteDecimal(value);
        }

        internal void WriteJsonNull()
        {
            stream.WriteByte((byte)NsonType.JsonNull);
            IncrementSize();
        }

        internal void WriteJsonNull(string fieldName)
        {
            WriteFieldName(fieldName);
            WriteJsonNull();
        }

        internal void WriteNull()
        {
            stream.WriteByte((byte)NsonType.Null);
            IncrementSize();
        }

        internal void WriteNull(string fieldName)
        {
            WriteFieldName(fieldName);
            WriteNull();
        }

        internal void WriteEmptyValue()
        {
            stream.WriteByte((byte)NsonType.Empty);
            IncrementSize();
        }

        internal void WriteEmptyValue(string fieldName)
        {
            WriteFieldName(fieldName);
            WriteEmptyValue();
        }

        internal void StartArray()
        {
            stream.WriteByte((byte)NsonType.Array);
            StartComplexValue();
        }

        internal void StartArray(string fieldName)
        {
            WriteFieldName(fieldName);
            StartArray();
        }

        internal void EndArray()
        {
            EndComplexValue();
        }

        internal void StartMap()
        {
            stream.WriteByte((byte)NsonType.Map);
            StartComplexValue();
        }

        internal void StartMap(string fieldName)
        {
            WriteFieldName(fieldName);
            StartMap();
        }

        internal void EndMap()
        {
            EndComplexValue();
        }

    }

}
