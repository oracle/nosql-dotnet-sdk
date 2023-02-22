/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

using System.Xml.Linq;

namespace Oracle.NoSQL.SDK.NsonProtocol
{
    using System.IO;
    using System.Net.Sockets;
    using static Protocol;
    using Query;
    using Query.BinaryProtocol;
    using BinaryProtocol = BinaryProtocol.Protocol;
    using Opcode = BinaryProtocol.Opcode;
    internal partial class RequestSerializer
    {
        internal const int MathContextCustom =
            SDK.BinaryProtocol.RequestSerializer.MathContextCustom;
        internal const int DecimalPrecision =
            SDK.BinaryProtocol.RequestSerializer.DecimalPrecision;
        internal const int DecimalRounding =
            SDK.BinaryProtocol.RequestSerializer.DecimalRounding;

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
            ref TopologyInfo topologyInfo)
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
                case FieldNames.ProxyTopoSeqNum:
                    topologyInfo ??= new TopologyInfo();
                    topologyInfo.SequenceNumber = reader.ReadInt32();
                    return true;
                case FieldNames.ShardIds:
                    topologyInfo ??= new TopologyInfo();
                    topologyInfo.ShardIds = ReadArray(reader, reader.ReadInt32);
                    return true;
                default:
                    return false;
            }
        }

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

        public void SerializePrepare(MemoryStream stream,
            PrepareRequest request)
        {
            var writer = GetNsonWriter(stream);
            writer.StartMap();
            WriteHeader(writer, Opcode.Prepare, request);
            writer.StartMap(FieldNames.Payload);
            writer.WriteInt32(FieldNames.QueryVersion,
                QueryRuntime.QueryVersion);
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
            TopologyInfo topologyInfo = null;

            DeserializeResponse(reader,
                field => ProcessPreparedStatementField(reader, field,
                    ref statement, ref topologyInfo), request, statement);
            ValidatePreparedStatement(statement);
            
            if (topologyInfo != null)
            {
                statement.SetTopologyInfo(topologyInfo);
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
            
            writer.WriteInt32(FieldNames.QueryVersion,
                QueryRuntime.QueryVersion);

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

            var topoInfo = request.PreparedStatement?.TopologyInfo;
            if (topoInfo != null)
            {
                writer.WriteInt32(FieldNames.TopoSeqNum, topoInfo.SequenceNumber);
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
            TopologyInfo topologyInfo = null;

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
                    default:
                        return ProcessPreparedStatementField(reader, field,
                            ref preparedStatement, ref topologyInfo);
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

            result.TopologyInfo = topologyInfo;
            return result;
        }
    }
}
