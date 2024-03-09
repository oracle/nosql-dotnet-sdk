/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.Text.Json;

    /// <summary>
    /// Represents a JSON NULL value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Oracle NoSQL Database uses a special value JSON NULL, which represents
    /// a value of JSON type <em>NULL</em>.  JSON NULL may occur as a value
    /// of a field of data type <em>Json</em> or one of its sub-fields that
    /// has a value <em>null</em>.  JSON NULL value is different and separate
    /// from SQL NULL.  For example, if table <em>MyTable</em> has a field
    /// <em>info</em> of type JSON, a query such as
    /// <em>SELECT info.name from MyTable</em> will yield different results
    /// for records where the value of <em>info.name</em> is <em>null</em>
    /// (e.g. if the value of <em>info</em> is <em>{ "name": null }</em>) with
    /// the result being JSON NULL and for records where the value of the
    /// <em>info</em> field itself is NULL (SQL NULL), with result being a SQL
    /// NULL.  In addition, the <em>info</em> field itself may take values of
    /// SQL NULL or JSON NULL which are distinct values, the latter being a
    /// value of a JSON type NULL (i.e.the value of <em>info</em> is JSON
    /// value <em>null</em>).  For more details, please see the Oracle NoSQL
    /// Database SQL Reference Guide.
    /// </para>
    /// <para>
    /// The only value of this class is <see cref="FieldValue.JsonNull"/>,
    /// which is the only immutable singleton instance of this class.  No
    /// other values are instantiated.
    /// </para>
    /// <para>
    /// On output, JSON NULL values returned by the driver will always be
    /// <see cref="FieldValue.JsonNull"/>.  On input, when using
    /// <see cref="MapValue"/> to create a value for a JSON field, it is
    /// acceptable to use either <see cref="FieldValue.JsonNull"/> or
    /// <see cref="FieldValue.Null"/> as value of the sub-field inside the
    /// <see cref="MapValue"/>.  However, <see cref="FieldValue.JsonNull"/>
    /// and <see cref="FieldValue.Null"/> will be treated as different values
    /// (JSON NULL and SQL NULL) if used as a value for the whole JSON field
    /// as described above.
    /// </para>
    /// </remarks>
    /// <example>
    /// Instantiating the row value with fields <em>id</em> and <em>info</em>
    /// where <em>info</em> is field of data type <em>Json</em> and has value
    /// of <em>{ "name": null }</em>.
    /// <code>
    /// var row = new MapValue
    /// {
    ///     ["id"] = 1000,
    ///     ["info"] = new MapValue
    ///     {
    ///         ["name"] = FieldValue.JsonNull
    ///     }
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="FieldValue"/>
    /// <seealso cref="NullValue"/>
    public class JsonNullValue : FieldValue
    {
        private JsonNullValue()
        {
        }

        internal static readonly JsonNullValue Instance = new JsonNullValue();

        /// <inheritdoc cref="FieldValue.DbType" path="summary"/>
        /// <value>
        /// <see cref="SDK.DbType.JsonNull"/>
        /// </value>
        public override DbType DbType => DbType.JsonNull;

        /// <inheritdoc/>
        public override void SerializeAsJson(Utf8JsonWriter writer,
            JsonOutputOptions options = null)
        {
            writer.WriteNullValue();
        }

        internal override bool IsSpecial => true;

        internal override int QueryCompare(FieldValue other, int nullRank)
        {
            switch (other.DbType)
            {
                case DbType.Null:
                    return -1;
                case DbType.JsonNull:
                    return 0;
                case DbType.Empty:
                    return 1;
                default:
                    return other.SupportsComparison ? nullRank :
                        throw ComparisonNotSupported(other);
            }
        }

        internal override bool QueryEquals(FieldValue other)
        {
            return other.DbType == DbType.JsonNull;
        }

        internal override int QueryHashCode() => int.MinValue;

        internal override long GetMemorySize() => 0;
    }
}
