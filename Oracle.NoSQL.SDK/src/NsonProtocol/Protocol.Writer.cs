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
    using System.Diagnostics;
    using System.IO;
    using BinaryProtocol = BinaryProtocol.Protocol;
    using Opcode = BinaryProtocol.Opcode;

    // Static methods for writing binary protocol data

    internal static partial class Protocol
    {
        // In future we can consider pooling these objects.
        internal static NsonWriter GetNsonWriter(MemoryStream stream)
        {
            return new NsonWriter(stream);
        }

        // In most cases, the table name will be request.InternalTableName,
        // however for WriteManyRequest, request.InternalTableName is used by
        // rate limiter and is set even for multi-table requests (in which
        // case it shouldn't be a part of the header).
        internal static void WriteHeader(NsonWriter writer, Opcode opcode,
            Request request, string tableName)
        {
            writer.StartMap(FieldNames.Header);
            writer.WriteInt32(FieldNames.Version, SerialVersion);
            
            if (tableName != null)
            {
                writer.WriteString(FieldNames.TableName, tableName);
            }

            writer.WriteInt32(FieldNames.Opcode, (int)opcode);
            writer.WriteInt32(FieldNames.Timeout,
                request.RequestTimeoutMillis);
            writer.WriteInt32(FieldNames.TopoSeqNum,
                request.QueryTopologySequenceNumber);
            writer.EndMap();
        }

        internal static void WriteHeader(NsonWriter writer, Opcode opcode,
            Request request) => WriteHeader(writer, opcode, request,
            request.InternalTableName);

        internal static void WriteConsistency(NsonWriter writer,
            Consistency consistency)
        {
            writer.StartMap(FieldNames.Consistency);
            writer.WriteInt32(FieldNames.Type, (int)consistency);
            writer.EndMap();
        }

        internal static void WriteDurability(NsonWriter writer,
            Durability? durability) =>
            writer.WriteInt32(FieldNames.Durability,
                BinaryProtocol.GetDurabilityInternal(durability));

        internal static void WriteArray(NsonWriter writer,
            ArrayValue arrayValue)
        {
            writer.StartArray();
            
            foreach (var fieldValue in arrayValue)
            {
                WriteFieldValue(writer, fieldValue);
            }

            writer.EndArray();
        }

        internal static void WriteMap(NsonWriter writer, MapValue mapValue)
        {
            writer.StartMap();
            
            foreach (var kv in mapValue)
            {
                writer.WriteFieldName(kv.Key);
                WriteFieldValue(writer, kv.Value);
            }

            writer.EndMap();
        }

        internal static void WriteFieldValue(NsonWriter writer,
            FieldValue fieldValue)
        {
            DbType dbType = fieldValue.DbType;
            switch (dbType)
            {
                case DbType.Array:
                    WriteArray(writer, fieldValue.AsArrayValue);
                    break;
                case DbType.Binary:
                    writer.WriteByteArray(fieldValue.AsByteArray);
                    break;
                case DbType.Boolean:
                    writer.WriteBoolean(fieldValue.AsBoolean);
                    break;
                case DbType.Double:
                    writer.WriteDouble(fieldValue.AsDouble);
                    break;
                case DbType.Integer:
                    writer.WriteInt32(fieldValue.AsInt32);
                    break;
                case DbType.Long:
                    writer.WriteInt64(fieldValue.AsInt64);
                    break;
                case DbType.Map:
                    WriteMap(writer, fieldValue.AsMapValue);
                    break;
                case DbType.String:
                    writer.WriteString(fieldValue.AsString);
                    break;
                case DbType.Timestamp:
                    writer.WriteDateTime(fieldValue.AsDateTime);
                    break;
                case DbType.Number:
                    writer.WriteDecimal(fieldValue.AsDecimal);
                    break;
                case DbType.JsonNull:
                    writer.WriteJsonNull();
                    break;
                case DbType.Null:
                    writer.WriteNull();
                    break;
                case DbType.Empty:
                    writer.WriteEmptyValue();
                    break;
                default:
                    Debug.Assert(false, "Invalid DbType");
                    break;
            }

        }

        internal static void WriteKey(NsonWriter writer, MapValue key)
        {
            Debug.Assert(key != null);
            writer.WriteFieldName(FieldNames.Key);
            WriteFieldValue(writer, key);
        }

        internal static void WriteValue(NsonWriter writer, FieldValue value)
        {
            writer.WriteFieldName(FieldNames.Value);
            WriteFieldValue(writer, value);
        }

        // same as in Java driver
        internal static string TTLToString(TimeToLive ttl) => ttl.Value +
            (ttl.TimeUnit == TTLTimeUnit.Days ? " DAYS" : " HOURS");

        internal static void SerializeWriteRequest(NsonWriter writer,
            WriteRequest request)
        {
            WriteDurability(writer, request.Durability);
            writer.WriteBoolean(FieldNames.ReturnRow, request.ReturnExisting);
        }

        internal static void WriteFieldRange(NsonWriter writer,
            FieldRange fieldRange)
        {
            writer.StartMap(FieldNames.Range);
            writer.WriteString(FieldNames.RangePath, fieldRange.FieldName);

            if (fieldRange.StartValue != null)
            {
                writer.StartMap(FieldNames.Start);
                WriteValue(writer, fieldRange.StartValue);
                writer.WriteBoolean(FieldNames.Inclusive,
                    fieldRange.IsStartInclusive);
                writer.EndMap();
            }

            if (fieldRange.EndValue != null)
            {
                writer.StartMap(FieldNames.End);
                WriteValue(writer, fieldRange.EndValue);
                writer.WriteBoolean(FieldNames.Inclusive,
                    fieldRange.IsEndInclusive);
                writer.EndMap();
            }

            writer.EndMap();
        }

        internal static void OptionallyWriteInt32(NsonWriter writer,
            string fieldName, int? value)
        {
            if (value.HasValue)
            {
                writer.WriteInt32(fieldName, value.Value);
            }
        }

        internal static void OptionallyWriteInt64(NsonWriter writer,
            string fieldName, long? value)
        {
            if (value.HasValue)
            {
                writer.WriteInt64(fieldName, value.Value);
            }
        }

        internal static void OptionallyWriteString(NsonWriter writer,
            string fieldName, string value)
        {
            if (value != null)
            {
                writer.WriteString(fieldName, value);
            }
        }

        // Somehow GetTableUsage request expects Nson strings instead of Nson
        // Timestamps for StartTime and EndTime parameters. 
        internal static void WriteDateTimeAsString(NsonWriter writer,
            string fieldName, DateTime value)
        {
            writer.WriteString(fieldName,
                BinaryProtocol.DateTimeToString(value));
        }

        internal static void WriteArray<T>(NsonWriter writer,
            IEnumerable<T> value, Action<T> writeElement)
        {
            writer.StartArray();

            foreach (var elem in value)
            {
                writeElement(elem);
            }

            writer.EndArray();
        }

    }

}
