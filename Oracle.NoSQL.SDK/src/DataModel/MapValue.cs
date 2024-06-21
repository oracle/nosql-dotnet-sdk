/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
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
    /// Represents an unordered dictionary with string keys and values of type
    /// <see cref="FieldValue" />.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used to represent values of NoSQL data types
    /// <em>Map</em> and <em>Json</em>.  On input, for operations such as
    /// <see cref="NoSQLClient.PutAsync"/> and
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/> this
    /// class also represents table rows.  In addition, this class also
    /// represents primary key values used for operations such as
    /// <see cref="NoSQLClient.GetAsync"/> and
    /// <see cref="NoSQLClient.DeleteAsync"/>.  For the cases listed above
    /// the key ordering is not required.  When the key ordering is required,
    /// e.g. for the results of operations such as
    /// <see cref="NoSQLClient.GetAsync"/> and
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/>,
    /// <see cref="RecordValue"/> is used which represents the ordered
    /// dictionary and is a subclass of this class.
    /// </para>
    /// <para>
    /// The instances of <see cref="MapValue"/> may have a nested structure
    /// as the values it represents may be of any subclass of
    /// <see cref="FieldValue"/> including complex types such as
    /// <see cref="ArrayValue"/>, <see cref="MapValue"/> and
    /// <see cref="RecordValue"/>. On input, e.g. when inserting a row via
    /// <see cref="NoSQLClient.PutAsync"/>, the mapping between the keys of
    /// <see cref="MapValue"/> instance and table fields is case-insensitive.
    /// The same is true for keys/fields inside the values of data types
    /// <em>Map</em> and <em>Record</em>.  However, for fields inside a value
    /// of data type <em>Json</em> the mapping is case-sensitive.
    /// </para>
    /// <para>
    /// This class supports all the functionality of
    /// <see cref="IDictionary{TKey, TValue}"/> interface, including
    /// enumeration with <c>foreach</c> loop.
    /// </para>
    /// </remarks>
    /// <example>
    /// Creating an instance of <see cref="MapValue"/> to represent a row with
    /// fields "id" and "info" of data types <em>Long</em> and <em>Json</em>
    /// respectively.
    /// <code>
    /// var row = new MapValue
    /// {
    ///     ["id"] = 1000,
    ///     ["info"] = new MapValue
    ///     {
    ///         ["myArray"] = new ArrayValue { 1, 2, 3, 4, 5 },
    ///         ["myObject"] = new MapValue
    ///         {
    ///             ["myString"] = "abc",
    ///             ["myBinary"] = Convert.FromBase64String("xyz")
    ///         }
    ///     }
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="FieldValue"/>
    public class MapValue : FieldValue, IDictionary<string, FieldValue>
    {
        internal readonly IDictionary<string, FieldValue> mapValue;

        /// <summary>
        /// Gets the collection containing the keys in the dictionary
        /// represented by this <see cref="MapValue"/> instance.
        /// </summary>
        /// <value>
        /// Collection containing the keys in the dictionary represented by
        /// this instance.
        /// </value>
        public ICollection<string> Keys => mapValue.Keys;

        /// <summary>
        /// Gets the collection containing the values in the dictionary
        /// represented by this <see cref="MapValue"/> instance.
        /// </summary>
        /// <value>
        /// Collection containing the values in the dictionary represented by
        /// this instance.
        /// </value>
        public ICollection<FieldValue> Values => mapValue.Values;

        /// <summary>
        /// Gets the number of key/value pairs in the dictionary represented
        /// by this <see cref="MapValue"/> instance.
        /// </summary>
        /// <value>
        /// The number of key/value pairs in the dictionary which this
        /// instance represents.
        /// </value>
        public override int Count => mapValue.Count;

        /// <inheritdoc cref="FieldValue.DbType" path="summary"/>
        /// <value>
        /// <see cref="SDK.DbType.Map"/>
        /// </value>
        public override DbType DbType => DbType.Map;

        /// <summary>
        /// Gets a value indicating whether the <see cref="ICollection{T}"/>
        /// is read-only.
        /// </summary>
        /// <value>
        /// <c>true</c> if the <see cref="ICollection{T}"/> is read-only,
        /// otherwise <c>false</c>.  Default implementation always returns
        /// <c>false</c>.
        /// </value>
        bool ICollection<KeyValuePair<string, FieldValue>>.IsReadOnly =>
            false;

        internal new static MapValue DeserializeFromJson(
            ref Utf8JsonReader reader, JsonInputOptions options,
            bool hasReadToken)
        {
            if (!hasReadToken)
            {
                if (!reader.Read())
                {
                    throw new JsonException(
                        "No tokens available for MapValue");
                }

                if (reader.TokenType != JsonTokenType.StartObject)
                {
                    throw new JsonException(
                        "Missing StartObject token for MapValue");
                }
            }

            var value = new MapValue();

            string propertyName = null;
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.EndObject)
                {
                    return value;
                }

                if (propertyName == null)
                {
                    if (reader.TokenType != JsonTokenType.PropertyName)
                    {
                        throw new JsonException(
                            "Missing property name for MapValue");
                    }

                    propertyName = reader.GetString();
                    continue;
                }

                // Using indexer instead of Add() will allow duplicate keys
                // in JSON string (which are technically allowed but
                // discouraged) and will result in the last value of a
                // particular key being used.
                value[propertyName] = FieldValue.DeserializeFromJson(
                    ref reader, options, true);
                propertyName = null;
            }

            throw new JsonException(
                "Missing EndObject token for MapValue");
        }


        internal static int QueryCompareMaps(IEnumerable<string> keys1,
            MapValue value1, IEnumerable<string> keys2, MapValue value2,
            int nullRank)
        {
            using var enum1 = keys1.GetEnumerator();
            using var enum2 = keys2.GetEnumerator();

            while (enum1.MoveNext() && enum2.MoveNext())
            {
                Debug.Assert(enum1.Current != null);
                Debug.Assert(enum2.Current != null);

                var result = string.Compare(enum1.Current, enum2.Current,
                    StringComparison.Ordinal);
                if (result != 0)
                {
                    return result;
                }

                result = value1[enum1.Current].QueryCompareTotalOrder(
                    value2[enum2.Current], nullRank);
                if (result != 0)
                {
                    return result;
                }
            }

            return value1.Count.CompareTo(value2.Count);
        }

        internal static int QueryCompareMapValues(MapValue value1,
            MapValue value2, int nullRank) => QueryCompareMaps(
            new SortedSet<string>(value1.Keys), value1,
            new SortedSet<string>(value2.Keys), value2, nullRank);

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <value>
        /// The value associated with the <paramref name="key"/>. If the
        /// <paramref name="key"/> is not found, a get operation throws a
        /// <see cref="KeyNotFoundException"/>, and a set operation creates a
        /// new value for the <paramref name="key"/>.  For set operation, if
        /// <paramref name="value"/> is <c>null</c>,
        /// <see cref="FieldValue.Null"/> will be used.
        /// </value>
        /// <exception cref="ArgumentNullException">The <paramref name="key"/>
        /// is <c>null</c>.</exception>
        /// <exception cref="KeyNotFoundException">The value is retrieved and
        /// the <paramref name="key"/> is not found in the dictionary.
        /// </exception>
        public override FieldValue this[string key]
        {
            get => mapValue[key];
            set => mapValue[key] = value ?? Null;
        }

        internal MapValue(IDictionary<string, FieldValue> value)
        {
            mapValue = value;
        }

        /// <summary>
        /// Initializes a new instance of <see cref="MapValue"/> that is empty
        /// and has the default initial capacity.
        /// </summary>
        /// <seealso cref="MapValue(int)"/>
        public MapValue() : this(new Dictionary<string, FieldValue>())
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="MapValue"/> that is empty
        /// and has the specified initial capacity.
        /// </summary>
        /// <remarks>
        /// The capacity of <see cref="MapValue"/> is the number of key/value
        /// pairs that <see cref="MapValue"/> can hold. As key and values are
        /// added to <see cref="MapValue"/>, the capacity is automatically
        /// increased as required.
        /// </remarks>
        /// <param name="capacity">The number of elements that the new
        /// <see cref="MapValue"/> can initially hold.</param>
        public MapValue(int capacity) :
            this(new Dictionary<string, FieldValue>(capacity))
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="MapValue"/> that
        /// contains elements from the specified collection of key-value
        /// pairs.
        /// </summary>
        /// <param name="collection">The collection whose elements are copied
        /// into the new <see cref="MapValue"/>.</param>
        /// <exception cref="ArgumentNullException">If
        /// <paramref name="collection"/> is <c>null</c>.</exception>
        public MapValue(
            IEnumerable<KeyValuePair<string, FieldValue>> collection) : this()
        {
            if (collection is null)
            {
                throw new ArgumentNullException(nameof(collection));
            }

            // We call ICollection<>.Add() individually to account for nulls.
            foreach (var item in collection)
            {
                ((ICollection<KeyValuePair<string, FieldValue>>)this).Add(
                    item);
            }
        }

        internal new static MapValue FromObject(object value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            if (!(value is MapValue))
            {
                throw new NotImplementedException(
                    $"Cannot convert from value of type {value.GetType()} " +
                    "to MapValue, class mapping is not supported");
            }

            return (MapValue)value;
        }

        internal TValue ToObject<TValue>()
        {
            Debug.Assert(this is TValue);
            return (TValue)(object)this;
        }

        /// <summary>
        /// Adds the specified key and value to the dictionary represented by
        /// this instance.
        /// </summary>
        /// <param name="key">The key to add.</param>
        /// <param name="value">The value to add.  If <c>null</c>,
        /// <see cref="FieldValue.Null"/> will be added instead.</param>
        /// <exception cref="ArgumentNullException">If <paramref name="key"/>
        /// is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">The value with the specified
        /// key already exists in the dictionary.</exception>
        public void Add(string key, FieldValue value)
        {
            mapValue.Add(key, value ?? Null);
        }

        /// <summary>
        /// Determines whether the specified key exists in the dictionary
        /// represented by this instance.
        /// </summary>
        /// <param name="key">The key to locate.</param>
        /// <returns><c>true</c> if the dictionary contains an element with
        /// the specified key, otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">The <paramref name="key"/>
        /// is <c>null</c>.</exception>
        public bool ContainsKey(string key)
        {
            return mapValue.ContainsKey(key);
        }

        /// <summary>
        /// Removes the value with the specified key from the dictionary
        /// represented by this instance.
        /// </summary>
        /// <param name="key">The key to remove.</param>
        /// <returns><c>true</c> if the value was found and removed,
        /// <c>false</c> if <paramref name="key"/> was not found in the
        /// dictionary.</returns>
        /// <exception cref="ArgumentNullException">The <paramref name="key"/>
        /// is <c>null</c>.</exception>
        public bool Remove(string key)
        {
            return mapValue.Remove(key);
        }

        /// <summary>
        /// Gets the value associated with the specified key.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="val">When this method returns, this parameter will
        /// contain the value associated with the specified key if the key
        /// was found, otherwise it will be <c>null</c>.</param>
        /// <returns><c>true</c> if <paramref name="key"/> was found in the
        /// dictionary, otherwise <c>false</c>.</returns>
        public bool TryGetValue(string key, out FieldValue val)
        {
            return mapValue.TryGetValue(key, out val);
        }

        /// <summary>
        /// Remove all keys and values from the dictionary represented by this
        /// instance.
        /// </summary>
        /// <remarks>
        /// <see cref="MapValue.Count"/> is set to 0, but the capacity of
        /// the dictionary is not changed by this operation.
        /// </remarks>
        public void Clear()
        {
            mapValue.Clear();
        }

        /// <inheritdoc/>
        void ICollection<KeyValuePair<string, FieldValue>>.Add(
            KeyValuePair<string, FieldValue> item)
        {
            mapValue.Add(!(item.Value is null) ? item :
                new KeyValuePair<string, FieldValue>(item.Key, Null));
        }

        /// <inheritdoc/>
        bool ICollection<KeyValuePair<string, FieldValue>>.Contains(
            KeyValuePair<string, FieldValue> item)
        {
            return mapValue.Contains(item);
        }

        /// <inheritdoc/>
        void ICollection<KeyValuePair<string, FieldValue>>.CopyTo(
            KeyValuePair<string, FieldValue>[] array, int arrayIndex)
        {
            mapValue.CopyTo(array, arrayIndex);
        }

        /// <inheritdoc/>
        bool ICollection<KeyValuePair<string, FieldValue>>.Remove(
            KeyValuePair<string, FieldValue> item)
        {
            return mapValue.Remove(item);
        }

        /// <inheritdoc/>
        IEnumerator<KeyValuePair<string, FieldValue>>
            IEnumerable<KeyValuePair<string, FieldValue>>.GetEnumerator()
        {
            return mapValue.GetEnumerator();
        }

        /// <inheritdoc/>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return mapValue.GetEnumerator();
        }

        /// <inheritdoc/>
        public override void SerializeAsJson(Utf8JsonWriter writer,
            JsonOutputOptions options = null)
        {
            writer.WriteStartObject();
            foreach (var item in this)
            {
                writer.WritePropertyName(item.Key);
                item.Value.SerializeAsJson(writer, options);
            }
            writer.WriteEndObject();
        }

        internal override bool IsAtomic => false;

        internal override bool SupportsComparison => false;

        internal override bool QueryEquals(FieldValue other)
        {
            if (!(other is MapValue value))
            {
                return false;
            }

            if (Count != value.Count)
            {
                return false;
            }

            foreach (var key in Keys)
            {
                var fieldValue1 = this[key];
                if (!value.TryGetValue(key, out var fieldValue2) ||
                    !fieldValue1.QueryEquals(fieldValue2))
                {
                    return false;
                }
            }

            return true;
        }

        internal override int QueryCompareTotalOrder(FieldValue other,
            int nullRank) => other.DbType == DbType.Map
            ? QueryCompareMapValues(this, other.AsMapValue, nullRank)
            : (other.DbType == DbType.Array ? -1 : 1);

        internal override int QueryHashCode()
        {
            var code = 1;
            foreach (var kv in mapValue)
            {
                code = 31 * code + kv.Key.GetHashCode() +
                       kv.Value.QueryHashCode();
            }

            return code;
        }

        internal override long GetMemorySize()
        {
            // Size of MapValue object itself + dictionary overhead
            // overhead of the entries
            var result = GetObjectSize(IntPtr.Size) + DictionaryOverhead +
                         DictionaryEntryOverhead * mapValue.Count;

            foreach(var kv in mapValue)
            {
                result += GetStringSize(kv.Key) + kv.Value.GetMemorySize();
            }

            return result;
        }

    }

}
