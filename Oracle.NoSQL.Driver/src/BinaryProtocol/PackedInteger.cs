/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver.BinaryProtocol
{
    // Static methods for reading and writing packed integers.

    internal static class PackedInteger
    {
        // The maximum number of bytes needed to store an int value(5).
        internal const int MaxInt32Length = 5;

        // The maximum number of bytes needed to store a long value(9).
        internal const int MaxInt64Length = 9;

        // Reads a sorted packed integer at the given buffer offset and
        // returns it, advances the buffer offset
        internal static int ReadInt32(byte[] buf, ref int off)
        {
            int byteLen;
            bool negative;

            // The first byte of the buf stores the length of the value part.
            var b1 = buf[off++];
            // Adjust the byteLen to the real length of the value part.
            if (b1 < 0x08)
            {
                byteLen = 0x08 - b1;
                negative = true;
            }
            else if (b1 > 0xf7)
            {
                byteLen = b1 - 0xf7;
                negative = false;
            }
            else
            {
                return b1 - 127;
            }

            // The following bytes on the buf store the value as a big endian
            // integer. We extract the significant bytes from the buf and put them
            // into the value in big endian order.
            int value;
            if (negative)
            {
                value = -1;
            }
            else
            {
                value = 0;
            }

            if (byteLen > 3)
            {
                value = (value << 8) | buf[off++];
            }

            if (byteLen > 2)
            {
                value = (value << 8) | buf[off++];
            }

            if (byteLen > 1)
            {
                value = (value << 8) | buf[off++];
            }

            value = (value << 8) | buf[off++];

            // After get the adjusted value, we have to adjust it back to the
            // original value.
            if (negative)
            {
                value -= 119;
            }
            else
            {
                value += 121;
            }

            return value;
        }

        // Reads a sorted packed integer at the given buffer offset and
        // returns it, advances the buffer offset
        internal static long ReadInt64(byte[] buf, ref int off)
        {
            int byteLen;
            bool negative;

            // The first byte of the buf stores the length of the value part.
            var b1 = buf[off++] & 0xff;
            // Adjust the byteLen to the real length of the value part.
            if (b1 < 0x08)
            {
                byteLen = 0x08 - b1;
                negative = true;
            }
            else if (b1 > 0xf7)
            {
                byteLen = b1 - 0xf7;
                negative = false;
            }
            else
            {
                return b1 - 127;
            }

            // The following bytes on the buf store the value as a big endian
            // integer. We extract the significant bytes from the buf and put them
            // into the value in big endian order.
            long value;
            if (negative)
            {
                value = -1;
            }
            else
            {
                value = 0;
            }

            if (byteLen > 7)
            {
                value = (value << 8) | buf[off++];
            }

            if (byteLen > 6)
            {
                value = (value << 8) | buf[off++];
            }

            if (byteLen > 5)
            {
                value = (value << 8) | buf[off++];
            }

            if (byteLen > 4)
            {
                value = (value << 8) | buf[off++];
            }

            if (byteLen > 3)
            {
                value = (value << 8) | buf[off++];
            }

            if (byteLen > 2)
            {
                value = (value << 8) | buf[off++];
            }

            if (byteLen > 1)
            {
                value = (value << 8) | buf[off++];
            }

            value = (value << 8) | buf[off++];

            // After obtaining the adjusted value, we have to adjust it back to the
            // original value.
            if (negative)
            {
                value -= 119;
            }
            else
            {
                value += 121;
            }

            return value;
        }

        // Writes a packed sorted integer starting at the given buffer
        // offset and returns the next offset to be written.
        internal static int WriteInt32(byte[] buf, int offset, int value)
        {
            // Values in the inclusive range [-119,120] are stored in a single
            // byte. For values outside that range, the first byte stores the
            // number of additional bytes. The additional bytes store
            // (value + 119 for negative and value - 121 for positive) as an
            // unsigned big endian integer.
            var byte1Off = offset;
            offset++;
            if (value < -119)
            {
                // If the value < -119, then first adjust the value by adding
                // 119.  Then the adjusted value is stored as an unsigned big
                // endian integer.
                value += 119;

                // Store the adjusted value as an unsigned big endian integer.
                // For an negative integer, from left to right, the first
                // significant byte is the byte which is not equal to 0xFF.
                // Also please note that, because the adjusted value is stored
                // in big endian integer, we extract the significant byte from
                // left to right.
                //
                // In the left to right order, if the first byte of the
                // adjusted value is a significant byte, it will be stored in
                // the 2nd byte of the buf. Then we will look at the 2nd byte
                // of the adjusted value to see if this byte is the
                // significant byte, if yes, this byte will be stored in the
                // 3rd byte of the buf, and the like.
                if (((uint)value | 0x00FFFFFF) != 0xFFFFFFFF)
                {
                    buf[offset++] = (byte) (value >> 24);
                }

                if (((uint)value | 0x0000FFFF) != 0xFFFFFFFF)
                {
                    buf[offset++] = (byte) (value >> 16);
                }

                if (((uint)value | 0x000000FF) != 0xFFFFFFFF)
                {
                    buf[offset++] = (byte) (value >> 8);
                }

                buf[offset++] = (byte) value;

                // valueLen is the length of the value part stored in buf.
                // Because the first byte of buf is used to stored the length,
                // we need to subtract one.
                int valueLen = offset - byte1Off - 1;

                // The first byte stores the number of additional bytes. Here
                // we store the result of 0x08 - valueLen, rather than
                // directly store valueLen. The reason is to implement natural
                // sort order for byte-by-byte comparison.
                buf[byte1Off] = (byte) (0x08 - valueLen);
            }
            else if (value > 120)
            {
                // If the value > 120, then first adjust the value by
                // subtracting 121. Then the adjusted value is stored as an
                // unsigned big endian integer.
                value -= 121;

                // Store the adjusted value as an unsigned big endian integer.
                // For a positive integer, from left to right, the first
                // significant byte is the byte which is not equal to 0x00.
                //
                // In the left to right order, if the first byte of the
                // adjusted value is a significant byte, it will be stored in
                // the 2nd byte of the buf. Then we will look at the 2nd byte
                // of the adjusted value to see if this byte is the
                // significant byte, if yes, this byte will be stored in the
                // 3rd byte of the buf, and the like.
                if ((value & 0xFF000000) != 0)
                {
                    buf[offset++] = (byte) (value >> 24);
                }

                if ((value & 0xFFFF0000) != 0)
                {
                    buf[offset++] = (byte) (value >> 16);
                }

                if ((value & 0xFFFFFF00) != 0)
                {
                    buf[offset++] = (byte) (value >> 8);
                }

                buf[offset++] = (byte) value;

                // valueLen is the length of the value part stored in buf. Because
                // the first byte of buf is used to stored the length, we need to
                // subtract one.
                int valueLen = offset - byte1Off - 1;

                // The first byte stores the number of additional bytes. Here
                // we store the result of 0xF7 + valueLen, rather than
                // directly store valueLen. The reason is to implement natural
                // sort order for byte-by-byte comparison.
                buf[byte1Off] = (byte) (0xF7 + valueLen);
            }
            else
            {
                // If -119 <= value <= 120, only one byte is needed to store
                // the value. The stored value is the original value plus 127.
                buf[byte1Off] = (byte) (value + 127);
            }

            return offset;
        }

        // Writes a packed sorted long integer starting at the given buffer
        // offset and returns the next offset to be written.
        internal static int WriteInt64(byte[] buf, int offset, long value)
        {
            // Values in the inclusive range [-119,120] are stored in a single
            // byte. For values outside that range, the first byte stores the
            // number of additional bytes. The additional bytes store
            // (value + 119 for negative and value - 121 for positive) as an
            // unsigned big endian integer.
            int byte1Off = offset;
            offset++;
            if (value < -119)
            {
                // If the value < -119, then first adjust the value by adding
                // 119.  Then the adjusted value is stored as an unsigned big
                // endian integer.
                value += 119;

                // Store the adjusted value as an unsigned big endian integer.
                // For an negative integer, from left to right, the first
                // significant byte is the byte which is not equal to 0xFF.
                // Also please note that, because the adjusted value is stored
                // in big endian integer, we extract the significant byte from
                // left to right.
                //
                // In the left to right order, if the first byte of the
                // adjusted value is a significant byte, it will be stored in
                // the 2nd byte of the buf. Then we will look at the 2nd byte
                // of the adjusted value to see if this byte is the
                // significant byte, if yes, this byte will be stored in the
                // 3rd byte of the buf, and the like.
                if (((ulong)value | 0x00FFFFFFFFFFFFFFL) !=
                    0xFFFFFFFFFFFFFFFFL)
                {
                    buf[offset++] = (byte) (value >> 56);
                }

                if (((ulong)value | 0x0000FFFFFFFFFFFFL) !=
                    0xFFFFFFFFFFFFFFFFL)
                {
                    buf[offset++] = (byte) (value >> 48);
                }

                if (((ulong)value | 0x000000FFFFFFFFFFL) !=
                    0xFFFFFFFFFFFFFFFFL)
                {
                    buf[offset++] = (byte) (value >> 40);
                }

                if (((ulong)value | 0x00000000FFFFFFFFL) !=
                    0xFFFFFFFFFFFFFFFFL)
                {
                    buf[offset++] = (byte) (value >> 32);
                }

                if (((ulong)value | 0x0000000000FFFFFFL) !=
                    0xFFFFFFFFFFFFFFFFL)
                {
                    buf[offset++] = (byte) (value >> 24);
                }

                if (((ulong)value | 0x000000000000FFFFL) !=
                    0xFFFFFFFFFFFFFFFFL)
                {
                    buf[offset++] = (byte) (value >> 16);
                }

                if (((ulong)value | 0x00000000000000FFL) !=
                    0xFFFFFFFFFFFFFFFFL)
                {
                    buf[offset++] = (byte) (value >> 8);
                }

                buf[offset++] = (byte) value;

                // valueLen is the length of the value part stored in buf.
                // Because the first byte of buf is used to stored the length,
                // so we need to minus one.
                int valueLen = offset - byte1Off - 1;

                // The first byte stores the number of additional bytes. Here
                // we store the result of 0x08 - valueLen, rather than
                // directly store valueLen. The reason is to implement nature
                // sort order for byte-by-byte comparison.
                buf[byte1Off] = (byte) (0x08 - valueLen);
            }
            else if (value > 120)
            {
                // If the value > 120, then first adjust the value by
                // subtracting 119. Then the adjusted value is stored as an
                // unsigned big endian integer.
                value -= 121;

                // Store the adjusted value as an unsigned big endian integer.
                // For a positive integer, from left to right, the first
                // significant byte is the byte which is not equal to 0x00.
                //
                // In the left to right order, if the first byte of the
                // adjusted value is a significant byte, it will be stored in
                // the 2nd byte of the buf. Then we will look at the 2nd byte
                // of the adjusted value to see if this byte is the
                // significant byte, if yes, this byte will be stored in the
                // 3rd byte of the buf, and the like.
                if (((ulong)value & 0xFF00000000000000L) != 0L)
                {
                    buf[offset++] = (byte) (value >> 56);
                }

                if (((ulong)value & 0xFFFF000000000000L) != 0L)
                {
                    buf[offset++] = (byte) (value >> 48);
                }

                if (((ulong)value & 0xFFFFFF0000000000L) != 0L)
                {
                    buf[offset++] = (byte) (value >> 40);
                }

                if (((ulong)value & 0xFFFFFFFF00000000L) != 0L)
                {
                    buf[offset++] = (byte) (value >> 32);
                }

                if (((ulong)value & 0xFFFFFFFFFF000000L) != 0L)
                {
                    buf[offset++] = (byte) (value >> 24);
                }

                if (((ulong)value & 0xFFFFFFFFFFFF0000L) != 0L)
                {
                    buf[offset++] = (byte) (value >> 16);
                }

                if (((ulong)value & 0xFFFFFFFFFFFFFF00L) != 0L)
                {
                    buf[offset++] = (byte) (value >> 8);
                }

                buf[offset++] = (byte) value;

                // valueLen is the length of the value part stored in buf.
                // Because the first byte of buf is used to stored the length,
                // so we need to minus one.
                int valueLen = offset - byte1Off - 1;

                // The first byte stores the number of additional bytes. Here
                // we store the result of 0xF7 + valueLen, rather than
                // directly store valueLen. The reason is to implement nature
                // sort order for byte-by-byte comparison.
                buf[byte1Off] = (byte) (0xF7 + valueLen);
            }
            else
            {
                // If -119 <= value <= 120, only one byte is needed to store
                // the value. The stored value is the original value adds 127.
                buf[byte1Off] = (byte) (value + 127);
            }

            return offset;
        }
    }

}
