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
    using System.Text.Json;
    using BinaryProtocol;
    using BinaryProtocol = BinaryProtocol.Protocol;
    using NsonType = DbType;
    using static ValidateUtils;

    // Static methods for reading binary protocol values

    internal static partial class Protocol
    {
        // In future we can consider pooling these objects.
        internal static NsonReader GetNsonReader(MemoryStream stream)
        {
            return new NsonReader(stream);
        }

        // We need to be able to call MemoryStream.GetBuffer(), which is not
        // possible with instance created by the 1-argument constructor.
        internal static MemoryStream GetMemoryStreamWithVisibleBuffer(
            byte[] buf) =>
            new MemoryStream(buf, 0, buf.Length, false, true);

        internal static ArrayValue ReadArrayValue(NsonReader reader)
        {
            var count = reader.Count;
            var arrayValue = new ArrayValue(count);
            for (var i = 0; i < count; i++)
            {
                reader.Next();
                arrayValue.Add(ReadFieldValue(reader));
            }

            return arrayValue;
        }
        
        internal static void ReadMapValue(NsonReader reader, MapValue mapValue)
        {
            var count = reader.Count;
            for (var i = 0; i < count; i++)
            {
                reader.Next();
                mapValue.Add(reader.FieldName, ReadFieldValue(reader));
            }
        }

        internal static MapValue ReadMapValue(NsonReader reader)
        {
            // This is a workaround because the protocol does not
            // currently distinguish between ordered and unordered maps.
            // Some query logic relies on having record type sub-fields
            // that are ordered.  Once the protocol distinguishes between
            // those we can change the created instance type to MapValue.
            MapValue mapValue = new RecordValue();

            ReadMapValue(reader, mapValue);
            return mapValue;
        }

        // Here we assume the type code has already been read.
        internal static FieldValue ReadFieldValue(NsonReader reader)
        {
            switch (reader.NsonType)
            {
                case NsonType.Array:
                    return ReadArrayValue(reader);
                case NsonType.Binary:
                    return reader.ReadByteArray();
                case NsonType.Boolean:
                    return reader.ReadBoolean();
                case NsonType.Double:
                    return reader.ReadDouble();
                case NsonType.Integer:
                    return reader.ReadInt32();
                case NsonType.Long:
                    return reader.ReadInt64();
                case NsonType.Map:
                    return ReadMapValue(reader);
                case NsonType.String:
                    return reader.ReadString();
                case NsonType.Timestamp:
                    return reader.ReadDateTime();
                case NsonType.Number:
                    return BinaryProtocol.GetNumberValue(
                        reader.ReadNumberAsString());
                case NsonType.Null:
                    return FieldValue.Null;
                case NsonType.JsonNull:
                    return FieldValue.JsonNull;
                case NsonType.Empty:
                    return FieldValue.Empty;
                default:
                    throw new BadProtocolException(
                        $"Read unknown Nson type code: {reader.NsonType}");
            }
        }

        internal static RecordValue ReadRow(NsonReader reader)
        {
            if (reader.NsonType != NsonType.Map)
            {
                throw new BadProtocolException(
                    "Invalid type code for table record: " +
                    $"{reader.NsonType}, must be {DbType.Map}");
            }
            
            var result = new RecordValue();
            ReadMapValue(reader, result);
            return result;
        }

        internal static RowVersion ReadRowVersion(NsonReader reader)
        {
            var data = reader.ReadByteArray();
            return data != null ? new RowVersion(data) : null;
        }

        internal static T[] ReadArray<T>(NsonReader reader,
            Func<T> readElement)
        {
            reader.ExpectType(NsonType.Array);

            var result = new T[reader.Count];
            for (var i = 0; i < result.Length; i++)
            {
                reader.Next();
                result[i] = readElement();
            }

            return result;
        }

        internal static T[] ReadArray<T>(NsonReader reader,
            Func<NsonReader, T> readElement) =>
            ReadArray(reader, () => readElement(reader));

        // processField() takes field name as an argument.  It returns true
        // if the field was read and processed or false if the field was
        // ignored, in which case we will skip it.
        // We assume processField() gets any other needed info (including the
        // NsonReader instance) from the closure context, as this is more
        // convenient, but we may add other overloads if required.
        internal static void ReadMap(NsonReader reader,
            Func<string, bool> processField)
        {
            reader.ExpectType(NsonType.Map);

            var count = reader.Count;
            for (var i = 0; i < count; i++)
            {
                reader.Next();
                if (!processField(reader.FieldName))
                {
                    reader.SkipValue();
                }
            }
        }

        internal static void ValidateTopologyInfo(TopologyInfo topoInfo)
        {
            if (topoInfo.SequenceNumber < 0)
            {
                throw new BadProtocolException(
                    "Received invalid topology sequence number: " +
                    topoInfo.SequenceNumber);
            }

            if (topoInfo.ShardIds == null || topoInfo.ShardIds.Count == 0)
            {
                throw new BadProtocolException(
                    "Missing shard ids for topology sequence number " +
                    topoInfo.SequenceNumber);
            }
        }

        internal static TopologyInfo ReadTopologyInfo(NsonReader reader)
        {
            var seqNo = -1;
            int[] shardIds = null;

            ReadMap(reader, fieldName =>
            {
                switch (fieldName)
                {
                    case FieldNames.ProxyTopoSeqNum:
                        seqNo = reader.ReadInt32();
                        return true;
                    case FieldNames.ShardIds:
                        shardIds = ReadArray(reader, reader.ReadInt32);
                        return true;
                    default:
                        return false;
                }
            });

            var result = new TopologyInfo(seqNo, shardIds);
            ValidateTopologyInfo(result);
            return result;
        }

        internal static ConsumedCapacity DeserializeConsumedCapacity(
            NsonReader reader)
        {
            var result = new ConsumedCapacity();
            ReadMap(reader, fieldName =>
            {
                switch (fieldName)
                {
                    case FieldNames.ReadUnits:
                        result.ReadUnits = reader.ReadInt32();
                        return true;
                    case FieldNames.ReadKB:
                        result.ReadKB = reader.ReadInt32();
                        return true;
                    case FieldNames.WriteKB:
                        result.WriteKB = reader.ReadInt32();
                        result.WriteUnits = result.WriteKB;
                        return true;
                    default:
                        return false;
                }
            });

            return result;
        }

        internal static void DeserializeResponse(NsonReader reader,
            Func<string, bool> processField, Request request, object result)
        {
            var statusCode = 0;
            string message = null;

            reader.Next();
            ReadMap(reader, fieldName =>
            {
                switch (fieldName)
                {
                    case FieldNames.Consumed:
                        if (result is IDataResult dataResult &&
                            request.Config.ServiceType != ServiceType.KVStore)
                        {
                            dataResult.ConsumedCapacity =
                                DeserializeConsumedCapacity(reader);
                            return true;
                        }
                        return false;
                    case FieldNames.ErrorCode:
                        statusCode = reader.ReadInt32();
                        return true;
                    case FieldNames.Exception:
                        message = reader.ReadString();
                        return true;
                    case FieldNames.TopologyInfo:
                        // Query topology may be received by any dml or query
                        // request.
                        request.Client.SetQueryTopology(
                            ReadTopologyInfo(reader));
                        return true;
                    default:
                        return processField(fieldName);
                }
            });

            if (statusCode != 0)
            {
                throw BinaryProtocol.MapException((ErrorCode)statusCode,
                    message, request);
            }
        }

        internal static DateTime? ReadOptionalTimestamp(NsonReader reader)
        {
            var millis = reader.ReadInt64();
            if (millis != 0)
            {
                return DateTimeUtils.UnixMillisToDateTime(millis);
            }

            return null;
        }

        internal static void DeserializeReturnInfo<TRow>(NsonReader reader,
            IWriteResult<TRow> result)
        {
            ReadMap(reader, field =>
            {
                switch (field)
                {
                    case FieldNames.ExistingModTime:
                        result.ExistingModificationTime =
                            ReadOptionalTimestamp(reader);
                        return true;
                    case FieldNames.ExistingVersion:
                        result.ExistingVersion = ReadRowVersion(reader);
                        return true;
                    case FieldNames.ExistingValue:
                        result.ExistingRow = ReadRow(reader).ToObject<TRow>();
                        return true;
                    default:
                        return false;
                }
            });
        }

        internal static ReplicaInfo DeserializeReplicaInfo(NsonReader reader)
        {
            var result = new ReplicaInfo();
            ReadMap(reader, field =>
            {
                switch (field)
                {
                    case FieldNames.Region:
                        result.ReplicaName = reader.ReadString();
                        return true;
                    case FieldNames.TableOCID:
                        result.ReplicaOCID = reader.ReadString();
                        return true;
                    case FieldNames.WriteUnits:
                        result.WriteUnits = reader.ReadInt32();
                        return true;
                    case FieldNames.LimitsMode:
                        result.CapacityMode =
                            (CapacityMode)reader.ReadInt32();
                        return true;
                    case FieldNames.TableState:
                        result.TableState = (TableState)reader.ReadInt32();
                        return true;
                    default:
                        return false;
                }
            });

            return result;
        }

        internal static TableResult DeserializeTableResult(NsonReader reader,
            Request request, TableResult result)
        {
            DeserializeResponse(reader, field =>
            {
                switch (field)
                {
                    case FieldNames.CompartmentOCID:
                    case FieldNames.Namespace:
                        result.CompartmentId = reader.ReadString();
                        return true;
                    case FieldNames.TableOCID:
                        result.TableOCID = reader.ReadString();
                        return true;
                    case FieldNames.TableName:
                        result.TableName = reader.ReadString();
                        return true;
                    case FieldNames.TableState:
                        result.TableState = (TableState)reader.ReadInt32();
                        CheckReceivedEnumValue(result.TableState);
                        return true;
                    case FieldNames.TableSchema:
                        result.TableSchema = reader.ReadString();
                        return true;
                    case FieldNames.TableDDL:
                        result.TableDDL = reader.ReadString();
                        return true;
                    case FieldNames.OperationId:
                        result.OperationId = reader.ReadString();
                        return true;
                    case FieldNames.FreeFormTags:
                        result.FreeFormTags = JsonSerializer
                            .Deserialize<Dictionary<string, string>>(
                                reader.ReadString());
                        return true;
                    case FieldNames.DefinedTags:
                        result.DefinedTags = JsonSerializer
                            .Deserialize<Dictionary<string,
                                IDictionary<string, string>>>(
                                reader.ReadString());
                        return true;
                    case FieldNames.Etag:
                        result.ETag = reader.ReadString();
                        return true;
                    case FieldNames.Limits:
                        var readUnits = 0;
                        var writeUnits = 0;
                        var storageGB = 0;
                        var capacityMode = CapacityMode.Provisioned;
                        ReadMap(reader, limitsField =>
                        {
                            switch (limitsField)
                            {
                                case FieldNames.ReadUnits:
                                    readUnits = reader.ReadInt32();
                                    return true;
                                case FieldNames.WriteUnits:
                                    writeUnits = reader.ReadInt32();
                                    return true;
                                case FieldNames.StorageGB:
                                    storageGB = reader.ReadInt32();
                                    return true;
                                case FieldNames.LimitsMode:
                                    capacityMode =
                                        (CapacityMode)reader.ReadInt32();
                                    return true;
                                default:
                                    return false;
                            }
                        });
                        result.TableLimits = new TableLimits(readUnits,
                            writeUnits, storageGB, capacityMode);
                        return true;
                    case FieldNames.SchemaFrozen:
                        result.IsSchemaFrozen = reader.ReadBoolean();
                        return true;
                    case FieldNames.Initialized:
                        result.IsLocalReplicaInitialized = reader.ReadBoolean();
                        return true;
                    case FieldNames.Replicas:
                        result.Replicas = ReadArray(reader,
                            DeserializeReplicaInfo);
                        return true;
                    default:
                        return false;
                }
            }, request, result);

            return result;
        }

        internal static TableResult DeserializeTableResult(
            MemoryStream stream, Request request)
        {
            var reader = GetNsonReader(stream);
            // We only need to pass non-null request if it represents table
            // operation (e.g. TableDDL, AddReplica, etc.) to be used in
            // WaitForCompletion. We pass null if request is not an instance
            // of TableOperationRequest (e.g. GetTableRequest).
            var result = new TableResult(request as TableOperationRequest);
            return DeserializeTableResult(reader, request, result);
        }

        // GetTableUsage response sends strings for the timestamp values.
        internal static DateTime ReadDateTimeAsString(NsonReader reader) =>
            BinaryProtocol.StringToDateTime(reader.ReadString());

    }

}
