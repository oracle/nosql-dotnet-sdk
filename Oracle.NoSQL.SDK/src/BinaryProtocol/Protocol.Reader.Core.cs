/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.BinaryProtocol
{
    using System;
    using System.Globalization;
    using System.IO;
    using System.Text;

    // Static methods for reading binary protocol data

    // One question is whether we should cache HTTP response body in memory
    // and present it here as MemoryStream, or not cache it and use regular
    // Stream (which is probably buffered stream backed by socket stream)?
    // I tend towards MemoryStream here for following reasons:
    // 1) Our responses are fairly small (max of 2 MB).
    // 2) If we don't cache the response body, we would have to use Async
    // methods to read from the stream (because using sync methods could
    // block), which means we would have to create Task object for every
    // small data item.
    // Same applies to writing request body (in addition to the fact that
    // for writing the current protocol requires seekable stream).

    internal static partial class Protocol
    {
        private static string GetEndOfStreamMessage(MemoryStream stream,
            string dataType, int? length = null)
        {
            return $"End of stream reached while reading {dataType} " +
                (length == null ? "" : "of length " + length) +
                $", stream length: {stream.Length}, " +
                $"position: {stream.Position}";
        }

        private static byte[] ReadByteArray(MemoryStream stream, int length)
        {
            if (stream.Position + length > stream.Length)
            {
                throw new BadProtocolException(GetEndOfStreamMessage(stream,
                    "binary", length));
            }

            var value = new byte[length];
            // Some say that Buffer.BlockCopy is actually slower than Array.Copy
            Array.Copy(stream.GetBuffer(), stream.Position, value, 0, length);
            stream.Position += length;
            return value;
        }

        internal static T[] ReadArray<T>(MemoryStream stream,
            Func<MemoryStream, T> readItem)
        {
            var length = ReadPackedInt32(stream);
            if (length < -1)
            {
                throw new BadProtocolException(
                    $"Received invalid array length: {length}");
            }

            if (length == -1)
            {
                return null;
            }

            var value = new T[length];
            for (int i = 0; i < length; i++)
            {
                value[i] = readItem(stream);
            }

            return value;
        }

        internal static sbyte ReadByte(MemoryStream stream)
        {
            var value = stream.ReadByte();
            if (value == -1)
            {
                throw new BadProtocolException(GetEndOfStreamMessage(stream,
                    "byte"));
            }

            return (sbyte)value;
        }

        internal static bool ReadBoolean(MemoryStream stream)
        {
            var value = stream.ReadByte();
            if (value == -1)
            {
                throw new BadProtocolException(GetEndOfStreamMessage(stream,
                    "boolean"));
            }

            return value != 0;
        }

        // Read packed integer
        internal static int ReadPackedInt32(MemoryStream stream)
        {
            try
            {
                var position = (int)stream.Position;
                var value = PackedInteger.ReadInt32(stream.GetBuffer(),
                    ref position);
                stream.Position = position;
                return value;
            }
            catch (IndexOutOfRangeException)
            {
                throw new BadProtocolException(GetEndOfStreamMessage(stream,
                    "Packed Int32"));
            }
        }

        // Read packed long
        internal static long ReadPackedInt64(MemoryStream stream)
        {
            try
            {
                var position = (int)stream.Position;
                var value = PackedInteger.ReadInt64(stream.GetBuffer(),
                    ref position);
                stream.Position = position;
                return value;
            }
            catch (IndexOutOfRangeException)
            {
                throw new BadProtocolException(GetEndOfStreamMessage(stream,
                    "Packed Int64"));
            }
        }

        internal static string ReadString(MemoryStream stream)
        {
            var length = ReadPackedInt32(stream);
            if (length < -1)
            {
                throw new BadProtocolException(
                    $"Received invalid string length: {length}");
            }
            if (length == -1)
            {
                return null;
            }

            if (stream.Position + length > stream.Length)
            {
                throw new BadProtocolException(GetEndOfStreamMessage(
                    stream, "string", length));
            }

            // This encoding is using replacement fallback, so exception
            // should not be thrown on invalid byte sequence
            var value = Encoding.UTF8.GetString(stream.GetBuffer(),
                (int)stream.Position, length);
            stream.Position += length;
            return value;
        }

        internal static byte[] ReadByteArray(MemoryStream stream)
        {
            var length = ReadPackedInt32(stream);
            if (length < -1)
            {
                throw new BadProtocolException(
                    $"Received invalid binary length: {length}");
            }

            if (length == -1)
            {
                return null;
            }
            return ReadByteArray(stream, length);
        }

        // Equivalent to readByteArrayWithInt() in BinaryProtocol.java
        internal static byte[] ReadByteArrayWithUnpackedLength(
            MemoryStream stream)
        {
            var length = ReadUnpackedInt32(stream);
            if (length < 0)
            {
                throw new BadProtocolException(
                    $"Received invalid binary length: {length}");
            }
            return ReadByteArray(stream, length);
        }

        // Read as 2 byte big endian
        internal static short ReadUnpackedInt16(MemoryStream stream)
        {
            byte[] bytes = ReadByteArray(stream, 2);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToInt16(bytes, 0);
        }

        // Read as 4 byte big endian
        internal static int ReadUnpackedInt32(MemoryStream stream)
        {
            byte[] bytes = ReadByteArray(stream, 4);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToInt32(bytes, 0);
        }

        internal static double ReadDouble(MemoryStream stream)
        {
            byte[] bytes = ReadByteArray(stream, 8);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(bytes);
            }

            return BitConverter.ToDouble(bytes, 0);
        }

        internal static DateTime ReadDateTime(MemoryStream stream)
        {
            string dateTimeString = ReadString(stream);
            if (!DateTime.TryParse(dateTimeString, out DateTime value))
            {
                throw new BadProtocolException(
                    $"Received invalid DateTime string: {dateTimeString}");
            }

            // Server should always send dates in UTC
            return DateTime.SpecifyKind(value, DateTimeKind.Utc);
        }

        internal static string[] ReadStringArray(MemoryStream stream)
        {
            return ReadArray(stream, ReadString);
        }

        internal static int[] ReadPackedInt32Array(MemoryStream stream)
        {
            return ReadArray(stream, ReadPackedInt32);
        }

    }

}
