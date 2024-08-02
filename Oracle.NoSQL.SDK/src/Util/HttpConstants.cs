/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

    internal static class HttpConstants
    {
        internal const string ContentType = "Content-Type";

        internal const string ContentTypeLowerCase = "content-type";

        internal const string ContentLength = "Content-Length";

        internal const string ContentLengthLowerCase = "content-length";

        internal const string ApplicationJson = "application/json";

        internal const string Authorization = "Authorization";

        internal const string Host = "host";

        internal const string Date = "date";

        internal const string RequestId = "x-nosql-request-id";

        internal const string DriverProtocolVersion = "x-nosql-serde-version";

        internal const string DataPathName = "data";

        internal const string NoSQLVersion = "V2";

        internal const string NoSQLPathName = "nosql";

        internal const string CompartmentId = "x-nosql-compartment-id";

        internal const string Namespace = "x-nosql-default-ns";

        internal const string RequestTarget = "(request-target)";

        internal const string ContentSHA256 = "x-content-sha256";

        internal const string OBOTokenHeader = "opc-obo-token";

        internal const string OPCRequestId = "opc-request-id";

        internal const string ServerSerialVersion = "x-nosql-serial-version";

        internal static readonly string NoSQLDataPath =
            $"{NoSQLVersion}/{NoSQLPathName}/{DataPathName}";
    }

}
