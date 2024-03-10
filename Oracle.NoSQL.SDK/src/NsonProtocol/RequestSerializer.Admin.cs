/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.NsonProtocol
{
    using System;
    using System.IO;
    using System.Text;
    using static Protocol;
    using Opcode = BinaryProtocol.Opcode;
    using NsonType = DbType;
    using static ValidateUtils;

    internal partial class RequestSerializer
    {
        private static AdminResult DeserializeAdminResult(NsonReader reader,
            Request request, AdminResult result)
        {
            DeserializeResponse(reader, field =>
            {
                switch (field)
                {
                    case FieldNames.SysopState:
                        result.State = (AdminState)reader.ReadInt32();
                        CheckReceivedEnumValue(result.State);
                        return true;
                    case FieldNames.SysopResult:
                        result.Output = reader.ReadString();
                        return true;
                    case FieldNames.Statement:
                        result.Statement = reader.ReadString();
                        return true;
                    case FieldNames.OperationId:
                        result.OperationId = reader.ReadString();
                        return true;
                    default:
                        return false;
                }
            }, request, result);

            return result;
        }

        public void SerializeAdmin(MemoryStream stream,
            AdminRequest request)
        {
            var writer = GetNsonWriter(stream);
            writer.StartMap();
            WriteHeader(writer, Opcode.SystemRequest, request);
            writer.StartMap(FieldNames.Payload);
            var statementBytes = Encoding.UTF8.GetBytes(request.Statement);

            try
            {
                writer.WriteByteArray(FieldNames.Statement, statementBytes);
            }
            finally
            {
                Array.Clear(statementBytes, 0, statementBytes.Length);
            }

            writer.EndMap();
            writer.EndMap();
        }

        public AdminResult DeserializeAdmin(MemoryStream stream,
            AdminRequest request)
        {
            return DeserializeAdminResult(GetNsonReader(stream), request,
                new AdminResult(request.Client));
        }

        public void SerializeGetAdminStatus(MemoryStream stream,
            AdminStatusRequest request)
        {
            var writer = GetNsonWriter(stream);
            writer.StartMap();
            WriteHeader(writer, Opcode.SystemStatusRequest, request);
            writer.StartMap(FieldNames.Payload);
            writer.WriteString(FieldNames.OperationId,
                request.AdminResult.OperationId);
            writer.WriteString(FieldNames.Statement,
                request.AdminResult.Statement);
            writer.EndMap();
            writer.EndMap();
        }

        public AdminResult DeserializeGetAdminStatus(MemoryStream stream,
            AdminStatusRequest request)
        {
            return DeserializeAdminResult(GetNsonReader(stream), request,
                new AdminResult(request.Client));
        }
    }

}
