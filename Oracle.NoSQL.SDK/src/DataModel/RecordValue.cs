/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using static SizeOf;

    /// <summary>
    /// Represents an order-preserving dictionary with string keys and values
    /// of type <see cref="FieldValue" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used to represent values of NoSQL data type
    /// <em>Record</em>.  It also represents table rows on output operations
    /// such as <see cref="NoSQLClient.GetAsync"/> and
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/>. In both
    /// of these cases, the field (key) ordering is required to be preserved.
    /// </para>
    /// <para>
    /// In addition to the functionality of <see cref="MapValue"/> this class
    /// preserves the ordering of keys in the same order as they were added to
    /// the dictionary.  The entries are not sorted by the key, instead the
    /// order is determined solely by the order in which the new entries are
    /// added.  When enumerated with <c>foreach</c> loop, the order of entries
    /// is guaranteed to be the same.
    /// </para>
    /// <para>
    /// Each entry in the dictionary represented by this class has an index.
    /// The index ranges from 0 to <see cref="MapValue.Count"/> and is
    /// is determined when the new entry is added to the dictionary, except
    /// for <see cref="Insert"/> method that provides the index.  This class
    /// also allows retrieving keys and values by index.
    /// </para>
    /// <para>
    /// Even though <see cref="RecordValue"/> may be used wherever
    /// <see cref="MapValue"/> is used, note that <see cref="RecordValue"/> is
    /// more memory and computation expensive then <see cref="MapValue"/> and
    /// thus it is preferred to use <see cref="MapValue"/> when the field
    /// ordering is not required.
    /// </para>
    /// </remarks>
    /// <seealso cref="MapValue"/>
    public class RecordValue : MapValue
    {
        private OrderedDictionary<string, FieldValue>
            OrderedDictionaryValue =>
                (OrderedDictionary<string, FieldValue>)mapValue;

        /// <summary>
        /// Gets the value at the specified index.
        /// </summary>
        /// <param name="index">Zero-based index of the value.</param>
        /// <value>
        /// The value at the <paramref name="index"/>
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">If
        /// <paramref name="index"/> is less than 0 or equal to or greater
        /// than <see cref="MapValue.Count"/></exception>
        public override FieldValue this[int index] => GetValueAtIndex(index);

        /// <summary>
        /// Initializes a new instance of <see cref="RecordValue"/> that is
        /// empty and has the default initial capacity.
        /// </summary>
        /// <seealso cref="MapValue()"/>
        public RecordValue() :
            base(new OrderedDictionary<string, FieldValue>())
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RecordValue"/> that is
        /// empty and has the specified initial capacity.
        /// </summary>
        /// <param name="capacity">The number of elements that the new
        /// <see cref="RecordValue"/> can initially hold.</param>
        /// <seealso cref="MapValue(int)"/>
        public RecordValue(int capacity) :
            base(new OrderedDictionary<string, FieldValue>(capacity))
        {
        }

        internal new static RecordValue FromObject(object value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!(value is RecordValue))
            {
                throw new NotImplementedException(
                    $"Cannot convert from value of type {value.GetType()} " +
                    "to RecordValue, class mapping is not supported");
            }

            return (RecordValue)value;
        }

        /// <summary>
        /// Inserts an entry with the specified key and the specified value at
        /// the specified index.
        /// </summary>
        /// <remarks>
        /// If <paramref name="index"/> is equal to
        /// <see cref="MapValue.Count"/>, the <paramref name="key"/> and
        /// <paramref name="value"/> are added to the end of the collection.
        /// Note that for this method the order of the new entry within the
        /// dictionary is determined only by <paramref name="index"/>.
        /// Existing indexes following the <paramref name="index"/> may be
        /// shifted to make room for the new entry.
        /// </remarks>
        /// <param name="index">Zero-based index at which the entry should be
        /// inserted.</param>
        /// <param name="key">Key to add.</param>
        /// <param name="value">Value to add.  If <c>null</c>,
        /// <see cref="FieldValue.Null"/> will be added instead.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="key"/>
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If
        /// <paramref name="index"/> is less than 0 or greater than
        /// <see cref="MapValue.Count"/></exception>
        /// <exception cref="ArgumentException">The value with the specified
        /// key already exists in the dictionary.</exception>
        public void Insert(int index, string key, FieldValue value)
        {
            OrderedDictionaryValue.Insert(index, key, value ?? Null);
        }

        /// <summary>
        /// Removes the entry at the specified index.
        /// </summary>
        /// <remarks>
        /// This operation may involve shifting the entries following the
        /// <paramref name="index"/> to the left to replace the removed
        /// entry.
        /// </remarks>
        /// <param name="index">Zero-based index of the entry to remove.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">If
        /// <paramref name="index"/> is less than 0 or equal to or greater
        /// than <see cref="MapValue.Count"/></exception>
        public void RemoveAt(int index)
        {
            OrderedDictionaryValue.RemoveAt(index);
        }

        /// <summary>
        /// Gets the key at the specified index.
        /// </summary>
        /// <param name="index">Zero-based index of the key.</param>
        /// <returns>The key at the <paramref name="index"/></returns>
        /// <exception cref="ArgumentOutOfRangeException">If
        /// <paramref name="index"/> is less than 0 or equal to or greater
        /// than <see cref="MapValue.Count"/></exception>
        public string GetKeyAtIndex(int index) =>
            OrderedDictionaryValue.GetKeyAtIndex(index);

        /// <summary>
        /// Gets the value at the specified index.
        /// </summary>
        /// <param name="index">Zero-based index of the value.</param>
        /// <returns>The value at the <paramref name="index"/></returns>
        /// <exception cref="ArgumentOutOfRangeException">If
        /// <paramref name="index"/> is less than 0 or equal to or greater
        /// than <see cref="MapValue.Count"/></exception>
        public FieldValue GetValueAtIndex(int index) =>
            OrderedDictionaryValue.GetValueAtIndex(index);

        /// <inheritdoc/>
        public override bool Equals(FieldValue other)
        {
            if (!(other is RecordValue value))
            {
                return false;
            }

            if (Count != value.Count)
            {
                return false;
            }

            for (var i = 0; i < Count; i++)
            {
                var key1 = GetKeyAtIndex(i);
                var key2 = value.GetKeyAtIndex(i);

                if (key1 != key2)
                {
                    return false;
                }

                if (!TryGetValue(key1, out var fieldValue1) ||
                    !value.TryGetValue(key2, out var fieldValue2) ||
                    !fieldValue1.Equals(fieldValue2))
                {
                    return false;
                }
            }

            return true;
        }

        //TODO: override GetHashCode()

        internal override long GetMemorySize()
        {
            // Size of MapValue object itself + ordered dictionary overhead +
            // overhead of the entries
            var result = GetObjectSize(IntPtr.Size) +
                         OrderedDictionaryOverhead +
                         OrderedDictionaryEntryOverhead * mapValue.Count;

            foreach (var kv in mapValue)
            {
                result += GetStringSize(kv.Key) + kv.Value.GetMemorySize();
            }

            return result;
        }

    }

}
