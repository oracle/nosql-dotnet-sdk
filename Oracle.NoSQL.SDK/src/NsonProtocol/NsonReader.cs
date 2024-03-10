/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.NsonProtocol
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Dynamic;
    using System.IO;
    using BinaryProtocol = BinaryProtocol.Protocol;
    using NsonType = DbType;

    internal class NsonReader
    {
        private class ComplexValueState
        {
            // Currently the type can only be Array or Map.
            internal NsonType Type { get; set; }
            internal int Count { get; set; }
            internal int NumberRead { get; set; }
            internal int Length { get; set; }
            internal int StartPosition { get; set; }
            internal bool IsDone => NumberRead == Count;
        }

        // To prevent an infinite loop for bad serialization.
        private const int MaxElementCount = 1_000_000_000;

        private readonly MemoryStream stream;

        private readonly Stack<ComplexValueState> complexValueStack =
            new Stack<ComplexValueState>();

        internal const NsonType NsonTypeNone = (NsonType)(-1);

        internal NsonReader(MemoryStream stream)
        {
            this.stream = stream;
        }

        private ComplexValueState ComplexStackTop =>
            complexValueStack.Count != 0
                ? complexValueStack.Peek()
                : null;

        private void CheckNsonType(NsonType expectedType)
        {
            if (NsonType != expectedType)
            {
                throw new BadProtocolException(
                    $"Cannot read value of type {NsonType} as type " +
                    expectedType);
            }
        }

        private void SkipBytes(int length)
        {
            if (stream.Position + length > stream.Length)
            {
                throw new BadProtocolException(
                    $"End of stream reached trying to skip {length} " +
                    $"byte(s) while skipping {NsonType}, " +
                    $"stream length: {stream.Length}, position: " +
                    stream.Position);
            }

            stream.Seek(length, SeekOrigin.Current);
        }

        private void SetStreamPosition(int position)
        {
            if (position > stream.Length)
            {
                throw new BadProtocolException(
                    $"Trying to set position {position} beyond the end of " +
                    $"the stream of length {stream.Length} while skipping " +
                    NsonType);
            }

            stream.Seek(position, SeekOrigin.Begin);
        }

        // Last Nson type read, NsonTypeNone if nothing is read yet.
        internal NsonType NsonType { get; private set; } = NsonTypeNone;

        // Total number of elements or entries in the current array or map,
        // or 0 if none. This lets the caller know how many entries to read.
        // Note that this value is only guaranteed to be correct right after
        // the call to Next() that finds the map or array.  Later this value
        // may change when reading nested maps or arrays.
        internal int Count => ComplexStackTop?.Count ?? 0;

        // Field name last read. If map entry is just read, the stream
        // is positioned to get the field value.  For atomic values, the
        // caller should call one of the Read...() methods to read the value.
        // For array or map, the caller should call Next() again to start
        // reading elements or entries, up to the value of Count.
        // Note that this is just last field name read, and is not saved on
        // the complexValueStack, so the parent map field name will not be
        // available even after child map has been read. For now, we expect
        // the caller to keep track of this if needed.
        internal string FieldName { get; private set; }

        // Start reading the next element.  For atomic values, the stream
        // will be positioned to call one of
        // Read...() methods to get the value.  For array or map, the caller
        // will need to call Next() again to read the elements or entries.
        // Note that if currently inside the map, this function will also
        // consume the field name, which will be available as FieldName
        // property.
        internal void Next()
        {
            var top = ComplexStackTop;

            // Check if we are done with current array or map, this can be
            // multi-level.
            while (top?.IsDone ?? false)
            {
                var lengthRead = (int)stream.Position - top.StartPosition;
                if (top.Length != lengthRead)
                {
                    // Or should we just log this instead?
                    throw new BadProtocolException(
                        $"Read invalid {NsonType} length: expected " +
                        $"{top.Length}, got {lengthRead}");
                }

                complexValueStack.Pop();
                top = ComplexStackTop;
            }

            if (top != null)
            {
                // If inside map, we must first read the field name.
                if (top.Type == NsonType.Map)
                {
                    FieldName = BinaryProtocol.ReadString(stream);
                }

                top.NumberRead++;
            }

            // We let the caller to validate the value read when it is
            // processed, to avoid bad performance of Enum.IsDefined,
            // see ValidateUtils.IsEnumDefined.
            NsonType = (NsonType)BinaryProtocol.ReadByte(stream);

            // Start array or map.
            if (NsonType == NsonType.Array || NsonType == NsonType.Map)
            {
                var complexState = new ComplexValueState
                {
                    Type = NsonType,
                    Length = BinaryProtocol.ReadUnpackedInt32(stream),
                    StartPosition = (int)stream.Position,
                    Count = BinaryProtocol.ReadUnpackedInt32(stream)
                };

                if (complexState.Count < 0 ||
                    complexState.Count > MaxElementCount)
                {
                    throw new BadProtocolException(
                        $"Invalid number of {NsonType} elements: " +
                        complexState.Count);
                }

                complexValueStack.Push(complexState);
            }
        }

        internal void ExpectType(NsonType type)
        {
            // Different error message than in CheckNsonType().
            if (NsonType != type)
            {
                throw new BadProtocolException(
                    $"Expecting type {type}, got type {NsonType}");
            }
        }

        internal byte[] ReadByteArray()
        {
            CheckNsonType(NsonType.Binary);
            return BinaryProtocol.ReadByteArray(stream);
        }

        internal bool ReadBoolean()
        {
            CheckNsonType(NsonType.Boolean);
            return BinaryProtocol.ReadBoolean(stream);
        }

        internal double ReadDouble()
        {
            CheckNsonType(NsonType.Double);
            return BinaryProtocol.ReadDouble(stream);
        }

        internal int ReadInt32()
        {
            CheckNsonType(NsonType.Integer);
            return BinaryProtocol.ReadPackedInt32(stream);
        }

        internal long ReadInt64()
        {
            CheckNsonType(NsonType.Long);
            return BinaryProtocol.ReadPackedInt64(stream);
        }

        internal string ReadString()
        {
            CheckNsonType(NsonType.String);
            return BinaryProtocol.ReadString(stream);
        }

        internal DateTime ReadDateTime()
        {
            CheckNsonType(NsonType.Timestamp);
            return BinaryProtocol.ReadDateTime(stream);
        }

        internal string ReadNumberAsString()
        {
            CheckNsonType(NsonType.Number);
            return BinaryProtocol.ReadString(stream);
        }

        // Currently this is not used because we may want to return double
        // instead, see BinaryProtocol.GetNumberValue().
        internal decimal ReadDecimal()
        {
            CheckNsonType(NsonType.Number);
            return decimal.Parse(BinaryProtocol.ReadString(stream));
        }

        // Skip current value, assumes the type code has already been read.
        internal void SkipValue()
        {
            switch (NsonType)
            {
                case NsonType.Array:
                case NsonType.Map:
                    var top = ComplexStackTop;
                    Debug.Assert(top != null);
                    SetStreamPosition(top.StartPosition + top.Length);
                    complexValueStack.Pop();
                    break;
                // Timestamp and Number are written as strings.  Both string
                // and binary use length-prefixed encoding.
                case NsonType.String:
                case NsonType.Binary:
                case NsonType.Timestamp:
                case NsonType.Number:
                    var length = BinaryProtocol.ReadPackedInt32(stream);
                    if (length > 0)
                    {
                        SkipBytes(length);
                    }
                    break;
                // fixed 1 byte length
                case NsonType.Boolean:
                    SkipBytes(1);
                    break;
                // fixed 8 byte length
                case NsonType.Double:
                    SkipBytes(8);
                    break;
                // variable length integer
                case NsonType.Integer:
                    BinaryProtocol.ReadPackedInt32(stream);
                    break;
                // variable length long
                case NsonType.Long:
                    BinaryProtocol.ReadPackedInt64(stream);
                    break;
                default:
                    throw new BadProtocolException(
                        $"Trying to skip unknown Nson type code: {NsonType}");
            }
        }

    }

}
