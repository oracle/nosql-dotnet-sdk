/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.BinaryProtocol
{
    using System;
    using System.IO;
    using System.Text;

    // Static methods for writing binary protocol values

    internal static partial class Protocol
    {
        // ISO 8601 format without "Z" suffix
        private const string TimestampFormat =
            "yyyy'-'MM'-'dd'T'HH':'mm':'ss.fffffff";

        // Note that this may increase the stream.Length beyond the total
        // size of data.  We will use stream.Position to determine how much
        // data has been written.
        private static void EnsureExtraLength(MemoryStream stream, int value)
        {
            var totalLength = stream.Position + value;
            if (stream.Length < totalLength)
            {
                stream.SetLength(totalLength);
            }
        }

        internal static void WriteByte(MemoryStream stream, byte value)
        {
            stream.WriteByte(value);
        }

        // Write packed integer
        internal static void WritePackedInt32(MemoryStream stream, int value)
        {
            EnsureExtraLength(stream, PackedInteger.MaxInt32Length);
            stream.Position = PackedInteger.WriteInt32(stream.GetBuffer(),
                (int) stream.Position, value);
        }

        // Write packed long
        internal static void WritePackedInt64(MemoryStream stream, long value)
        {
            EnsureExtraLength(stream, PackedInteger.MaxInt64Length);
            stream.Position = PackedInteger.WriteInt64(stream.GetBuffer(),
                (int) stream.Position, value);
        }

        internal static void WriteString(MemoryStream stream, string value)
        {
            if (value != null)
            {
                byte[] bytes = Encoding.UTF8.GetBytes(value);
                WritePackedInt32(stream, bytes.Length);
                stream.Write(bytes, 0, bytes.Length);
            }
            else
            {
                WritePackedInt32(stream, -1);
            }
        }

        internal static void WriteByteArray(MemoryStream stream, byte[] value)
        {
            if (value != null)
            {
                WritePackedInt32(stream, value.Length);
                stream.Write(value, 0, value.Length);
            }
            else
            {
                WritePackedInt32(stream, -1);
            }
        }

        // Equivalent to writeByteArrayWithInt() in BinaryProtocol.java
        internal static void WriteByteArrayWithUnpackedLength(
            MemoryStream stream, byte[] value)
        {
            if (value != null)
            {
                WriteUnpackedInt32(stream, value.Length);
                stream.Write(value, 0, value.Length);
            }
            else
            {
                WriteUnpackedInt32(stream, 0);
            }
        }

        // Write as 2 byte big endian
        internal static void WriteUnpackedInt16(MemoryStream stream,
            short value)
        {
            // Should we use unsafe code here to avoid creating extra
            // byte array?
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            stream.Write(bytes, 0, bytes.Length);
        }

        // Write as 4 byte big endian
        internal static void WriteUnpackedInt32(MemoryStream stream,
            int value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            stream.Write(bytes, 0, bytes.Length);
        }

        internal static void WriteBoolean(MemoryStream stream, bool value)
        {
            stream.WriteByte(value ? (byte) 1 : (byte) 0);
        }

        // Write as 8 byte big endian
        internal static void WriteDouble(MemoryStream stream, double value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            stream.Write(bytes, 0, bytes.Length);
        }

        // Timestamps are always in UTC
        internal static void WriteDateTime(MemoryStream stream,
            DateTime value)
        {
            WriteString(stream, value.ToUniversalTime().ToString(
                TimestampFormat));
        }

    }

}
