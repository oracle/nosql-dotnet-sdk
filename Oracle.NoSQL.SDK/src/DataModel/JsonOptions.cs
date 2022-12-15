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
    using System.Text.Json;

    /// <summary>
    /// Represents options passed to <see cref="FieldValue.ToJsonString"/> and
    /// <see cref="FieldValue.SerializeAsJson"/>.
    /// </summary>
    /// <remarks>
    /// These options allow you to customize how <see cref="FieldValue"/>
    /// instances are represented in JSON.  For properties not specified or
    /// <c>null</c>, appropriate defaults will be used as indicated below.
    /// </remarks>
    /// <seealso cref="FieldValue.ToJsonString"/>
    /// <seealso cref="FieldValue.SerializeAsJson"/>
    public class JsonOutputOptions
    {
        /// <summary>
        /// Default date and time format string used to represent
        /// <see cref="DateTime"/> values in JSON.
        /// </summary>
        /// <remarks>
        /// This format string is in ISO8601 format that is always represented
        /// in UTC time zone.  The fractions of a second are displayed if not
        /// 0.
        /// </remarks>
        public const string DefaultDateTimeFormatString =
            "yyyy-MM-ddTHH:mm:ss.FFFFFFFZ";

        /// <summary>
        /// Gets or sets a value indicating whether the JSON output should be
        /// formatted.
        /// </summary>
        /// <remarks>
        /// Formatting includes indenting nested JSON tokens, adding new
        /// lines, and adding white space between property names and values.
        /// This option does not apply to
        /// <see cref="FieldValue.SerializeAsJson"/> (instead use
        /// <see cref="JsonWriterOptions"/> when creating
        /// <see cref="Utf8JsonWriter"/>).
        /// </remarks>
        /// <value>
        /// <c>true</c> to format JSON output, <c>false</c> to write without
        /// any extra white space. The default is <c>false</c>.
        /// </value>
        public bool Indented { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to output
        /// <see cref="DateTime"/> values as a number of milliseconds since
        /// the Unix Epoch.
        /// </summary>
        /// <remarks>
        /// By default, <see cref="DateTime"/> values represented by
        /// <see cref="TimestampValue"/> instances will be represented in JSON
        /// as ISO8601-formatted strings.  This option allows representing
        /// them instead as a number equal to the number of milliseconds since
        /// the Unix Epoch, 00:00:00 UTC, January 1, 1970.  See
        /// <see cref="TimestampValue.ToInt64"/> for more information.
        /// </remarks>
        /// <value>
        /// <c>true</c> to output <see cref="DateTime"/> values as a number of
        /// milliseconds since the Unix Epoch, <c>false</c> to output
        /// <see cref="DateTime"/> values as strings.  The default is
        /// <c>false</c>.
        /// </value>
        /// <seealso cref="TimestampValue"/>
        public bool DateTimeAsMillis { get; set; }

        /// <summary>
        /// Gets or sets a date and time format string to represent
        /// <see cref="DateTime"/> values in JSON.
        /// </summary>
        /// <remarks>
        /// This option specifies the date and time format string to use to
        /// represent <see cref="DateTime"/> values in JSON.  It is passed as
        /// an argument to <see cref="DateTime.ToString(string)"/> method.
        /// The default is The default is
        /// <see cref="DefaultDateTimeFormatString"/>.  If set to <c>null</c>,
        /// the general date and time format specifier 'G' will be used as if
        /// calling <see cref="DateTime.ToString()"/>.  If
        /// <see cref="DateTimeAsMillis"/> is <c>true</c>, this option has no
        /// effect.
        /// </remarks>
        /// <value>
        /// Date and time format string.  The default is
        /// <see cref="DefaultDateTimeFormatString"/>.
        /// </value>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/standard/base-types/standard-date-and-time-format-strings">
        /// Standard date and time format strings
        /// </seealso>
        /// <seealso href="https://docs.microsoft.com/en-us/dotnet/standard/base-types/custom-date-and-time-format-strings">
        /// Custom date and time format strings
        /// </seealso>
        public string DateTimeFormatString { get; set; } =
            DefaultDateTimeFormatString;
    }

    /// <summary>
    /// Represents options passed to <see cref="FieldValue.FromJsonString"/>
    /// and <see cref="FieldValue.DeserializeFromJson(ref Utf8JsonReader, JsonInputOptions)"/>.
    /// </summary>
    /// <remarks>
    /// These options allow you to customize how the JSON input is parsed to
    /// create <see cref="FieldValue"/> instances.  For properties not
    /// specified or <c>null</c>, appropriate defaults will be used as
    /// indicated below.
    /// </remarks>
    /// <seealso cref="FieldValue.FromJsonString"/>
    /// <seealso cref="FieldValue.DeserializeFromJson(ref Utf8JsonReader, JsonInputOptions)"/>
    public class JsonInputOptions
    {
        /// <summary>
        /// Gets or sets a value indicating whether comments are allowed in
        /// the JSON input.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If allowed, comments follow the C# syntax, using <em>//</em> for
        /// single-line comments and <em>/* */</em> for single-line and
        /// multi-line comments.  If comments are not allowed and comments
        /// are encountered in JSON input,
        /// <see cref="FieldValue.FromJsonString"/> will throw
        /// <see cref="JsonParseException"/>.
        /// </para>
        /// <para>
        /// This option does not apply to
        /// <see cref="FieldValue.DeserializeFromJson(ref Utf8JsonReader, JsonInputOptions)"/>
        /// (instead use <see cref="JsonReaderOptions"/> when creating
        /// <see cref="Utf8JsonReader"/>).
        /// </para>
        /// </remarks>
        /// <example>
        /// JSON with comments.
        /// <code>
        /// {
        ///     "key1": "string value", // single-line comment
        ///     "key2": 10, /* another single-line comment */
        ///     /*
        ///         Multi-line
        ///        comment.
        ///     */
        ///     "key3": null
        /// }
        /// </code>
        /// </example>
        /// <value>
        /// <c>true</c> to allow comments, <c>false</c> to disallow comments.
        /// The default is <c>false</c>.
        /// </value>
        public bool AllowComments { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether trailing commas are
        /// allowed in the JSON input.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Trailing commas are extra commas at the end of a list of JSON
        /// values in an object or array. If allowed, they are ignored.
        /// If not allowed and they are encountered in JSON input,
        /// <see cref="FieldValue.FromJsonString"/> will throw
        /// <see cref="JsonParseException"/>.
        /// </para>
        /// <para>
        /// This option does not apply to
        /// <see cref="FieldValue.DeserializeFromJson(ref Utf8JsonReader, JsonInputOptions)"/>
        /// (instead use <see cref="JsonReaderOptions"/> when creating
        /// <see cref="Utf8JsonReader"/>).
        /// </para>
        /// </remarks>
        /// <example>
        /// JSON with trailing commas.
        /// <code>
        /// {
        ///     "key1": "string value",
        ///     "key2": [ 1, 2, 3, 4, 5 ],
        ///     "key2": true,
        /// }
        /// </code>
        /// </example>
        /// <value>
        /// <c>true</c> to allow trailing commas, <c>false</c> to disallow
        /// trailing commas. The default is <c>false</c>.
        /// </value>
        public bool AllowTrailingCommas { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to prefer using
        /// <see cref="NumberValue"/> over <see cref="DoubleValue"/> to
        /// deserialize JSON numbers.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When deserializing JSON <em>Number</em> values, the whole numbers
        /// are usually instantiated as <see cref="IntegerValue"/> or
        /// <see cref="LongValue"/>.  By default, fractional numbers are
        /// instantiated as <see cref="DoubleValue"/>.  In some cases, you may
        /// prefer to use <see cref="NumberValue"/> instead, which stores
        /// <c>decimal</c> value.  <c>decimal</c> has bigger precision
        /// (although smaller range) than <c>double</c>.  In addition,
        /// <c>double</c> store its value in binary format and thus may not
        /// represent all decimal values precisely.
        /// </para>
        /// <para>
        /// If <see cref="PreferDecimal"/> is set to <c>true</c>, the
        /// fractional numbers will be deserialized as
        /// <see cref="NumberValue"/>, unless they are outside of the range of
        /// <see cref="Decimal"/> in which case <see cref="DoubleValue"/> will
        /// still be used.
        /// </para>
        /// <para>
        /// In addition to fractional numbers, the above also applies to whole
        /// numbers that are outside of the range of <c>long</c> type.
        /// </para>
        /// </remarks>
        /// <value>
        /// <c>true</c> to prefer using <see cref="NumberValue"/> over
        /// <see cref="DoubleValue"/> to deserialize fractional numbers as
        /// well as whole numbers outside of the range of <c>long</c> type,
        /// <c>false</c> to prefer using <see cref="DoubleValue"/>.  The
        /// default is <c>false</c>.
        /// </value>
        public bool PreferDecimal { get; set; }
    }

}
