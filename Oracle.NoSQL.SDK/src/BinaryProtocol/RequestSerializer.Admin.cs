/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.BinaryProtocol
{
    using System;
    using System.IO;
    using System.Text;
    using static Protocol;

    internal partial class RequestSerializer
    {
        public void SerializeAdmin(MemoryStream stream,
            AdminRequest request)
        {
            WriteOpcode(stream, Opcode.SystemRequest);
            SerializeRequest(stream, request);
            var statementBytes = Encoding.UTF8.GetBytes(request.Statement);
            try
            {
                WriteByteArray(stream, statementBytes);
            }
            finally
            {
                Array.Clear(statementBytes, 0, statementBytes.Length);
            }
        }

        public AdminResult DeserializeAdmin(MemoryStream stream,
            AdminRequest request)
        {
            return DeserializeAdminResult(stream, new AdminResult(
                request.Client));
        }

        public void SerializeGetAdminStatus(MemoryStream stream,
            AdminStatusRequest request)
        {
            WriteOpcode(stream, Opcode.SystemStatusRequest);
            SerializeRequest(stream, request);
            WriteString(stream, request.AdminResult.OperationId);
            WriteString(stream, request.AdminResult.Statement);
        }

        public AdminResult DeserializeGetAdminStatus(MemoryStream stream,
            AdminStatusRequest request)
        {
            return DeserializeAdminResult(stream, new AdminResult(
                request.Client));
        }
    }

}
