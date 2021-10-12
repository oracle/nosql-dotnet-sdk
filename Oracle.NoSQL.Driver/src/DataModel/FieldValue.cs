/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using System.Text.Json;

    /// <include file="FieldValue.Doc.xml" path="FieldValue/*"/>
    public abstract partial class FieldValue : IComparable<FieldValue>,
        IEquatable<FieldValue>
    {
        internal static readonly EmptyValue Empty = EmptyValue.Instance;

        /// <summary>
        /// Singleton instance of <see cref="JsonNullValue"/>.
        /// </summary>
        public static readonly FieldValue JsonNull = JsonNullValue.Instance;

        /// <summary>
        /// Singleton instance of <see cref="NullValue"/>.
        /// </summary>
        public static readonly FieldValue Null = NullValue.Instance;

        /// <summary>
        /// Gets <see cref="DbType"/> of this instance which represents
        /// the type of this value.
        /// </summary>
        /// <value>
        /// The type of the <see cref="FieldValue"/> instance.
        /// </value>
        public abstract DbType DbType { get; }

        /// <summary>
        /// Gets the number of elements in the collection.
        /// </summary>
        /// <remarks>
        /// This property is valid only for instances of
        /// <see cref="ArrayValue"/>, <see cref="MapValue"/> and
        /// <see cref="RecordValue"/>.
        /// </remarks>
        /// <value>Number of elements in the collection.</value>
        /// <exception cref="NotSupportedException">If this instance is
        /// not <see cref="ArrayValue"/>, <see cref="MapValue"/> or
        /// <see cref="RecordValue"/>.</exception>
        /// <seealso cref="ArrayValue.Count"/>
        /// <seealso cref="MapValue.Count"/>
        public virtual int Count => throw new NotSupportedException(
            "\"Count\" property is not supported for type {GetType().Name}");

        /// <summary>
        /// Gets or sets the value associated with the specified key.
        /// </summary>
        /// <remarks>
        /// This operation is valid only for instances of
        /// <see cref="MapValue"/> and <see cref="RecordValue"/>.
        /// </remarks>
        /// <param name="key">The key of the value to get or set.</param>
        /// <returns>The value associated with the specified key. If the
        /// specified key is not found, a get operation throws a
        /// <see cref="KeyNotFoundException"/>, and a set operation
        /// creates a new element with the specified key.</returns>
        /// <exception cref="NotSupportedException">If this instance is not
        /// <see cref="MapValue"/> or <see cref="RecordValue"/>.</exception>
        /// <exception cref="KeyNotFoundException">If the property is
        /// retrieved and <paramref name="key"/> does not exist in this
        /// instance.</exception>
        /// <seealso cref="MapValue.this[string]"/>
        public virtual FieldValue this[string key]
        {
            get => throw new NotSupportedException(
                $"Cannot apply string get indexer to type {GetType().Name}");

            set => throw new NotSupportedException(
                $"Cannot apply string set indexer to type {GetType().Name}");
        }

        /// <summary>
        /// Gets or sets the element at the specified index.
        /// </summary>
        /// <remarks>
        /// This operation is valid only for instances of
        /// <see cref="ArrayValue"/> and <see cref="RecordValue"/> (as the
        /// latter represents dictionary with well-defined order of keys).
        /// </remarks>
        /// <param name="index">The zero-based index of the element to get or
        /// set.</param>
        /// <returns>The element at the specified index.</returns>
        /// <exception cref="NotSupportedException">If this instance is not
        /// <see cref="ArrayValue"/> or <see cref="RecordValue"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If
        /// <paramref name="index"/> is less than 0 or
        /// <paramref name="index"/> is equal to or greater than
        /// <see cref="FieldValue.Count"/></exception>
        /// <seealso cref="ArrayValue.this[int]"/>
        /// <seealso cref="RecordValue.this[int]"/>
        public virtual FieldValue this[int index]
        {
            get => throw new NotSupportedException(
                $"Cannot apply numeric get indexer to type {GetType().Name}");

            set => throw new NotSupportedException(
                $"Cannot apply numeric set indexer to type {GetType().Name}");
        }

        internal static FieldValue FromObject(object value)
        {
            if (value == null)
            {
                return Null;
            }
            Debug.Assert(value is FieldValue);
            return (FieldValue)value;
        }

        // Json serialization and deserialization functions

        /// <summary>
        /// Writes JSON representation of the value to the stream represented
        /// by <see cref="Utf8JsonWriter"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is an advanced method to optimize performance, using
        /// functionality in <see cref="System.Text.Json"/> namespace.  Most
        /// applications can use <see cref="FieldValue.ToJsonString"/>.
        /// See the remarks section of <see cref="FieldValue"/> on mappings
        /// between JSON types and subclasses of <see cref="FieldValue"/>.
        /// </para>
        /// <para>
        /// Note that this method writes the value at the current position of
        /// the stream represented by <see cref="Utf8JsonWriter"/> and it does
        /// not flush the stream.  The state of <see cref="Utf8JsonWriter"/>
        /// should be managed by the caller.
        /// </para>
        /// </remarks>
        /// <param name="writer">The writer to which the value represented by
        /// this instance is written.</param>
        /// <param name="options">(Optional) Options that allow limited
        /// customization of the output.  If not specified or <c>null</c>,
        /// appropriate defaults will be used as described in
        /// <see cref="JsonOutputOptions"/>.</param>
        /// <seealso cref="Utf8JsonWriter"/>
        /// <seealso cref="JsonOutputOptions"/>
        public abstract void SerializeAsJson(Utf8JsonWriter writer,
            JsonOutputOptions options = null);

        // This should probably be optimized somehow, perhaps by keeping a
        // pool of Utf8JsonWriter objects and MemoryStream objects so that
        // they can be reused.

        /// <summary>
        /// Returns JSON representation of the value.
        /// </summary>
        /// <remarks>
        /// See sections <em>JSON Mappings</em> and <em>JSON Conversions</em>
        /// in the remarks section of <see cref="FieldValue"/> on details of
        /// how different data types and subclasses of
        /// <see cref="FieldValue"/> are represented in JSON.
        /// </remarks>
        /// <param name="options">(Optional) Options that allow limited
        /// customization of the output.  If not specified or <c>null</c>,
        /// appropriate defaults will be used as described in
        /// <see cref="JsonOutputOptions"/>.</param>
        /// <returns>JSON string representing the value of this instance.
        /// </returns>
        /// <seealso cref="JsonOutputOptions"/>
        public string ToJsonString(JsonOutputOptions options = null)
        {
            var jsonOptions = options == null
                ? default
                : new JsonWriterOptions
                {
                    Indented = options.Indented
                };

            using var stream = new MemoryStream();
            using var writer = new Utf8JsonWriter(stream, jsonOptions);

            SerializeAsJson(writer, options);
            writer.Flush();
            return Encoding.UTF8.GetString(stream.GetBuffer(), 0,
                (int)stream.Length);
        }

        /// <summary>
        /// Creates <see cref="FieldValue"/> instance from JSON data
        /// represented by <see cref="Utf8JsonReader"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is an advanced method to optimize performance, using
        /// functionality in <see cref="System.Text.Json"/> namespace.  Most
        /// applications can use <see cref="FieldValue.FromJsonString"/>.
        /// See the remarks section of <see cref="FieldValue"/> on mappings
        /// between JSON types and subclasses of <see cref="FieldValue"/>.
        /// </para>
        /// <para>
        /// Note that this method read the value from the current position of
        /// represented by <see cref="Utf8JsonReader"/>.  The state of
        /// <see cref="Utf8JsonReader"/> should be managed by the caller.
        /// </para>
        /// </remarks>
        /// <param name="reader">The reader from which the value is read.
        /// </param>
        /// <param name="options">(Optional) Options that allow limited
        /// customization of the deserialization process.  If not specified or
        /// <c>null</c>, appropriate defaults will be used as described in
        /// <see cref="JsonInputOptions"/>.</param>
        /// <returns><see cref="FieldValue"/> instance representing JSON data
        /// read.</returns>
        /// <exception cref="ArgumentException"><paramref name="options"/> has
        /// invalid values.</exception>
        /// <exception cref="JsonException"><paramref name="reader"/> has
        /// invalid JSON data.</exception>
        /// <seealso cref="Utf8JsonReader"/>
        /// <seealso cref="JsonInputOptions"/>
        public static FieldValue DeserializeFromJson(
            ref Utf8JsonReader reader, JsonInputOptions options = null)
        {
            return DeserializeFromJson(ref reader, options, false);
        }

        /// <summary>
        /// Creates <see cref="FieldValue"/> instance from JSON string.
        /// </summary>
        /// <remarks>
        /// See sections <em>JSON Mappings</em> and <em>JSON Conversions</em>
        /// in the remarks section of <see cref="FieldValue"/> on details of
        /// how different data types and subclasses of
        /// <see cref="FieldValue"/> are represented in JSON.
        /// </remarks>
        /// <param name="json">JSON string</param>
        /// <param name="options">(Optional) Options that allow limited
        /// customization of the deserialization process.  If not specified or
        /// <c>null</c>,  appropriate defaults will be used as described in
        /// <see cref="JsonInputOptions"/>.</param>
        /// <returns><see cref="FieldValue"/> instance representing JSON data
        /// read.</returns>
        /// <exception cref="ArgumentException"><paramref name="options"/> has
        /// invalid values.</exception>
        /// <exception cref="JsonParseException"><paramref name="json"/> has
        /// invalid JSON data.</exception>
        /// <seealso cref="JsonInputOptions"/>
        public static FieldValue FromJsonString(string json,
            JsonInputOptions options = null)
        {
            if (json == null)
            {
                return JsonNull;
            }

            var bytes = Encoding.UTF8.GetBytes(json);
            var jsonOptions = options == null
                ? default
                : new JsonReaderOptions()
                {
                    AllowTrailingCommas = options.AllowTrailingCommas,
                    CommentHandling = options.AllowComments
                        ? JsonCommentHandling.Skip :
                        JsonCommentHandling.Disallow
                };

            var reader = new Utf8JsonReader(bytes, jsonOptions);
            try
            {
                return DeserializeFromJson(ref reader, options);
            }
            catch (JsonException ex)
            {
                throw new JsonParseException(ex);
            }
        }

    }

}
