/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.NsonProtocol
{
    using System.IO;
    using static Protocol;
    using ErrorCode = BinaryProtocol.ErrorCode;
    using BinaryProtocol = BinaryProtocol.Protocol;

    internal partial class RequestSerializer : IRequestSerializer
    {
        // At this time this serializer only supports V4. To switch to earlier
        // protocol version, we have to switch serializer to
        // BinaryProtocol.RequestSerializer.
        private const short DefaultSerialVersion = Protocol.SerialVersion;

        public short SerialVersion => DefaultSerialVersion;

        // Nson request always starts with serial version.
        public void StartWrite(MemoryStream stream, Request request)
        {
            BinaryProtocol.WriteUnpackedInt16(stream, SerialVersion);
        }

        // In Nson, the error information is part of the top-level Nson map.
        public void StartRead(MemoryStream stream, Request request)
        {
            var code = stream.ReadByte();
            
            // If the client is connected to a pre-V4 server, the following
            // error codes can be returned by the pre-V4 servers:
            // V3: UnsupportedProtocol (24)
            // V2: BadProtocolMessage (17)
            // Neither of these currently maps to any valid Nson type, so we
            // know the server is not speaking V4 protocol. We can throw
            // UnsupportedProtocolException so that the protocol serial
            // version will be decremented accordingly.
            if (code == (int)ErrorCode.UnsupportedProtocol ||
                code == (int)ErrorCode.BadProtocolMessage)
            {
                throw new UnsupportedProtocolException(
                    $"Unsupported protocol version {SerialVersion}");
            }

            // The stream shouldn't be empty, but we will let deserializer
            // throw the exception on this.
            if (code != -1)
            {
                stream.Position = 0;
            }
        }

        public string ContentType => "application/octet-stream";

    }

}
