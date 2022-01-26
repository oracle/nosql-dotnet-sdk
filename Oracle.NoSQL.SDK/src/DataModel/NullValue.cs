/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.Text.Json;

    /// <summary>
    /// Represents SQL NULL value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// SQL NULL is a special value that is used to indicate the fact that
    /// the value of the field is unknown or inapplicable. SQL NULL may be a
    /// value of the field of any data type.  Note that SQL NULL is distinct
    /// from JSON NULL which is a value of JSON type <em>NULL</em>.  See
    /// <see cref="JsonNullValue"/> for more information.
    /// </para>
    /// <para>
    /// The only value of this class is <see cref="FieldValue.Null"/>, which
    /// is the only immutable singleton instance of this class.  No other
    /// values are instantiated.
    /// </para>
    /// <para>
    /// When serialized to JSON (see <see cref="FieldValue.ToJsonString"/>),
    /// this value becomes <em>null</em> value in JSON, thus indistinguishable
    /// from <see cref="JsonNullValue"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="FieldValue"/>
    /// <seealso cref="JsonNullValue"/>
    public class NullValue : FieldValue
    {
        private NullValue()
        {
        }

        internal static readonly NullValue Instance = new NullValue();

        /// <inheritdoc cref="FieldValue.DbType" path="summary"/>
        /// <value>
        /// <see cref="SDK.DbType.Null"/>
        /// </value>
        public override DbType DbType => DbType.Null;

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
                    return 0;
                case DbType.JsonNull: case DbType.Empty:
                    return 1;
                default:
                    return other.SupportsComparison ? nullRank :
                        throw ComparisonNotSupported(other);
            }
        }

        internal override bool QueryEquals(FieldValue other)
        {
            return other.DbType == DbType.Null;
        }

        internal override int QueryHashCode() => int.MaxValue;

        internal override long GetMemorySize() => 0;
    }
}
