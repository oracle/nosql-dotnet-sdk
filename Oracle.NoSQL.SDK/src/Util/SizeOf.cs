/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections.Generic;

    internal static class SizeOf
    {
        // Using estimations provided here:
        // https://codeblog.jonskeet.uk/2011/04/05/of-memory-and-strings/

        internal static readonly int ObjectOverhead = IntPtr.Size * 2;
        internal static readonly int ObjectMinSize = IntPtr.Size * 3;
        internal static readonly int ObjectAlignment = IntPtr.Size;
        internal static readonly int StringOverhead = IntPtr.Size * 3 + 2;
        internal static readonly int ArrayOverhead = IntPtr.Size * 4;

        // Estimated from the reference source for List<T>
        internal static readonly long ListOverhead =
            GetObjectSize(IntPtr.Size * 2 + ArrayOverhead + 8);
        internal static readonly int ListEntryOverhead = IntPtr.Size;

        // Estimated from reference source for Dictionary<T>
        internal static readonly long DictionaryOverhead =
            GetObjectSize(IntPtr.Size * 6 + ArrayOverhead * 2 + 16);

        // Roughly: overhead of the entry itself (assuming key and value are
        // reference types) + one reference in entries array + 1 bucket index
        // in buckets array
        internal static readonly int DictionaryEntryOverhead =
            IntPtr.Size * 3 + 12;

        internal static readonly long OrderedDictionaryOverhead =
            GetObjectSize(IntPtr.Size * 2) + DictionaryOverhead +
            ListOverhead;

        internal static readonly int OrderedDictionaryEntryOverhead =
            DictionaryEntryOverhead + ListEntryOverhead;

        // Estimated from reference source for HashSet<T>, slot size +
        // bucket index size
        internal static readonly long HashSetEntryOverhead =
            IntPtr.Size + 12;

        // Estimated from reference source for SortedSet<T>, size of Node
        internal static readonly long SortedSetEntryOverhead =
            GetObjectSize(1 + 3 * IntPtr.Size);

        internal const int DateTimeSize = 8;

        internal static long Pad(long size) =>
            (size + ObjectAlignment - 1) / ObjectAlignment * ObjectAlignment;

        internal static long GetObjectSize(long size) =>
            Math.Max(Pad(ObjectOverhead + size), ObjectMinSize);

        internal static long GetStringSize(string value) =>
            Pad(StringOverhead + value.Length * sizeof(char));

        internal static long GetByteArraySize(byte[] value) =>
            Pad(ObjectMinSize + value.Length);

        internal static long GetMemorySize(long overhead, int count,
            IEnumerable<FieldValue> list)
        {
            long result = overhead + IntPtr.Size * count;
            foreach (var value in list)
            {
                result += value.GetMemorySize();
            }

            return result;
        }

        internal static long GetMemorySize(FieldValue[] array) =>
            GetMemorySize(ArrayOverhead, array.Length, array);

        internal static long GetDictionaryEntrySize(long keySize,
            long valueSize) => DictionaryEntryOverhead + keySize + valueSize;

        internal static long GetListEntrySize(long size) =>
            ListEntryOverhead + size;

        internal static long GetListOverheadSize(int count) =>
            ListOverhead + ListEntryOverhead * count;

        internal static long GetHashSetEntrySize(long size) =>
            HashSetEntryOverhead + size;
    }
}
