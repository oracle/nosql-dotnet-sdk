/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.BinaryProtocol
{
    using System.IO;
    using static Protocol;
    using Query;
    using Query.BinaryProtocol;

    internal partial class RequestSerializer
    {

        private const int MathContextCustom = 5;
        private const int DecimalPrecision = 29;
        private const int DecimalRounding = 6; // Half-even

        private static void DeserializePreparedStatement(MemoryStream stream,
            bool getQueryPlan, PreparedStatement statement)
        {
            statement.ProxyStatement = ReadByteArrayWithUnpackedLength(
                stream);
            if (getQueryPlan)
            {
                statement.QueryPlan = ReadString(stream);
            }

            statement.DriverQueryPlan = PlanSerializer.DeserializeStep(
                stream);
            if (statement.DriverQueryPlan != null)
            {
                // Number of iterators, not used
                stream.Seek(4, SeekOrigin.Current);
                statement.RegisterCount = ReadUnpackedInt32(stream);
                var paramCount = ReadUnpackedInt32(stream);
                if (paramCount > 0)
                {
                    statement.VariableNames = new string[paramCount];
                    for (var i = 0; i < paramCount; i++)
                    {
                        var name = ReadString(stream);
                        var position = ReadUnpackedInt32(stream);
                        if (position < 0 || position > paramCount)
                        {
                            throw new BadProtocolException(
                                "Query: parameter position out of range, " +
                                $"name: {name}, position: {position}, " +
                                $"parameter count: {paramCount}");
                        }

                        statement.VariableNames[position] = name;
                    }
                }

                statement.SetTopologyInfo(ReadTopologyInfo(stream));
            }
        }

        private static void SerializeMathContext(MemoryStream stream)
        {
            WriteByte(stream, MathContextCustom);
            WriteUnpackedInt32(stream, DecimalPrecision);
            WriteUnpackedInt32(stream, DecimalRounding);
        }

        public void SerializePrepare(MemoryStream stream,
            PrepareRequest request)
        {
            WriteOpcode(stream, Opcode.Prepare);
            SerializeRequest(stream, request);
            WriteString(stream, request.Statement);
            WriteUnpackedInt16(stream, QueryRuntime.QueryVersion);
            WriteBoolean(stream, request.GetQueryPlan);
        }

        public PreparedStatement DeserializePrepare(MemoryStream stream,
            PrepareRequest request)
        {
            var statement = new PreparedStatement
            {
                SQLText = request.Statement
            };
            DeserializeConsumedCapacity(stream, request, statement);
            DeserializePreparedStatement(stream, request.GetQueryPlan,
                statement);
            return statement;
        }

        public void SerializeQuery<TRow>(MemoryStream stream,
            QueryRequest<TRow> request)
        {
            WriteOpcode(stream, Opcode.Query);
            SerializeRequest(stream, request);
            WriteConsistency(stream, request.Consistency);
            WritePackedInt32(stream, request.Options?.Limit ?? 0);
            WritePackedInt32(stream, request.Options?.MaxReadKB ?? 0);
            WriteByteArray(stream, request.Options?.ContinuationKey?.Bytes);
            WriteBoolean(stream, request.PreparedStatement != null);

            // The following 7 fields were added in V2
            WriteUnpackedInt16(stream, QueryRuntime.QueryVersion);
            WritePackedInt32(stream, request.Options?.TraceLevel ?? 0);
            WritePackedInt32(stream, request.Options?.MaxWriteKB ?? 0);
            SerializeMathContext(stream);
            WritePackedInt32(stream,
                request.PreparedStatement?.TopologyInfo?.SequenceNumber ??
                -1);
            WritePackedInt32(stream, request.ShardId);
            WriteBoolean(stream, // Whether it is a prepared simple query
                request.PreparedStatement != null &&
                request.PreparedStatement.DriverQueryPlan == null);

            if (request.PreparedStatement != null)
            {
                WriteByteArrayWithUnpackedLength(stream,
                    request.PreparedStatement.ProxyStatement);
                var variables = request.PreparedStatement.variables;
                if (variables != null)
                {
                    WritePackedInt32(stream, variables.Count);
                    foreach (var kvp in variables)
                    {
                        WriteString(stream, kvp.Key);
                        WriteFieldValue(stream, kvp.Value);
                    }
                }
                else
                {
                    WritePackedInt32(stream, 0);
                }
            }
            else
            {
                WriteString(stream, request.Statement);
            }
        }

        public QueryResult<TRow> DeserializeQuery<TRow>(MemoryStream stream,
            QueryRequest<TRow> request)
        {
            var result = new QueryResult<TRow>();
            var records  = new TRow[ReadUnpackedInt32(stream)];
            var isAllPartSortPhase1 = ReadBoolean(stream);

            for (var i = 0; i < records.Length; i++)
            {
                records[i] = ReadRow(stream).ToObject<TRow>();
            }

            result.Rows = records;

            if (isAllPartSortPhase1)
            {
                result.SortPhase1 = new AllPartitionsSortPhase1
                {
                    ToContinue = ReadBoolean(stream),
                    PartitionIds = ReadPackedInt32Array(stream)
                };
                if (result.SortPhase1.PartitionIds != null)
                {
                    result.SortPhase1.ResultCounts = ReadPackedInt32Array(
                        stream);
                    var partitionCount =
                        result.SortPhase1.PartitionIds.Length;
                    result.SortPhase1.ContinuationKeys =
                        new byte[partitionCount][];
                    for (var i = 0; i < partitionCount; i++)
                    {
                        result.SortPhase1.ContinuationKeys[i] = ReadByteArray(
                            stream);
                    }
                }
            }

            DeserializeConsumedCapacity(stream, request, result);

            var continuationKeyBytes = ReadByteArray(stream);
            if (continuationKeyBytes != null)
            {
                result.ContinuationKey = new QueryContinuationKey(
                    continuationKeyBytes);
            }

            /*
             * In V2, if the QueryRequest was not initially prepared, the prepared
             * statement created at the proxy is returned back along with the
             * query results, so that the preparation does not need to be done
             * during each query batch.  For advanced queries, only prepared
             * statement will be returned and the query will start executing
             * on the next invocation of NoSQLClient#query() method.
             */
            if (request.PreparedStatement == null)
            {
                result.PreparedStatement = new PreparedStatement
                {
                    SQLText = request.Statement,
                    ConsumedCapacity = result.ConsumedCapacity
                };
                DeserializePreparedStatement(stream, false, result.PreparedStatement);
            }

            if (request.IsInternal)
            {
                result.ReachedLimit = ReadBoolean(stream);
                result.TopologyInfo = ReadTopologyInfo(stream);
            }

            return result;
        }
    }
}
