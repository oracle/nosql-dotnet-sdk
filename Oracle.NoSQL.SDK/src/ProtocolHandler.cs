/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.IO;
    using System.Runtime.CompilerServices;

    internal class ProtocolHandler
    {
        private readonly object lockObj = new object();
        private volatile IRequestSerializer serializer;
        private volatile short queryVersion =
            QueryRequestBase.DefaultQueryVersion;

        internal ProtocolHandler()
        {
            serializer = new NsonProtocol.RequestSerializer();
        }

        internal IRequestSerializer Serializer => serializer;

        internal string ContentType => Serializer.ContentType;

        internal void StartWrite(MemoryStream stream, Request request) =>
            Serializer.StartWrite(stream, request);

        internal void StartRead(MemoryStream stream, Request request) =>
            Serializer.StartRead(stream, request);

        internal short SerialVersion => Serializer.SerialVersion;

        internal short QueryVersion => queryVersion;

        // Earlier serializers will usually support lower query version. This
        // method allows us to avoid extra HTTP request for decrementing query
        // version.
        private void CheckSetSerializerQueryVersion()
        {
            var qVer = serializer.MaxQueryVersion;

            if (queryVersion > qVer)
            {
                queryVersion = qVer;
            }
        }

        // Currently two protocols are supported: Nson and binary.  If server
        // doesn't support Nson protocol, we switch to binary.
        internal bool DecrementSerialVersion(short versionUsed)
        {
            lock (lockObj)
            {
                // The purpose of checking versionUsed here is to avoid a race
                // condition where DecrementSerialVersion() gets called
                // called concurrently by two threads and thus decrements
                // the serial version twice without trying request with the
                // intermediate version.
                if (SerialVersion != versionUsed)
                {
                    return true;
                }

                if (serializer.DecrementSerialVersion(versionUsed))
                {
                    return true;
                }

                if (serializer is NsonProtocol.RequestSerializer)
                {
                    serializer = new BinaryProtocol.RequestSerializer();
                    CheckSetSerializerQueryVersion();
                    return true;
                }

                return false;
            }
        }

        internal bool DecrementQueryVersion(short versionUsed)
        {
            lock (lockObj)
            {
                // Same as in DecrementSerialVersion above.
                if (queryVersion != versionUsed)
                {
                    return true;
                }

                // Allow fallback from V4 to V3.
                if (queryVersion == QueryRequestBase.QueryV4)
                {
                    queryVersion = QueryRequestBase.QueryV3;
                    return true;
                }

                return false;
            }
        }
    }
    
}
