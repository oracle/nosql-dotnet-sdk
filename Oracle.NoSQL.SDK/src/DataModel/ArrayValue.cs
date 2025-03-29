/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Text.Json;
    using static SizeOf;

    /// <summary>
    /// Represents an array of <see cref="FieldValue"/> instances.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used to represent values of NoSQL data type
    /// <em>Array</em>.  It represents its value as a <see cref="List{T}"/>
    /// <see cref="FieldValue"/> instances and uses zero-based indexes.
    /// The list is automatically resized as more values are added.
    /// </para>
    /// <para>
    /// This class supports all the functionality of <see cref="IList{T}"/>
    /// interface, including enumeration with <c>foreach</c> loop.
    /// </para>
    /// </remarks>
    /// <seealso cref="FieldValue"/>
    public class ArrayValue : FieldValue, IList<FieldValue>
    {
        private readonly IList<FieldValue> arrayValue;

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <param name="index">The zero-based index of the element to get or
        /// set.</param>
        /// <value>
        /// The element at the specified index.  For set operation, if the
        /// <paramref name="value"/> is <c>null</c>,
        /// <see cref="FieldValue.Null"/> will be used.
        /// </value>
        /// <exception cref="ArgumentOutOfRangeException">If
        /// <paramref name="index"/> is less than 0 or equal to or greater
        /// than <see cref="Count"/></exception>
        public override FieldValue this[int index]
        {
            get => arrayValue[index];
            set => arrayValue[index] = value ?? Null;
        }

        /// <inheritdoc cref="FieldValue.DbType" path="summary"/>
        /// <value>
        /// <see cref="SDK.DbType.Array"/>
        /// </value>
        public override DbType DbType => DbType.Array;

        /// <summary>
        /// Gets the number of elements in this <see cref="ArrayValue"/>
        /// instance.
        /// </summary>
        /// <value>
        /// The number of elements contained in the array which this instance
        /// represents.
        /// </value>
        public override int Count => arrayValue.Count;

        /// <summary>
        /// Gets a value indicating whether the <see cref="ICollection{T}"/>
        /// is read-only.
        /// </summary>
        /// <value>
        /// <c>true</c> if the <see cref="ICollection{T}"/> is read-only,
        /// otherwise <c>false</c>.  Default implementation always returns
        /// <c>false</c>.
        /// </value>
        bool ICollection<FieldValue>.IsReadOnly => false;

        internal new static ArrayValue DeserializeFromJson(
            ref Utf8JsonReader reader, JsonInputOptions options,
            bool hasReadToken)
        {
            if (!hasReadToken)
            {
                if (!reader.Read())
                {
                    throw new JsonException(
                        "No tokens available for ArrayValue");
                }

                if (reader.TokenType != JsonTokenType.StartArray)
                {
                    throw new JsonException(
                        "Missing StartArray token for ArrayValue");
                }
            }

            var value = new ArrayValue();

            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndArray)
                {
                    return value;
                }

                value.Add(FieldValue.DeserializeFromJson(ref reader, options,
                    true));
            }

            throw new JsonException(
                "Missing EndArray token for ArrayValue");
        }

        internal static int QueryCompareArrayValues(ArrayValue value1,
            ArrayValue value2, int nullRank)
        {
            using var enum1 =
                ((IEnumerable<FieldValue>)value1).GetEnumerator();
            using var enum2 =
                ((IEnumerable<FieldValue>)value2).GetEnumerator();

            while (enum1.MoveNext() && enum2.MoveNext())
            {
                Debug.Assert(enum1.Current != null);
                var result = enum1.Current.QueryCompareTotalOrder(
                    enum2.Current, nullRank);
                if (result != 0)
                {
                    return result;
                }
            }

            return value1.Count.CompareTo(value2.Count);
        }

        internal void Sort(Comparison<FieldValue> comparison)
        {
            // We use List.Sort() to sort in place. This code will need to
            // change if the type of arrayValue changes.
            Debug.Assert(arrayValue is List<FieldValue>);
            ((List<FieldValue>)arrayValue).Sort(comparison);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ArrayValue"/> that is
        /// empty and has the default initial capacity.
        /// </summary>
        /// <seealso cref="ArrayValue(int)"/>
        public ArrayValue()
        {
            arrayValue = new List<FieldValue>();
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ArrayValue"/> that is
        /// empty and has the specified initial capacity.
        /// </summary>
        /// <remarks>
        /// The capacity of <see cref="ArrayValue"/> is the number of
        /// elements that <see cref="ArrayValue"/> can hold. As elements
        /// are added to an <see cref="ArrayValue"/>, the capacity is
        /// automatically increased as required.
        /// </remarks>
        /// <param name="capacity">The number of elements that the new
        /// <see cref="ArrayValue"/> can initially hold.</param>
        public ArrayValue(int capacity)
        {
            arrayValue = new List<FieldValue>(capacity);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ArrayValue"/> that
        /// contains elements from the specified collection of
        /// <see cref="FieldValue"/> instances.
        /// </summary>
        /// <remarks>
        /// The elements are added in the same order as they are read by
        /// the enumerator of the collection.
        /// </remarks>
        /// <param name="collection">The collection whose elements are added
        /// to the new <see cref="ArrayValue"/>.</param>
        /// <exception cref="ArgumentNullException">If
        /// <paramref name="collection"/> is <c>null</c>.</exception>
        public ArrayValue(IEnumerable<FieldValue> collection) : this()
        {
            if (collection is null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            // We call Add() individually to account for null values.
            foreach (var item in collection)
            {
                Add(item);
            }
        }

        /// <summary>
        /// Adds a value to the end of the list represented by this instance.
        /// </summary>
        /// <param name="item"><see cref="FieldValue"/> instance to be added
        /// to the end of the list represented by this instance.  If
        /// <c>null</c>, <see cref="FieldValue.Null"/> will be added instead.
        /// </param>
        public void Add(FieldValue item)
        {
            arrayValue.Add(item ?? Null);
        }

        /// <summary>
        /// Searches for the specified value in the list represented by this
        /// instance and returns the zero-based index of the first occurrence
        /// of this value.
        /// </summary>
        /// <remarks>
        /// The equality of the values in the list is determined by the
        /// semantics of <see cref="FieldValue.Equals(FieldValue)"/> method.
        /// </remarks>
        /// <param name="item">The value to search for.</param>
        /// <returns>The zero-based index of the first occurrence of
        /// <paramref name="item"/> if found, otherwise -1.</returns>
        public int IndexOf(FieldValue item)
        {
            return arrayValue.IndexOf(item);
        }

        /// <summary>
        /// Inserts a value into the list represented by this instance at the
        /// specified index.
        /// </summary>
        /// <remarks>
        /// This operation may involve shifting the elements following the
        /// <paramref name="index"/> to the right to make room for the
        /// <paramref name="item"/>.  If <paramref name="index"/> is equal to
        /// <see cref="Count"/>, the value is added to the end of the list.
        /// </remarks>
        /// <param name="index">The zero-based index at which to insert
        /// <paramref name="item"/>.</param>
        /// <param name="item">The value to be inserted.  If <c>null</c>,
        /// <see cref="FieldValue.Null"/> will be inserted instead.</param>
        /// <exception cref="ArgumentOutOfRangeException">If
        /// <paramref name="index"/> is less than 0 or greater
        /// than <see cref="Count"/></exception>
        public void Insert(int index, FieldValue item)
        {
            arrayValue.Insert(index, item ?? Null);
        }

        /// <summary>
        /// Removes the element at the specified index from the list
        /// represented by this instance.
        /// </summary>
        /// <remarks>
        /// This operation may involve shifting the elements following the
        /// <paramref name="index"/> to the left to replace the removed
        /// element.
        /// </remarks>
        /// <param name="index">The zero-based index of the element to remove.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException">If
        /// <paramref name="index"/> is less than 0 or equal to or greater
        /// than <see cref="Count"/></exception>
        public void RemoveAt(int index)
        {
            arrayValue.RemoveAt(index);
        }

        /// <summary>
        /// Remove all elements from the list represented by this instance.
        /// </summary>
        /// <remarks>
        /// <see cref="ArrayValue.Count"/> is set to 0, but the capacity of
        /// the list is not changed by this operation.
        /// </remarks>
        public void Clear()
        {
            arrayValue.Clear();
        }

        /// <summary>
        /// Determines whether a specified element is in the list represented
        /// by this instance.
        /// </summary>
        /// <remarks>
        /// The equality of the values is determined by the semantics of
        /// <see cref="FieldValue.Equals(FieldValue)"/> method.
        /// </remarks>
        /// <param name="item">The value to locate.</param>
        /// <returns><c>true</c> if the value is found, otherwise
        /// <c>false</c>.</returns>
        public bool Contains(FieldValue item)
        {
            return arrayValue.Contains(item);
        }

        /// <summary>
        /// Copies the entire list represented by this instance to an array of
        /// <see cref="FieldValue"/>, starting at the specified index of the
        /// target array.
        /// </summary>
        /// <remarks>
        /// Only references to <see cref="FieldValue"/> instances within the
        /// list are copied (the values are not cloned).
        /// </remarks>
        /// <param name="array">The target array.</param>
        /// <param name="arrayIndex">The zero-based index in
        /// <paramref name="array"/> where to start copying.</param>
        /// <exception cref="ArgumentOutOfRangeException">If
        /// <paramref name="arrayIndex"/> is less than 0.</exception>
        /// <exception cref="ArgumentNullException">If
        /// <paramref name="array"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">There is not enough space in
        /// <paramref name="array"/> starting at <paramref name="arrayIndex"/>
        /// to copy all the elements.</exception>
        public void CopyTo(FieldValue[] array, int arrayIndex)
        {
            arrayValue.CopyTo(array, arrayIndex);
        }

        /// <summary>
        /// Removes the first occurrence of a specific value from the list
        /// represented by this instance.
        /// </summary>
        /// <remarks>
        /// The equality of the values is determined by the semantics of
        /// <see cref="FieldValue.Equals(FieldValue)"/> method.
        /// This operation may involve shifting the elements following the
        /// removed element to the left to replace the removed element.
        /// </remarks>
        /// <param name="item">Value to remove.</param>
        /// <returns><c>true</c> if the value was successfully removed,
        /// <c>false</c> if the value was not found in the list.</returns>
        public bool Remove(FieldValue item)
        {
            return arrayValue.Remove(item);
        }

        /// <inheritdoc/>
        IEnumerator<FieldValue> IEnumerable<FieldValue>.GetEnumerator()
        {
            return arrayValue.GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return arrayValue.GetEnumerator();
        }

        internal new static ArrayValue FromObject(object value)
        {
            Debug.Assert(value is ArrayValue);
            return (ArrayValue)value;
        }

        /// <inheritdoc/>
        public override void SerializeAsJson(Utf8JsonWriter writer,
            JsonOutputOptions options = null)
        {
            writer.WriteStartArray();
            foreach (var value in this)
            {
                value.SerializeAsJson(writer, options);
            }
            writer.WriteEndArray();
        }

        internal override bool IsAtomic => false;

        internal override bool SupportsComparison => false;

        internal override bool QueryEquals(FieldValue other)
        {
            if (!(other is ArrayValue value))
            {
                return false;
            }

            if (Count != value.Count)
            {
                return false;
            }

            for (var i = 0; i < Count; i++)
            {
                Debug.Assert(!(this[i] is null));
                if (!this[i].QueryEquals(value[i]))
                {
                    return false;
                }
            }

            return true;
        }

        internal override int QueryCompareTotalOrder(FieldValue other,
            int nullRank) => other.DbType == DbType.Array
            ? QueryCompareArrayValues(this, other.AsArrayValue, nullRank)
            : 1;

        internal override int QueryHashCode()
        {
            var code = 1;
            foreach (var elem in arrayValue)
            {
                code = 31 * code + elem.QueryHashCode();
            }

            return code;
        }

        internal override long GetMemorySize()
        {
            // Size of ArrayValue object itself + list overhead +
            // overhead of the entries
            var result = GetObjectSize(IntPtr.Size) + ListOverhead +
                ListEntryOverhead * arrayValue.Count;

            foreach(var elem in arrayValue)
            {
                result += elem.GetMemorySize();
            }

            return result;
        }

    }

}
