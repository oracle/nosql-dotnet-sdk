/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.BinaryProtocol
{
    using System;
    using System.IO;
    using static Protocol;

    internal partial class RequestSerializer : IRequestSerializer
    {
        private static long GetUnixMillisOrZero(DateTime? dateTime)
        {
            return dateTime.HasValue ? DateTimeUtils.GetUnixMillis(
                dateTime.Value) : 0;
        }

        public void ReadAndCheckError(MemoryStream stream)
        {
            var statusCode = ReadByte(stream);
            if (statusCode != 0)
            {
                var message = ReadString(stream);
                throw MapException((ErrorCode)statusCode, message);
            }
        }

        public string ContentType => "application/octet-stream";

    }

}
