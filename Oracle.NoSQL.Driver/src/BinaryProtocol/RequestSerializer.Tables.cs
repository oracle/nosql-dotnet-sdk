/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver.BinaryProtocol
{
    using System;
    using System.IO;
    using static Protocol;

    internal partial class RequestSerializer
    {
        private TableUsageRecord DeserializeTableUsageRecord(
            MemoryStream stream)
        {
            return new TableUsageRecord()
            {
                StartTime = DateTimeUtils.UnixMillisToDateTime(
                    ReadPackedInt64(stream)),
                Duration = TimeSpan.FromSeconds(ReadPackedInt32(stream)),
                ReadUnits = ReadPackedInt32(stream),
                WriteUnits = ReadPackedInt32(stream),
                StorageGB = ReadPackedInt32(stream),
                ReadThrottleCount = ReadPackedInt32(stream),
                WriteThrottleCount = ReadPackedInt32(stream),
                StorageThrottleCount = ReadPackedInt32(stream)
            };
        }

        private IndexResult DeserializeIndexResult(MemoryStream stream)
        {
            IndexResult result = new IndexResult()
            {
                IndexName = ReadString(stream)
            };

            var count = ReadPackedInt32(stream);
            if (count < 0)
            {
                throw new BadProtocolException(
                    $"Received invalid index field count: {count}");
            }

            string[] fields = new string[count];
            for (var i = 0; i < fields.Length; i++)
            {
                fields[i] = ReadString(stream);
            }

            result.Fields = fields;
            return result;
        }

        public void SerializeTableDDL(MemoryStream stream,
            TableDDLRequest request)
        {
            WriteOpcode(stream, Opcode.TableRequest);
            SerializeRequest(stream, request);
            WriteString(stream, request.Statement);

            var tableLimits = request.GetTableLimits();
            if (tableLimits != null)
            {
                WriteBoolean(stream, true);
                WriteUnpackedInt32(stream, tableLimits.ReadUnits);
                WriteUnpackedInt32(stream, tableLimits.WriteUnits);
                WriteUnpackedInt32(stream, tableLimits.StorageGB);

                var tableName = request.GetTableName();
                if (tableName != null)
                {
                    WriteBoolean(stream, true);
                    WriteString(stream, tableName);
                }
                else
                {
                    WriteBoolean(stream, false);
                }
            }
            else
            {
                WriteBoolean(stream, false);
            }
        }

        public TableResult DeserializeTableDDL(MemoryStream stream,
            TableDDLRequest request)
        {
            return DeserializeTableResult(stream, request,
                new TableResult(request));
        }

        public void SerializeGetTable(MemoryStream stream,
            GetTableRequest request)
        {
            WriteOpcode(stream, Opcode.GetTable);
            SerializeRequest(stream, request);
            WriteString(stream, request.TableName);
            WriteString(stream, request.operationId);
        }

        public TableResult DeserializeGetTable(MemoryStream stream,
            GetTableRequest request)
        {
            return DeserializeTableResult(stream, request, new TableResult());
        }

        public void SerializeGetTableUsage(MemoryStream stream,
            GetTableUsageRequest request)
        {
            WriteOpcode(stream, Opcode.GetTableUsage);
            SerializeRequest(stream, request);
            WriteString(stream, request.TableName);

            WritePackedInt64(stream, GetUnixMillisOrZero(
                request.Options?.StartTime));
            WritePackedInt64(stream, GetUnixMillisOrZero(
                request.Options?.EndTime));
            WritePackedInt32(stream, request.Options?.Limit ?? 0);
        }

        public TableUsageResult DeserializeGetTableUsage(MemoryStream stream,
            GetTableUsageRequest request)
        {
            // tenant id is not used
            ReadString(stream);
            var result = new TableUsageResult()
            {
                TableName = ReadString(stream)
            };

            var count = ReadPackedInt32(stream);
            if (count < 0)
            {
                throw new BadProtocolException(
                    $"Received invalid table usage record count: {count}");
            }

            var usageRecords = new TableUsageRecord[count];
            for (var i = 0; i < usageRecords.Length; i++)
            {
                usageRecords[i] = DeserializeTableUsageRecord(stream);
            }

            result.UsageRecords = usageRecords;
            return result;
        }

        public void SerializeGetIndexes(MemoryStream stream,
            GetIndexesRequest request)
        {
            WriteOpcode(stream, Opcode.GetIndexes);
            SerializeRequest(stream, request);
            WriteString(stream, request.TableName);

            var indexName = request.GetIndexName();
            if (indexName != null)
            {
                WriteBoolean(stream, true);
                WriteString(stream, indexName);
            }
            else
            {
                WriteBoolean(stream, false);
            }
        }

        public IndexResult[] DeserializeGetIndexes(MemoryStream stream,
            GetIndexesRequest request)
        {
            var count = ReadPackedInt32(stream);
            if (count < 0)
            {
                throw new BadProtocolException(
                    $"Received invalid index count: {count}");
            }

            var indexResults = new IndexResult[count];
            for (var i = 0; i < indexResults.Length; i++)
            {
                indexResults[i] = DeserializeIndexResult(stream);
            }

            return indexResults;
        }

        public void SerializeListTables(MemoryStream stream,
            ListTablesRequest request)
        {
            WriteOpcode(stream, Opcode.ListTables);
            SerializeRequest(stream, request);
            WriteUnpackedInt32(stream, request.Options?.FromIndex ?? 0);
            WriteUnpackedInt32(stream, request.Options?.Limit ?? 0);
            WriteString(stream, request.Options?.Namespace);
        }

        public ListTablesResult DeserializeListTables(MemoryStream stream,
            ListTablesRequest request)
        {
            var count = ReadPackedInt32(stream);
            if (count < 0)
            {
                throw new BadProtocolException(
                    $"Received invalid table count: {count}");
            }

            var tableNames = new string[count];
            for (var i = 0; i < tableNames.Length; i++)
            {
                tableNames[i] = ReadString(stream);
            }

            return new ListTablesResult()
            {
                TableNames = tableNames,
                NextIndex = ReadPackedInt32(stream)
            };
        }
    }
}
