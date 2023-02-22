/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.NsonProtocol
{
    using System.Diagnostics;
    using System.IO;
    using static Protocol;
    using BinaryProtocol = BinaryProtocol.Protocol;
    using Opcode = BinaryProtocol.Opcode;

    internal partial class RequestSerializer
    {
        private static Opcode GetPutOpcode(IPutOp op) =>
            SDK.BinaryProtocol.RequestSerializer.GetPutOpcode(op);

        private static Opcode GetDeleteOpcode(IDeleteOp op) =>
            SDK.BinaryProtocol.RequestSerializer.GetDeleteOpcode(op);

        private static void SerializePutOp(NsonWriter writer, IPutOp op)
        {
            if (op.Options?.ExactMatch ?? false)
            {
                writer.WriteBoolean(FieldNames.ExactMatch, true);
            }

            if (op.Options?.UpdateTTL ?? false)
            {
                writer.WriteBoolean(FieldNames.UpdateTTL, true);
            }

            if (op.Options?.TTL.HasValue ?? false)
            {
                writer.WriteString(FieldNames.TTL,
                    TTLToString(op.Options.TTL.Value));
            }

            if (op.Options?.IdentityCacheSize.HasValue ?? false)
            {
                writer.WriteInt32(FieldNames.IdentityCacheSize,
                    op.Options.IdentityCacheSize.Value);
            }

            if (op.MatchVersion != null)
            {
                writer.WriteByteArray(FieldNames.RowVersion,
                    op.MatchVersion.Bytes);
            }

            WriteValue(writer, MapValue.FromObject(op.Row));
        }

        private static void SerializeDeleteOp(NsonWriter writer, IDeleteOp op)
        {
            if (op.MatchVersion != null)
            {
                writer.WriteByteArray(FieldNames.RowVersion,
                    op.MatchVersion.Bytes);
            }

            WriteKey(writer, MapValue.FromObject(op.PrimaryKey));
        }

        private static WriteOperationResult<TRow>
            DeserializeWriteOperationResult<TRow>(NsonReader reader)
        {
            var result = new WriteOperationResult<TRow>();
            ReadMap(reader, field =>
            {
                switch (field)
                {
                    case FieldNames.Success:
                        result.Success = reader.ReadBoolean();
                        return true;
                    case FieldNames.RowVersion:
                        result.Version = ReadRowVersion(reader);
                        return true;
                    case FieldNames.Generated:
                        result.GeneratedValue = ReadFieldValue(reader);
                        return true;
                    case FieldNames.ReturnInfo:
                        DeserializeReturnInfo(reader, result);
                        return true;
                    default:
                        return false;
                }
            });

            return result;
        }

        public void SerializeGet<TRow>(MemoryStream stream,
            GetRequest<TRow> request)
        {
            var writer = GetNsonWriter(stream);
            writer.StartMap();
            WriteHeader(writer, Opcode.Get, request);
            writer.StartMap(FieldNames.Payload);
            WriteConsistency(writer, request.Consistency);
            WriteKey(writer, MapValue.FromObject(request.PrimaryKey));
            writer.EndMap();
            writer.EndMap();
        }

        public GetResult<TRow> DeserializeGet<TRow>(MemoryStream stream,
            GetRequest<TRow> request)
        {
            var reader = GetNsonReader(stream);
            var result = new GetResult<TRow>();
            DeserializeResponse(reader, field =>
            {
                if (field != FieldNames.Row)
                {
                    return false;
                }

                ReadMap(reader, rowField =>
                {
                    switch (rowField)
                    {
                        case FieldNames.Value:
                            result.Row = ReadRow(reader).ToObject<TRow>();
                            return true;
                        case FieldNames.RowVersion:
                            result.Version = ReadRowVersion(reader);
                            return true;
                        case FieldNames.ExpirationTime:
                            result.ExpirationTime =
                                ReadOptionalTimestamp(reader);
                            return true;
                        case FieldNames.ModificationTime:
                            result.ModificationTime =
                                ReadOptionalTimestamp(reader);
                            return true;
                        default:
                            return false;
                    }
                });
                return true;
            }, request, result);

            return result;
        }

        public void SerializePut<TRow>(MemoryStream stream,
            PutRequest<TRow> request)
        {
            var writer = GetNsonWriter(stream);
            writer.StartMap();
            WriteHeader(writer, GetPutOpcode(request), request);
            writer.StartMap(FieldNames.Payload);
            SerializeWriteRequest(writer, request);
            SerializePutOp(writer, request);
            writer.EndMap();
            writer.EndMap();
        }

        public PutResult<TRow> DeserializePut<TRow>(MemoryStream stream,
            PutRequest<TRow> request)
        {
            var reader = GetNsonReader(stream);
            var result = new PutResult<TRow>();
            DeserializeResponse(reader, field =>
            {
                switch (field)
                {
                    case FieldNames.RowVersion:
                        result.Version = ReadRowVersion(reader);
                        result.Success = true;
                        return true;
                    case FieldNames.ReturnInfo:
                        DeserializeReturnInfo(reader, result);
                        return true;
                    case FieldNames.Generated:
                        result.GeneratedValue = ReadFieldValue(reader);
                        return true;
                    default:
                        return false;
                }
            }, request, result);

            return result;
        }

        public void SerializeDelete<TRow>(MemoryStream stream,
            DeleteRequest<TRow> request)
        {
            var writer = GetNsonWriter(stream);
            writer.StartMap();
            WriteHeader(writer, GetDeleteOpcode(request), request);
            writer.StartMap(FieldNames.Payload);
            SerializeWriteRequest(writer, request);
            SerializeDeleteOp(writer, request);
            writer.EndMap();
            writer.EndMap();
        }

        public DeleteResult<TRow> DeserializeDelete<TRow>(MemoryStream stream,
            DeleteRequest<TRow> request)
        {
            var reader = GetNsonReader(stream);
            var result = new DeleteResult<TRow>();
            DeserializeResponse(reader, field =>
            {
                switch (field)
                {
                    case FieldNames.Success:
                        result.Success = reader.ReadBoolean();
                        return true;
                    case FieldNames.ReturnInfo:
                        DeserializeReturnInfo(reader, result);
                        return true;
                    default:
                        return false;
                }
            }, request, result);

            return result;
        }

        public void SerializeDeleteRange(MemoryStream stream,
            DeleteRangeRequest request)
        {
            var writer = GetNsonWriter(stream);
            writer.StartMap();
            WriteHeader(writer, Opcode.MultiDelete, request);
            writer.StartMap(FieldNames.Payload);
            WriteDurability(writer, request.Durability);

            if (request.Options?.MaxWriteKB.HasValue ?? false)
            {
                writer.WriteInt32(FieldNames.MaxWriteKB,
                    request.Options.MaxWriteKB.Value);
            }

            if (request.Options?.ContinuationKey != null)
            {
                writer.WriteByteArray(FieldNames.ContinuationKey,
                    request.Options.ContinuationKey.Bytes);
            }

            if (request.FieldRange != null)
            {
                WriteFieldRange(writer, request.FieldRange);
            }

            WriteKey(writer, MapValue.FromObject(request.PartialPrimaryKey));
            writer.EndMap();
            writer.EndMap();
        }

        public DeleteRangeResult DeserializeDeleteRange(MemoryStream stream,
            DeleteRangeRequest request)
        {
            var result = new DeleteRangeResult();
            var reader = GetNsonReader(stream);
            DeserializeResponse(reader, field =>
            {
                switch (field)
                {
                    case FieldNames.NumDeletions:
                        result.DeletedCount = reader.ReadInt32();
                        return true;
                    case FieldNames.ContinuationKey:
                        result.ContinuationKey =
                            new DeleteRangeContinuationKey(
                                reader.ReadByteArray());
                        return true;
                    default:
                        return false;
                }
            }, request, result);

            return result;
        }

        public void SerializeWriteMany(MemoryStream stream,
            WriteManyRequest request)
        {
            var writer = GetNsonWriter(stream);
            writer.StartMap();
            // request.TableName is set for single-table requests but not for
            // multi-table requests.
            WriteHeader(writer, Opcode.WriteMultiple, request,
                request.TableName);
            writer.StartMap(FieldNames.Payload);
            WriteDurability(writer, request.Durability);
            writer.WriteInt32(FieldNames.NumOperations,
                request.Operations.Count);

            writer.StartArray(FieldNames.Operations);

            foreach (var op in request.Operations)
            {
                var start = stream.Position;
                writer.StartMap();
                if (op.TableName != null)
                {
                    writer.WriteString(FieldNames.TableName, op.TableName);
                }

                if (op is IPutOp putOp)
                {
                    writer.WriteInt32(FieldNames.Opcode,
                        (int)GetPutOpcode(putOp));
                    SerializePutOp(writer, putOp);
                    writer.WriteBoolean(FieldNames.ReturnRow,
                        putOp.ReturnExisting);
                }
                else
                {
                    Debug.Assert(op is IDeleteOp,
                        $"Invalid operation type: {op.GetType().Name}");
                    var deleteOp = (IDeleteOp)op;
                    writer.WriteInt32(FieldNames.Opcode,
                        (int)GetDeleteOpcode(deleteOp));
                    SerializeDeleteOp(writer, deleteOp);
                    writer.WriteBoolean(FieldNames.ReturnRow,
                        deleteOp.ReturnExisting);
                }

                writer.WriteBoolean(FieldNames.AbortOnFail,
                    op.AbortIfUnsuccessful || request.AbortIfUnsuccessful);
                writer.EndMap();
                
                if (stream.Position - start > BinaryProtocol.RequestSizeLimit)
                {
                    throw new RequestSizeLimitException();
                }
            }

            writer.EndArray();
            writer.EndMap();
            writer.EndMap();
        }

        public WriteManyResult<TRow> DeserializeWriteMany<TRow>(
            MemoryStream stream, WriteManyRequest request)
        {
            var result = new WriteManyResult<TRow>();
            var reader = GetNsonReader(stream);
            DeserializeResponse(reader, field =>
            {
                switch (field)
                {
                    case FieldNames.WmSuccess:
                        result.Results = ReadArray(reader,
                            DeserializeWriteOperationResult<TRow>);
                        return true;
                    case FieldNames.WmFailure:
                        ReadMap(reader, failField =>
                        {
                            switch (failField)
                            {
                                case FieldNames.WmFailIndex:
                                    result.FailedOperationIndex =
                                        reader.ReadInt32();
                                    return true;
                                case FieldNames.WmFailResult:
                                    result.FailedOperationResult =
                                        DeserializeWriteOperationResult<TRow>(
                                            reader);
                                    return true;
                                default:
                                    return false;
                            }
                        });
                        return true;
                    default:
                        return false;
                }
            }, request, result);

            return result;
        }
    }

}
