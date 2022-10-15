/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.BinaryProtocol
{
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using static Protocol;

    internal partial class RequestSerializer
    {
        private static Opcode GetPutOpcode(IPutOp op)
        {
            return Opcode.Put + (int)op.PutOpKind;
        }

        private static Opcode GetDeleteOpcode(IDeleteOp op)
        {
            return op.MatchVersion == null ?
                Opcode.Delete : Opcode.DeleteIfVersion;
        }

        private static void SerializePutOp(MemoryStream stream, IPutOp op)
        {
            WriteBoolean(stream, op.Options?.ExactMatch ?? false);
            WritePackedInt32(stream, op.Options?.IdentityCacheSize ?? 0);
            WriteFieldValue(stream, MapValue.FromObject(op.Row));
            WriteBoolean(stream, op.Options?.UpdateTTL ?? false);
            WriteTTL(stream, op.Options?.TTL);
            if (op.MatchVersion != null)
            {
                WriteVersion(stream, op.MatchVersion);
            }
        }

        private static void SerializeDeleteOp(MemoryStream stream, IDeleteOp op)
        {
            WriteFieldValue(stream, MapValue.FromObject(op.PrimaryKey));
            if (op.MatchVersion != null)
            {
                WriteVersion(stream, op.MatchVersion);
            }
        }

        private static string GetOperationTableNames(WriteOperationCollection woc)
        {
            Debug.Assert(woc.Count != 0);

            StringBuilder result = new StringBuilder();
            var i = 0;
            foreach(IWriteOperation op in woc)
            {
                Debug.Assert(op.TableName != null);
                result.Append(op.TableName);
                if (++i < woc.Count)
                {
                    result.Append(',');
                }
            }

            // table names cannot be empty
            Debug.Assert(result.Length != 0);
            return result.ToString();
        }

        private WriteOperationResult<TRow>
            DeserializeWriteOperationResult<TRow>(
            MemoryStream stream)
        {
            var result = new WriteOperationResult<TRow>
            {
                Success = ReadBoolean(stream)
            };
            if (ReadBoolean(stream))
            {
                result.Version = ReadRecordVersion(stream);
            }

            DeserializeWriteResponseWithId(stream, serialVersion, result);
            return result;
        }

        public void SerializeGet<TRow>(MemoryStream stream,
            GetRequest<TRow> request)
        {
            WriteOpcode(stream, Opcode.Get);
            SerializeReadRequest(stream, request);
            WriteFieldValue(stream, MapValue.FromObject(request.PrimaryKey));
        }

        public GetResult<TRow> DeserializeGet<TRow>(MemoryStream stream,
            GetRequest<TRow> request)
        {
            var result = new GetResult<TRow>();
            DeserializeConsumedCapacity(stream, request, result);

            var hasRow = ReadBoolean(stream);
            if (hasRow)
            {
                result.Row = ReadRow(stream).ToObject<TRow>();

                long millis;
                if ((millis = ReadPackedInt64(stream)) != 0)
                {
                    result.ExpirationTime =
                        DateTimeUtils.UnixMillisToDateTime(millis);
                }

                result.Version = ReadRecordVersion(stream);

                if (serialVersion > V2 &&
                    (millis = ReadPackedInt64(stream)) != 0)
                {
                    result.ModificationTime =
                        DateTimeUtils.UnixMillisToDateTime(millis);
                }
            }

            return result;
        }

        public void SerializePut<TRow>(MemoryStream stream,
            PutRequest<TRow> request)
        {
            WriteOpcode(stream, GetPutOpcode(request));
            SerializeWriteRequest(stream, request, serialVersion);
            SerializePutOp(stream, request);
        }

        public PutResult<TRow> DeserializePut<TRow>(MemoryStream stream,
            PutRequest<TRow> request)
        {
            var result = new PutResult<TRow>();
            DeserializeConsumedCapacity(stream, request, result);
            result.Success = ReadBoolean(stream);
            if (result.Success)
            {
                result.Version = ReadRecordVersion(stream);
            }

            DeserializeWriteResponseWithId(stream, serialVersion, result);
            return result;
        }

        public void SerializeDelete<TRow>(MemoryStream stream,
            DeleteRequest<TRow> request)
        {
            WriteOpcode(stream, GetDeleteOpcode(request));
            SerializeWriteRequest(stream, request, serialVersion);
            SerializeDeleteOp(stream, request);
        }

        public DeleteResult<TRow> DeserializeDelete<TRow>(MemoryStream stream,
            DeleteRequest<TRow> request)
        {
            var result = new DeleteResult<TRow>();
            DeserializeConsumedCapacity(stream, request, result);
            result.Success = ReadBoolean(stream);

            DeserializeWriteResponse(stream, serialVersion, result);
            return result;
        }

        public void SerializeDeleteRange(MemoryStream stream,
            DeleteRangeRequest request)
        {
            WriteOpcode(stream, Opcode.MultiDelete);
            SerializeRequest(stream, request);
            WriteString(stream, request.TableName);
            WriteDurability(stream, request.Durability, serialVersion);
            WriteFieldValue(stream, MapValue.FromObject(
                request.PartialPrimaryKey));
            WriteFieldRange(stream, request.FieldRange);
            WritePackedInt32(stream, request.Options?.MaxWriteKB ?? 0);
            WriteByteArray(stream, request.Options?.ContinuationKey?.Bytes);
        }

        public DeleteRangeResult DeserializeDeleteRange(MemoryStream stream,
            DeleteRangeRequest request)
        {
            var result = new DeleteRangeResult();
            DeserializeConsumedCapacity(stream, request, result);
            result.DeletedCount = ReadPackedInt32(stream);
            var bytes = ReadByteArray(stream);
            if (bytes != null)
            {
                result.ContinuationKey = new DeleteRangeContinuationKey(
                    bytes);
            }

            return result;
        }

        public void SerializeWriteMany(MemoryStream stream,
            WriteManyRequest request)
        {
            WriteOpcode(stream, Opcode.WriteMultiple);
            SerializeRequest(stream, request);
            WriteString(stream,
                request.IsSingleTable
                    ? request.TableName
                    : GetOperationTableNames(request.Operations));
            WritePackedInt32(stream, request.Operations.Count);
            WriteDurability(stream, request.Durability, serialVersion);

            foreach (var op in request.Operations)
            {
                var start = stream.Position;
                WriteBoolean(stream,
                    op.AbortIfUnsuccessful || request.AbortIfUnsuccessful);
                if (op is IPutOp putOp)
                {
                    WriteSubOpcode(stream, GetPutOpcode(putOp));
                    WriteBoolean(stream, putOp.ReturnExisting);
                    SerializePutOp(stream, putOp);
                }
                else
                {
                    Debug.Assert(op is IDeleteOp,
                        $"Invalid operation type: {op.GetType().Name}");
                    var deleteOp = (IDeleteOp)op;
                    WriteSubOpcode(stream, GetDeleteOpcode(deleteOp));
                    WriteBoolean(stream, deleteOp.ReturnExisting);
                    SerializeDeleteOp(stream, deleteOp);
                }

                if (stream.Position - start > RequestSizeLimit)
                {
                    throw new RequestSizeLimitException();
                }
            }
        }

        public WriteManyResult<TRow> DeserializeWriteMany<TRow>(
            MemoryStream stream, WriteManyRequest request)
        {
            var result = new WriteManyResult<TRow>();
            var succeeded = ReadBoolean(stream);
            DeserializeConsumedCapacity(stream, request, result);
            if (succeeded)
            {
                result.Results = ReadArray(stream,
                    DeserializeWriteOperationResult<TRow>);
            }
            else
            {
                result.FailedOperationIndex = ReadByte(stream);
                result.FailedOperationResult =
                    DeserializeWriteOperationResult<TRow>(stream);
            }

            return result;
        }
    }

}
