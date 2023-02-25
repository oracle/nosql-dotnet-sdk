/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.NsonProtocol
{
    using System;
    using System.IO;
    using System.Text.Json;
    using static Protocol;
    using Opcode = BinaryProtocol.Opcode;
    using NsonType = DbType;

    internal partial class RequestSerializer
    {
        private TableUsageRecord DeserializeTableUsageRecord(
            NsonReader reader)
        {
            var result = new TableUsageRecord();
            ReadMap(reader, field =>
            {
                switch (field)
                {
                    case FieldNames.Start:
                        result.StartTime = ReadDateTimeAsString(reader);
                        return true;
                    case FieldNames.TableUsagePeriod:
                        result.Duration = TimeSpan.FromSeconds(
                            reader.ReadInt32());
                        return true;
                    case FieldNames.ReadUnits:
                        result.ReadUnits = reader.ReadInt32();
                        return true;
                    case FieldNames.WriteUnits:
                        result.WriteUnits = reader.ReadInt32();
                        return true;
                    case FieldNames.StorageGB:
                        result.StorageGB = reader.ReadInt32();
                        return true;
                    case FieldNames.ReadThrottleCount:
                        result.ReadThrottleCount = reader.ReadInt32();
                        return true;
                    case FieldNames.WriteThrottleCount:
                        result.WriteThrottleCount = reader.ReadInt32();
                        return true;
                    case FieldNames.StorageThrottleCount:
                        result.StorageThrottleCount = reader.ReadInt32();
                        return true;
                    case FieldNames.MaxShardUsagePercent:
                        result.MaxShardUsagePercent = reader.ReadInt32();
                        return true;
                    default:
                        return false;
                }
            });

            return result;
        }

        private IndexResult DeserializeIndexResult(NsonReader reader)
        {
            var result = new IndexResult();
            ReadMap(reader, field =>
            {
                switch (field)
                {
                    case FieldNames.Name:
                        result.IndexName = reader.ReadString();
                        return true;
                    case FieldNames.Fields:
                        // We can't use ReadArray() here since we need to
                        // populate two arrays.
                        reader.ExpectType(NsonType.Array);
                        var fields = new string[reader.Count];
                        var fieldTypes = new string[fields.Length];
                        for (var i = 0; i < fields.Length; i++)
                        {
                            string fieldName = null;
                            string fieldType = null;

                            reader.Next();
                            ReadMap(reader, fieldField =>
                            {
                                switch (fieldField)
                                {
                                    case FieldNames.Path:
                                        fieldName = reader.ReadString();
                                        return true;
                                    case FieldNames.Type:
                                        fieldType = reader.ReadString();
                                        return true;
                                    default:
                                        return false;
                                }
                            });

                            fields[i] = fieldName ??
                                        throw new BadProtocolException(
                                "Missing field name in IndexResult");
                            fieldTypes[i] = fieldType;
                        }

                        result.Fields = fields;
                        result.FieldTypes = fieldTypes;
                        return true;
                    default:
                        return false;
                }
            });

            return result;
        }

        public void SerializeTableDDL(MemoryStream stream,
            TableDDLRequest request)
        {
            var writer = GetNsonWriter(stream);
            writer.StartMap();
            WriteHeader(writer, Opcode.TableRequest, request);
            writer.StartMap(FieldNames.Payload);
            
            if (request.Statement != null)
            {
                writer.WriteString(FieldNames.Statement, request.Statement);
            }
            
            var tableLimits = request.GetTableLimits();
            if (tableLimits != null)
            {
                writer.StartMap(FieldNames.Limits);
                writer.WriteInt32(FieldNames.ReadUnits, tableLimits.ReadUnits);
                writer.WriteInt32(FieldNames.WriteUnits,
                    tableLimits.WriteUnits);
                writer.WriteInt32(FieldNames.StorageGB, tableLimits.StorageGB);
                writer.WriteInt32(FieldNames.LimitsMode,
                    (int)tableLimits.CapacityMode);
                writer.EndMap();
            }

            var definedTags = request.GetDefinedTags();
            if (definedTags != null)
            {
                writer.WriteString(FieldNames.DefinedTags,
                    JsonSerializer.Serialize(definedTags));
            }

            var freeFormTags = request.GetFreeFormTags();
            if (freeFormTags != null)
            {
                writer.WriteString(FieldNames.FreeFormTags,
                    JsonSerializer.Serialize(freeFormTags));
            }

            if (request.Options?.MatchETag != null)
            {
                writer.WriteString(FieldNames.Etag,
                    request.Options.MatchETag);
            }

            writer.EndMap();
            writer.EndMap();
        }

        public TableResult DeserializeTableDDL(MemoryStream stream,
            TableDDLRequest request)
        {
            var reader = GetNsonReader(stream);
            var result = new TableResult(request);
            return DeserializeTableResult(reader, request, result);
        }

        public void SerializeGetTable(MemoryStream stream,
            GetTableRequest request)
        {
            var writer = GetNsonWriter(stream);
            writer.StartMap();
            WriteHeader(writer, Opcode.GetTable, request);
            writer.StartMap(FieldNames.Payload);
            if (request.operationId != null)
            {
                writer.WriteString(FieldNames.OperationId,
                    request.operationId);
            }
            writer.EndMap();
            writer.EndMap();
        }

        public TableResult DeserializeGetTable(MemoryStream stream,
            GetTableRequest request)
        {
            var reader = GetNsonReader(stream);
            var result = new TableResult();
            return DeserializeTableResult(reader, request, result);
        }

        public void SerializeGetTableUsage(MemoryStream stream,
            GetTableUsageRequest request)
        {
            var writer = GetNsonWriter(stream);
            writer.StartMap();
            WriteHeader(writer, Opcode.GetTableUsage, request);
            writer.StartMap(FieldNames.Payload);
            
            if (request.Options?.StartTime.HasValue ?? false)
            {
                WriteDateTimeAsString(writer, FieldNames.Start,
                    request.Options.StartTime.Value);
            }

            if (request.Options?.EndTime.HasValue ?? false)
            {
                WriteDateTimeAsString(writer, FieldNames.End,
                    request.Options.EndTime.Value);
            }

            if (request.Options?.Limit.HasValue ?? false)
            {
                writer.WriteInt32(FieldNames.ListMaxToRead,
                    request.Options.Limit.Value);
            }


            if (request.Options?.FromIndex.HasValue ?? false)
            {
                writer.WriteInt32(FieldNames.ListStartIndex,
                    request.Options.FromIndex.Value);
            }

            writer.EndMap();
            writer.EndMap();
        }

        public TableUsageResult DeserializeGetTableUsage(MemoryStream stream,
            GetTableUsageRequest request)
        {
            var reader = GetNsonReader(stream);
            var result = new TableUsageResult();

            DeserializeResponse(reader, field =>
            {
                switch (field)
                {
                    case FieldNames.TableName:
                        result.TableName = reader.ReadString();
                        return true;
                    case FieldNames.TableUsage:
                        result.UsageRecords = ReadArray(reader,
                            DeserializeTableUsageRecord);
                        return true;
                    case FieldNames.LastIndex:
                        result.NextIndex = reader.ReadInt32();
                        return true;
                    default:
                        return false;
                }
            }, request, result);
            return result;
        }

        public void SerializeGetIndexes(MemoryStream stream,
            GetIndexesRequest request)
        {
            var writer = GetNsonWriter(stream);
            writer.StartMap();
            WriteHeader(writer, Opcode.GetIndexes, request);
            writer.StartMap(FieldNames.Payload);
            var indexName = request.GetIndexName();
            
            if (indexName != null)
            {
                writer.WriteString(FieldNames.Index, indexName);
            }

            writer.EndMap();
            writer.EndMap();
        }

        public IndexResult[] DeserializeGetIndexes(MemoryStream stream,
            GetIndexesRequest request)
        {
            var reader = GetNsonReader(stream);
            IndexResult[] result = null;

            DeserializeResponse(reader, field =>
            {
                if (field != FieldNames.Indexes)
                {
                    return false;
                }

                result = ReadArray(reader, DeserializeIndexResult);
                return true;
            }, request, result);

            return result ?? Array.Empty<IndexResult>();
        }

        public void SerializeListTables(MemoryStream stream,
            ListTablesRequest request)
        {
            var writer = GetNsonWriter(stream);
            writer.StartMap();
            WriteHeader(writer, Opcode.ListTables, request);
            writer.StartMap(FieldNames.Payload);
            
            OptionallyWriteInt32(writer, FieldNames.ListStartIndex,
                request.Options?.FromIndex);
            OptionallyWriteInt32(writer, FieldNames.ListMaxToRead,
                request.Options?.Limit);

            if (request.Options?.Namespace != null)
            {
                writer.WriteString(FieldNames.Namespace,
                    request.Options.Namespace);
            }

            writer.EndMap();
            writer.EndMap();
        }

        public ListTablesResult DeserializeListTables(MemoryStream stream,
            ListTablesRequest request)
        {
            var reader = GetNsonReader(stream);
            var result = new ListTablesResult();

            DeserializeResponse(reader, field =>
            {
                switch (field)
                {
                    case FieldNames.Tables:
                        result.TableNames = ReadArray(reader,
                            reader.ReadString);
                        return true;
                    case FieldNames.LastIndex:
                        result.NextIndex = reader.ReadInt32();
                        return true;
                    default:
                        return false;
                }
            }, request, result);

            result.TableNames ??= Array.Empty<string>();
            return result;
        }
    }
}
