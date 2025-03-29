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
    using static Protocol;
    using Query;
    using Query.BinaryProtocol;
    using BinaryProtocol = BinaryProtocol.Protocol;
    using Opcode = BinaryProtocol.Opcode;
    using NsonType = DbType;

    internal partial class RequestSerializer
    {
        internal const int MathContextCustom =
            SDK.BinaryProtocol.RequestSerializer.MathContextCustom;
        internal const int DecimalPrecision =
            SDK.BinaryProtocol.RequestSerializer.DecimalPrecision;
        internal const int DecimalRounding =
            SDK.BinaryProtocol.RequestSerializer.DecimalRounding;

        // To keep TopologyInfo immutable and accomodate existing logic in
        // ProcessPreparedStatementField. This is only used by query V3 and
        // below.
        private class MutableTopologyInfo
        {
            internal int SequenceNumber { get; set; }
            internal IReadOnlyList<int> ShardIds { get; set; }

            internal TopologyInfo ToTopologyInfo() =>
                new TopologyInfo(SequenceNumber, ShardIds);
        }

        private static void DeserializeDriverPlanInfo(MemoryStream stream,
            PreparedStatement statement)
        {
            statement.DriverQueryPlan = PlanSerializer.DeserializeStep(
                stream);
            
            if (statement.DriverQueryPlan == null)
            {
                return;
            }

            // Number of iterators, not used
            stream.Seek(4, SeekOrigin.Current);
            statement.RegisterCount = BinaryProtocol.ReadUnpackedInt32(
                stream);

            var varCount = BinaryProtocol.ReadUnpackedInt32(stream);
            
            if (varCount <= 0)
            {
                return;
            }

            statement.VariableNames = new string[varCount];
            for (var i = 0; i < varCount; i++)
            {
                var name = BinaryProtocol.ReadString(stream);
                var position = BinaryProtocol.ReadUnpackedInt32(stream);
                if (position < 0 || position > varCount)
                {
                    throw new BadProtocolException(
                        "Query: parameter position out of range, " +
                        $"name: {name}, position: {position}, " +
                        $"parameter count: {varCount}");
                }

                statement.VariableNames[position] = name;
            }
        }

        // topology info is not always sent
        private static bool ProcessPreparedStatementField(NsonReader reader,
            string field, ref PreparedStatement statement,
            ref MutableTopologyInfo topologyInfo)
        {
            switch (field)
            {
                case FieldNames.PreparedQuery:
                    statement ??= new PreparedStatement();
                    statement.ProxyStatement = reader.ReadByteArray();
                    return true;
                case FieldNames.DriverQueryPlan:
                    statement ??= new PreparedStatement();
                    var stream = GetMemoryStreamWithVisibleBuffer(
                        reader.ReadByteArray());
                    DeserializeDriverPlanInfo(stream, statement);
                    return true;
                case FieldNames.TableName:
                    statement ??= new PreparedStatement();
                    statement.TableName = reader.ReadString();
                    return true;
                case FieldNames.Namespace:
                    statement ??= new PreparedStatement();
                    statement.Namespace = reader.ReadString();
                    return true;
                case FieldNames.QueryPlanString:
                    statement ??= new PreparedStatement();
                    statement.QueryPlan = reader.ReadString();
                    return true;
                case FieldNames.QueryResultSchema:
                    statement ??= new PreparedStatement();
                    statement.ResultSchema = reader.ReadString();
                    return true;
                case FieldNames.QueryOperation:
                    statement ??= new PreparedStatement();
                    statement.OperationCode = (sbyte)reader.ReadInt32();
                    return true;
                // These fields are for query V3 and below, for query V4 the
                // topology is read in Protocol.DeserializeResponse().
                case FieldNames.ProxyTopoSeqNum:
                    topologyInfo ??= new MutableTopologyInfo();
                    topologyInfo.SequenceNumber = reader.ReadInt32();
                    return true;
                case FieldNames.ShardIds:
                    topologyInfo ??= new MutableTopologyInfo();
                    topologyInfo.ShardIds = ReadArray(reader, reader.ReadInt32);
                    return true;
                default:
                    return false;
            }
        }

        // Validates the server portion of the prepared statement from the
        // protocol perspective. The client portion is validated by
        // PreparedStatement.Validate().
        private static void ValidatePreparedStatement(
            PreparedStatement preparedStatement)
        {
            if (preparedStatement == null)
            {
                throw new BadProtocolException(
                    "Missing prepared query information");
            }

            if (preparedStatement.ProxyStatement == null)
            {
                throw new BadProtocolException("Missing prepared statement");
            }

            // Anything else we need to check? Table name, namespace,
            // query opcode?
        }

        // Note that this MathContext reflects properties of C# decimal type.
        // However, Java driver uses DECIMAL32 as default.  Need to decide
        // whether to allow specifying different MathContext for the server
        // than is used on the client (C# decimal).
        private static void SerializeMathContext(NsonWriter writer)
        {
            writer.WriteInt32(FieldNames.MathContextPrecision,
                DecimalPrecision);
            writer.WriteInt32(FieldNames.MathContextRoundingMode,
                DecimalRounding);
            writer.WriteInt32(FieldNames.MathContextCode, MathContextCustom);
        }

        private static AllPartitionsSortPhase1 DeserializeSortPhase1Results(
            MemoryStream stream)
        {
            var sortPhase1 = new AllPartitionsSortPhase1
            {
                ToContinue = BinaryProtocol.ReadBoolean(stream),
                PartitionIds = BinaryProtocol.ReadPackedInt32Array(stream)
            };

            if (sortPhase1.PartitionIds != null)
            {
                sortPhase1.ResultCounts = BinaryProtocol.ReadPackedInt32Array(
                    stream);
                var partitionCount = sortPhase1.PartitionIds.Length;
                sortPhase1.ContinuationKeys = new byte[partitionCount][];
                for (var i = 0; i < partitionCount; i++)
                {
                    sortPhase1.ContinuationKeys[i] =
                        BinaryProtocol.ReadByteArray(stream);
                }
            }

            return sortPhase1;
        }

        private static void SerializeVirtualScan(NsonWriter writer,
            VirtualScan vs)
        {
            writer.StartMap(FieldNames.VirtualScan);
            writer.WriteInt32(FieldNames.VirtualScanSID, vs.ShardId);
            writer.WriteInt32(FieldNames.VirtualScanPID, vs.PartitionId);

            if (!vs.IsInfoSent)
            {
                writer.WriteByteArray(FieldNames.VirtualScanPrimKey,
                    vs.PrimaryKey);
                writer.WriteByteArray(FieldNames.VirtualScanSecKey,
                    vs.SecondaryKey);
                writer.WriteBoolean(FieldNames.VirtualScanMoveAfter,
                    vs.MoveAfterResumeKey);

                writer.WriteByteArray(FieldNames.VirtualScanJoinDescResumeKey,
                    vs.JoinDescendantResumeKey);

                if (vs.JoinPathTableIds != null)
                {
                    writer.WriteFieldName(
                        FieldNames.VirtualScanJoinPathTables);
                    WriteArray(writer, vs.JoinPathTableIds,
                        writer.WriteInt32);
                }
                
                writer.WriteByteArray(FieldNames.VirtualScanJoinPathKey,
                    vs.JoinPathPrimaryKey);
                writer.WriteByteArray(FieldNames.VirtualScanJoinPathSecKey,
                    vs.JoinPathSecondaryKey);
                writer.WriteBoolean(FieldNames.VirtualScanJoinPathMatched,
                    vs.JoinPathMatched);
            }

            writer.EndMap();
        }

        private static VirtualScan DeserializeVirtualScan(NsonReader reader)
        {
            var result = new VirtualScan();

            ReadMap(reader, field =>
            {
                switch (field)
                {
                    case FieldNames.VirtualScanSID:
                        result.ShardId = reader.ReadInt32();
                        return true;
                    case FieldNames.VirtualScanPID:
                        result.PartitionId = reader.ReadInt32();
                        return true;
                    case FieldNames.VirtualScanPrimKey:
                        result.PrimaryKey = reader.ReadByteArray();
                        return true;
                    case FieldNames.VirtualScanSecKey:
                        result.SecondaryKey = reader.ReadByteArray();
                        return true;
                    case FieldNames.VirtualScanMoveAfter:
                        result.MoveAfterResumeKey = reader.ReadBoolean();
                        return true;
                    case FieldNames.VirtualScanJoinDescResumeKey:
                        result.JoinDescendantResumeKey =
                            reader.ReadByteArray();
                        return true;
                    case FieldNames.VirtualScanJoinPathTables:
                        result.JoinPathTableIds =
                            ReadArray(reader, reader.ReadInt32);
                        return true;
                    case FieldNames.VirtualScanJoinPathKey:
                        result.JoinPathPrimaryKey = reader.ReadByteArray();
                        return true;
                    case FieldNames.VirtualScanJoinPathSecKey:
                        result.JoinPathSecondaryKey = reader.ReadByteArray();
                        return true;
                    case FieldNames.VirtualScanJoinPathMatched:
                        result.JoinPathMatched = reader.ReadBoolean();
                        return true;
                    default:
                        return false;
                }
            });

            return result;
        }

        static IReadOnlyList<QueryTraceRecord> DeserializeQueryTraces(
            NsonReader reader)
        {
            reader.ExpectType(NsonType.Array);
            var count = reader.Count;
            if (count % 2 != 0)
            {
                throw new BadProtocolException(
                    $"Received odd length of query traces array: {count}");
            }

            count /= 2;

            var result = new QueryTraceRecord[count];

            for (var i = 0; i < result.Length; i++)
            {
                ref var elem = ref result[i];
                reader.Next();
                elem.BatchName = reader.ReadString();
                reader.Next();
                elem.BatchTrace = reader.ReadString();
            }

            return result;
        }

        public void SerializePrepare(MemoryStream stream,
            PrepareRequest request)
        {
            var writer = GetNsonWriter(stream);
            writer.StartMap();
            WriteHeader(writer, Opcode.Prepare, request);
            writer.StartMap(FieldNames.Payload);
            writer.WriteInt32(FieldNames.QueryVersion, request.QueryVersion);
            writer.WriteString(FieldNames.Statement, request.Statement);
            writer.WriteBoolean(FieldNames.GetQueryPlan,
                request.GetQueryPlan);
            writer.WriteBoolean(FieldNames.GetQuerySchema,
                request.GetResultSchema);
            writer.EndMap();
            writer.EndMap();
        }

        public PreparedStatement DeserializePrepare(MemoryStream stream,
            PrepareRequest request)
        {
            var reader = GetNsonReader(stream);
            var statement = new PreparedStatement
            {
                SQLText = request.Statement
            };
            MutableTopologyInfo mti = null;

            DeserializeResponse(reader,
                field => ProcessPreparedStatementField(reader, field,
                    ref statement, ref mti), request, statement);
            ValidatePreparedStatement(statement);
            
            // Only for query <= V3.
            if (mti != null)
            {
                var topologyInfo = mti.ToTopologyInfo();
                ValidateTopologyInfo(topologyInfo);
                request.Client.SetQueryTopology(topologyInfo);
            }

            return statement;
        }

        public void SerializeQuery<TRow>(MemoryStream stream,
            QueryRequest<TRow> request)
        {
            var writer = GetNsonWriter(stream);
            writer.StartMap();
            WriteHeader(writer, Opcode.Query, request);
            writer.StartMap(FieldNames.Payload);

            WriteConsistency(writer, request.Consistency);
            
            if (request.Durability.HasValue)
            {
                WriteDurability(writer, request.Durability);
            }

            OptionallyWriteInt32(writer, FieldNames.MaxReadKB,
                request.Options?.MaxReadKB);
            OptionallyWriteInt32(writer, FieldNames.MaxWriteKB,
                request.Options?.MaxWriteKB);
            OptionallyWriteInt32(writer, FieldNames.NumberLimit,
                request.Options?.Limit);
            OptionallyWriteInt32(writer, FieldNames.TraceLevel,
                request.Options?.TraceLevel);
            OptionallyWriteInt64(writer, FieldNames.ServerMemoryConsumption,
                request.Options?.MaxServerMemory);

            if (request.Options?.TraceLevel.HasValue ?? false)
            {
                writer.WriteBoolean(FieldNames.TraceToLogFiles,
                    request.Options?.TraceToLogFiles ?? false);
                var batchNum = request.Options?.BatchNumber ?? 0;
                // Java driver is using 1-based counter.
                writer.WriteInt32(FieldNames.BatchCounter, batchNum + 1);
            }

            writer.WriteInt32(FieldNames.QueryVersion, request.QueryVersion);

            if (request.PreparedStatement != null)
            {
                writer.WriteBoolean(FieldNames.IsPrepared, true);
                writer.WriteBoolean(FieldNames.IsSimpleQuery,
                    request.PreparedStatement.IsSimpleQuery);
                writer.WriteByteArray(FieldNames.PreparedQuery,
                    request.PreparedStatement.ProxyStatement);

                var variables = request.PreparedStatement.variables;
                if (variables != null)
                {
                    writer.StartArray(FieldNames.BindVariables);
                    foreach (var kvp in variables)
                    {
                        writer.StartMap();
                        writer.WriteString(FieldNames.Name, kvp.Key);
                        WriteValue(writer, kvp.Value);
                        writer.EndMap();
                    }
                    writer.EndArray();
                }

                // For query <=V3 we write topology seqNo in the payload.
                if (request.QueryVersion < QueryRequestBase.QueryV4)
                {
                    var topoSeqNum = request.QueryTopologySequenceNumber;
                    if (topoSeqNum != -1)
                    {
                        writer.WriteInt32(FieldNames.TopoSeqNum, topoSeqNum);
                    }
                }
            }
            else
            {
                writer.WriteString(FieldNames.Statement, request.Statement);
            }

            if (request.ContinuationKey != null)
            {
                writer.WriteByteArray(FieldNames.ContinuationKey,
                    request.ContinuationKey.Bytes);
            }

            SerializeMathContext(writer);

            if (request.ShardId != -1)
            {
                writer.WriteInt32(FieldNames.ShardId, request.ShardId);
            }

            if (request.QueryVersion >= QueryRequestBase.QueryV4)
            {
                OptionallyWriteString(writer, FieldNames.QueryName,
                    request.Options?.QueryLabel);
                if (request.VirtualScan != null)
                {
                    SerializeVirtualScan(writer, request.VirtualScan);
                }
            }

            writer.EndMap();
            writer.EndMap();
        }

        public QueryResult<TRow> DeserializeQuery<TRow>(MemoryStream stream,
            QueryRequest<TRow> request)
        {
            var reader = GetNsonReader(stream);
            var result = new QueryResult<TRow>();
            PreparedStatement preparedStatement = null;
            MutableTopologyInfo mti = null;

            DeserializeResponse(reader, field =>
            {
                switch (field)
                {
                    case FieldNames.QueryResults:
                        result.Rows = ReadArray(reader,
                            () => ReadRow(reader).ToObject<TRow>());
                        return true;
                    case FieldNames.ContinuationKey:
                        var bytes = reader.ReadByteArray();
                        if (bytes != null)
                        {
                            result.ContinuationKey = new QueryContinuationKey(
                                bytes);
                        }
                        return true;
                    case FieldNames.SortPhase1Results:
                        result.SortPhase1 = DeserializeSortPhase1Results(
                            GetMemoryStreamWithVisibleBuffer(
                                reader.ReadByteArray()));
                        return true;
                    case FieldNames.ReachedLimit:
                        result.ReachedLimit = reader.ReadBoolean();
                        return true;
                    case FieldNames.VirtualScans:
                        result.VirtualScans = ReadArray(reader,
                            DeserializeVirtualScan);
                        return true;
                    case FieldNames.QueryBatchTraces:
                        result.QueryTraces = DeserializeQueryTraces(reader);
                        return true;
                    default:
                        return ProcessPreparedStatementField(reader, field,
                            ref preparedStatement, ref mti);
                }
            }, request, result);

            /*
             * If the QueryRequest was not initially prepared, the prepared
             * statement created at the proxy is returned back along with the
             * query results, so that the preparation does not need to be done
             * during each query batch.
             */
            if (request.PreparedStatement == null)
            {
                ValidatePreparedStatement(preparedStatement);
                preparedStatement.SQLText = request.Statement;
                preparedStatement.ConsumedCapacity = result.ConsumedCapacity;
                result.PreparedStatement = preparedStatement;
            }

            if (mti != null)
            {
                // We received updated topology info. This can happen here
                // only for query V3 and below.
                var topologyInfo = mti.ToTopologyInfo();
                ValidateTopologyInfo(topologyInfo);
                request.Client.SetQueryTopology(topologyInfo);
            }

            return result;
        }
    }
}
