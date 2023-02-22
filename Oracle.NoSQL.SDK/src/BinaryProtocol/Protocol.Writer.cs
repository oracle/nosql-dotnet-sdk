/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.BinaryProtocol
{
    using System.Diagnostics;
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.IO;

    // Static methods for writing binary protocol data

    internal static partial class Protocol
    {
        internal const int RequestSizeLimit = 2 * 1024 * 1024;

        internal static void WriteTimeout(MemoryStream stream, int timeout)
        {
            WritePackedInt32(stream, timeout);
        }

        internal static void WriteConsistency(MemoryStream stream,
            Consistency consistency)
        {
            WriteByte(stream, (byte) consistency);
        }

        internal static void WriteTTL(MemoryStream stream, TimeToLive? ttl)
        {
            // We assume TTL is valid (represented as days or hours)
            if (!ttl.HasValue)
            {
                WritePackedInt64(stream, -1);
            }
            else
            {
                Debug.Assert(ttl.Value.Value >= 0, "Invalid TTL value");
                WritePackedInt64(stream, ttl.Value.Value);
                WriteByte(stream, (byte)ttl.Value.TimeUnit);
            }
        }

        internal static void WriteArray(MemoryStream stream,
            ArrayValue arrayValue)
        {
            // Skip 4 bytes
            stream.Seek(4, SeekOrigin.Current);

            var startPosition = (int)stream.Position;
            WriteUnpackedInt32(stream, arrayValue.Count);
            foreach (var fieldValue in arrayValue)
            {
                WriteFieldValue(stream, fieldValue);
            }

            var endPosition = (int) stream.Position;
            stream.Position = startPosition - 4; // length offset
            WriteUnpackedInt32(stream, endPosition - startPosition);
            stream.Position = endPosition;
        }

        internal static void WriteMap(MemoryStream stream,
            MapValue mapValue)
        {
            // Skip 4 bytes
            stream.Seek(4, SeekOrigin.Current);

            var startPosition = (int)stream.Position;
            WriteUnpackedInt32(stream, mapValue.Count);
            foreach (var pair in mapValue)
            {
                WriteString(stream, pair.Key);
                WriteFieldValue(stream, pair.Value);
            }

            var endPosition = (int) stream.Position;
            stream.Position = startPosition - 4; // length offset
            WriteUnpackedInt32(stream, endPosition - startPosition);
            stream.Position = endPosition;
        }

        internal static void WriteVersion(MemoryStream stream,
            RowVersion version)
        {
            WriteByteArray(stream, version.Bytes);
        }

        internal static void WriteOpcode(MemoryStream stream, Opcode opcode,
            short serialVersion)
        {
            WriteUnpackedInt16(stream, serialVersion);
            WriteByte(stream, (byte) opcode);
        }

        internal static void WriteSubOpcode(MemoryStream stream, Opcode opcode)
        {
            WriteByte(stream, (byte) opcode);
        }

        internal static void WriteFieldRange(MemoryStream stream,
            FieldRange fieldRange)
        {
            if (fieldRange == null)
            {
                WriteBoolean(stream, false);
                return;
            }

            WriteBoolean(stream, true);
            WriteString(stream, fieldRange.FieldName);

            if (fieldRange.StartValue != null)
            {
                WriteBoolean(stream, true);
                WriteFieldValue(stream, fieldRange.StartValue);
                WriteBoolean(stream, fieldRange.IsStartInclusive);
            }
            else
            {
                WriteBoolean(stream, false);
            }

            if (fieldRange.EndValue != null)
            {
                WriteBoolean(stream, true);
                WriteFieldValue(stream, fieldRange.EndValue);
                WriteBoolean(stream, fieldRange.IsEndInclusive);
            }
            else
            {
                WriteBoolean(stream, false);
            }
        }

        internal static void WriteFieldValue(MemoryStream stream,
            FieldValue fieldValue)
        {
            DbType dbType = fieldValue.DbType;
            WriteByte(stream, (byte) dbType);
            switch (dbType)
            {
                case DbType.Array:
                    WriteArray(stream, fieldValue.AsArrayValue);
                    break;
                case DbType.Binary:
                    WriteByteArray(stream, fieldValue.AsByteArray);
                    break;
                case DbType.Boolean:
                    WriteBoolean(stream, fieldValue.AsBoolean);
                    break;
                case DbType.Double:
                    WriteDouble(stream, fieldValue.AsDouble);
                    break;
                case DbType.Integer:
                    WritePackedInt32(stream, fieldValue.AsInt32);
                    break;
                case DbType.Long:
                    WritePackedInt64(stream, fieldValue.AsInt64);
                    break;
                case DbType.Map:
                    WriteMap(stream, fieldValue.AsMapValue);
                    break;
                case DbType.String:
                    WriteString(stream, fieldValue.AsString);
                    break;
                case DbType.Timestamp:
                    WriteDateTime(stream, fieldValue.AsDateTime);
                    break;
                case DbType.Number:
                    WriteString(stream, fieldValue.AsDecimal.ToString(
                        CultureInfo.InvariantCulture));
                    break;
                case DbType.JsonNull:
                case DbType.Null:
                case DbType.Empty:
                    break;
                default:
                    Debug.Assert(false, "Invalid DbType");
                    break;
            }
        }

        internal static int GetDurabilityInternal(Durability? durability)
        {
            if (!durability.HasValue)
            {
                return 0;
            }

            var masterSync = (int)durability.Value.MasterSync + 1;
            var replicaSync = (int)durability.Value.ReplicaSync + 1;
            var replicaAck = (int)durability.Value.ReplicaAck + 1;

            return masterSync | (replicaSync << 2) | (replicaAck << 4);
        }

        internal static void WriteDurability(MemoryStream stream,
            Durability? durability, short serialVersion)
        {
            if (serialVersion < V3)
            {
                return;
            }

            WriteByte(stream, (byte)GetDurabilityInternal(durability));
        }

        internal static void SerializeRequest(MemoryStream stream,
            Request request)
        {
            Debug.Assert(request.RequestTimeoutMillis > 0,
                "Invalid request timeout");
            WriteTimeout(stream, request.RequestTimeoutMillis);
        }

        internal static void SerializeReadRequest(MemoryStream stream,
            ReadRequest request)
        {
            SerializeRequest(stream, request);
            Debug.Assert(request.TableName != null,
                "Read operation must have table name");
            WriteString(stream, request.TableName);
            WriteConsistency(stream, request.Consistency);
        }

        internal static void SerializeWriteRequest(MemoryStream stream,
            WriteRequest request, short serialVersion)
        {
            SerializeRequest(stream, request);
            Debug.Assert(request.TableName != null,
                "Write operation must have table name");
            WriteString(stream, request.TableName);
            WriteBoolean(stream, request.ReturnExisting);
            WriteDurability(stream, request.Durability, serialVersion);
        }

        internal static void WriteMathContext(MemoryStream stream)
        {
            WriteByte(stream, 0);
        }
    }
}
