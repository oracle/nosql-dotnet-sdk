/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.BinaryProtocol
{
    using System;
    using System.Data;
    using System.IO;
    using DbType = DbType;

    // Static methods for reading binary protocol values

    internal static partial class Protocol
    {
        private static void ReadMap(MemoryStream stream,
            MapValue mapValue)
        {
            // Skip 4 bytes for total length
            stream.Seek(4, SeekOrigin.Current);
            var count = ReadUnpackedInt32(stream);
            for (var i = 0; i < count; i++)
            {
                var key = ReadString(stream);
                var value = ReadFieldValue(stream);
                mapValue.Add(key, value);
            }
        }

        internal static ArrayValue ReadArray(MemoryStream stream)
        {
            // Skip 4 bytes for total length
            stream.Seek(4, SeekOrigin.Current);
            var count = ReadUnpackedInt32(stream);
            var arrayValue = new ArrayValue(count);
            for (var i = 0; i < count; i++)
            {
                arrayValue.Add(ReadFieldValue(stream));
            }

            return arrayValue;
        }

        internal static MapValue ReadMap(MemoryStream stream)
        {
            // This is a workaround because the protocol does not
            // currently distinguish between ordered and unordered maps.
            // Some query logic relies on having record type sub-fields
            // that are ordered.  Once the protocol distinguishes between
            // those we can change the created instance type to MapValue.
            MapValue mapValue = new RecordValue();

            ReadMap(stream, mapValue);
            return mapValue;
        }

        internal static RecordValue ReadRecord(MemoryStream stream)
        {
            RecordValue recordValue = new RecordValue();
            ReadMap(stream, recordValue);
            return recordValue;
        }

        internal static FieldValue ReadFieldValue(MemoryStream stream)
        {
            var type = (DbType)ReadByte(stream);
            switch (type)
            {
                case DbType.Array:
                    return ReadArray(stream);
                case DbType.Binary:
                    return ReadByteArray(stream);
                case DbType.Boolean:
                    return ReadBoolean(stream);
                case DbType.Double:
                    return ReadDouble(stream);
                case DbType.Integer:
                    return ReadPackedInt32(stream);
                case DbType.Long:
                    return ReadPackedInt64(stream);
                case DbType.Map:
                    return ReadMap(stream);
                case DbType.String:
                    return ReadString(stream);
                case DbType.Timestamp:
                    return ReadDateTime(stream);
                case DbType.Number:
                    return ReadDecimal(stream);
                case DbType.Null:
                    return FieldValue.Null;
                case DbType.JsonNull:
                    return FieldValue.JsonNull;
                case DbType.Empty:
                    return FieldValue.Empty;
                default:
                    throw new BadProtocolException(
                        $"Unknown value type code: {type}");
            }
        }

        internal static RecordValue ReadRow(MemoryStream stream)
        {
            var type = (DbType)ReadByte(stream);
            if (type != DbType.Map)
            {
                throw new BadProtocolException(
                    $"Invalid type code for table record: {type}, " +
                    $"must be {DbType.Map}");
            }

            return ReadRecord(stream);
        }

        internal static RowVersion ReadRecordVersion(MemoryStream stream)
        {
            var data = ReadByteArray(stream);
            return data != null ? new RowVersion(data) : null;
        }

        internal static void DeserializeConsumedCapacity(
            MemoryStream stream,
            Request request,
            IDataResult result)
        {
            var readUnits = ReadPackedInt32(stream);
            var readKB = ReadPackedInt32(stream);
            var writeKB = ReadPackedInt32(stream);
            if (request.Config.ServiceType != ServiceType.KVStore)
            {
                result.ConsumedCapacity = new ConsumedCapacity(
                    readUnits, readKB, writeKB, writeKB);
            }
        }

        internal static void DeserializeWriteResponse<TRow>(
            MemoryStream stream, IWriteResult<TRow> result)
        {
            var returnInfo = ReadBoolean(stream);
            if (returnInfo)
            {
                result.ExistingRow = ReadRow(stream).ToObject<TRow>();
                result.ExistingVersion = ReadRecordVersion(stream);
            }
        }

        internal static void DeserializeWriteResponseWithId<TRow>(
            MemoryStream stream, IWriteResultWithId<TRow> result)
        {
            DeserializeWriteResponse(stream, result);
            if (ReadBoolean(stream))
            {
                result.GeneratedValue = ReadFieldValue(stream);
            }
        }

        internal static TableResult DeserializeTableResult(
            MemoryStream stream, Request request, TableResult result)
        {
            var hasInfo = ReadBoolean(stream);
            if (hasInfo)
            {
                var compartmentId = ReadString(stream);
                if (request.Config.ServiceType == ServiceType.Cloud)
                {
                    result.CompartmentId = compartmentId;
                }

                result.TableName = ReadString(stream);
                result.TableState = (TableState)ReadByte(stream);
                if (!Enum.IsDefined(typeof(TableState), result.TableState))
                {
                    throw new BadProtocolException(
                        "Received invalid value of table state: " +
                        result.TableState);
                }

                var hasStaticState = ReadBoolean(stream);
                if (hasStaticState)
                {
                    var readUnits = ReadPackedInt32(stream);
                    var writeUnits = ReadPackedInt32(stream);
                    var storageGB = ReadPackedInt32(stream);
                    if (request.Config.ServiceType !=
                        ServiceType.KVStore)
                    {
                        result.TableLimits = new TableLimits(readUnits,
                            writeUnits, storageGB);
                    }

                    result.TableSchema = ReadString(stream);
                }

                result.OperationId = ReadString(stream);
            }

            return result;
        }

        internal static AdminResult DeserializeAdminResult(
            MemoryStream stream, AdminResult result)
        {
            result.State = (AdminState)ReadByte(stream);
            if (!Enum.IsDefined(typeof(AdminState), result.State))
            {
                throw new BadProtocolException(
                    $"Received invalid value of admin state: {result.State}");
            }

            result.OperationId = ReadString(stream);
            result.Statement = ReadString(stream);
            result.Output = ReadString(stream);
            return result;
        }

        internal static TopologyInfo ReadTopologyInfo(MemoryStream stream)
        {
            var sequenceNumber = ReadPackedInt32(stream);
            if (sequenceNumber < -1)
            {
                throw new BadProtocolException(
                    $"Invalid topology sequence number: {sequenceNumber}");
            }

            if (sequenceNumber == -1)
            {
                // No topology info sent by the proxy
                return null;
            }

            var shardIds = ReadPackedInt32Array(stream);
            return new TopologyInfo(sequenceNumber, shardIds);
        }

    }

}
