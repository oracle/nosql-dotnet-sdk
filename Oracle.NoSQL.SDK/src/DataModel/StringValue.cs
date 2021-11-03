/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Text.Json;
    using static SizeOf;

    /// <summary>
    /// Represents a string value.
    /// </summary>
    /// <remarks>
    /// This class is used to represent values of NoSQL data types
    /// <em>String</em> and <em>Enum</em>.  This value is represented
    /// by a C# type <see cref="String"/>.
    /// </remarks>
    /// <seealso cref="FieldValue"/>
    public class StringValue : FieldValue
    {
        private readonly string value;

        /// <summary>
        /// Initializes a new instance of the <see cref="StringValue"/> with
        /// the specified <c>string</c> value.
        /// </summary>
        /// <param name="value">The value which this instance will represent.
        /// </param>
        /// <exception cref="ArgumentNullException">If
        /// <paramref name="value"/> is <c>null</c>.</exception>
        public StringValue(string value)
        {
            this.value = value ?? throw new ArgumentNullException(
                nameof(value),
                "Argument to StringValue constructor cannot be null");
        }

        /// <inheritdoc cref="FieldValue.DbType" path="summary"/>
        /// <value>
        /// <see cref="SDK.DbType.String"/>
        /// </value>
        public override DbType DbType => DbType.String;

        /// <summary>
        /// Gets the value of this instance as string.
        /// </summary>
        /// <value>
        /// The <c>string</c> value that this instance represents.
        /// </value>
        public override string AsString => value;

        /// <summary>
        /// Converts the value represented by this instance to a byte array.
        /// </summary>
        /// <remarks>
        /// This method is valid only if the string represented by this value
        /// represents a Base64-encoded binary value.  It performs the same
        /// conversion as <see cref="Convert.FromBase64String(string)"/>.
        /// </remarks>
        /// <returns>A byte array resulting from decoding a Base64-encoded
        /// value represented by this instance.
        /// </returns>
        /// <exception cref="FormatException">The value represented by this
        /// instance is not a valid Base64 string.</exception>
        /// <seealso cref="Convert.ToBoolean(string)"/>
        public override byte[] ToByteArray() =>
            Convert.FromBase64String(value);

        /// <summary>
        /// Converts the value represented by this instance to a boolean.
        /// </summary>
        /// <remarks>
        /// This method performs the same conversion as
        /// <see cref="Convert.ToBoolean(string)"/>.
        /// </remarks>
        /// <returns><c>true</c> if value equals "true", or <c>false</c> if
        /// value equals <c>false</c>.  The comparison is case-insensitive and
        /// ignores leading and trailing whitespace.
        /// </returns>
        /// <exception cref="FormatException">The value is neither "true" or
        /// "false".</exception>
        /// <seealso cref="Convert.ToBoolean(string)"/>
        public override bool ToBoolean() => Convert.ToBoolean(value);

        /// <summary>
        /// Converts the value represented by this instance to a 32-bit sighed
        /// integer.
        /// </summary>
        /// <remarks>
        /// This method performs the same conversion as
        /// <see cref="Convert.ToInt32(string)"/>.
        /// </remarks>
        /// <returns>A 32-bit signed integer equivalent to the number
        /// represented by the string represented by this instance.
        /// </returns>
        /// <exception cref="FormatException">The string represented by this
        /// instance does not represent an integer.</exception>
        /// <exception cref="OverflowException">If the string represented by
        /// this instance represents value less than
        /// <see cref="Int32.MinValue"/> or greater than
        /// <see cref="Int32.MaxValue"/></exception>
        /// <seealso cref="Convert.ToInt32(string)"/>
        public override int ToInt32() => Convert.ToInt32(value);

        /// <summary>
        /// Converts the value represented by this instance to a 64-bit sighed
        /// integer.
        /// </summary>
        /// <remarks>
        /// This method performs the same conversion as
        /// <see cref="Convert.ToInt64(string)"/>.
        /// </remarks>
        /// <returns>A 64-bit signed integer equivalent to the number
        /// represented by the string represented by this instance.
        /// </returns>
        /// <exception cref="FormatException">The string represented by this
        /// instance does not represent an integer.</exception>
        /// <exception cref="OverflowException">If the string represented by
        /// this instance represents value less than
        /// <see cref="Int64.MinValue"/> or greater than
        /// <see cref="Int64.MaxValue"/></exception>
        /// <seealso cref="Convert.ToInt64(string)"/>
        public override long ToInt64() => Convert.ToInt64(value);

        /// <summary>
        /// Converts the value represented by this instance to a double
        /// precision floating point number.
        /// </summary>
        /// <remarks>
        /// This method performs the same conversion as
        /// <see cref="Convert.ToDouble(string)"/>.
        /// </remarks>
        /// <returns>A double precision floating point number equivalent to
        /// the number represented by the string represented by this instance.
        /// </returns>
        /// <exception cref="FormatException">The string represented by this
        /// instance does not represent a number.</exception>
        /// <exception cref="OverflowException">If the string represented by
        /// this instance represents value less than
        /// <see cref="Double.MinValue"/> or greater than
        /// <see cref="Double.MaxValue"/></exception>
        /// <seealso cref="Convert.ToDouble(string)"/>
        public override double ToDouble() => Convert.ToDouble(value);

        /// <summary>
        /// Converts the value represented by this instance to a decimal
        /// number.
        /// </summary>
        /// <remarks>
        /// This method performs the same conversion as
        /// <see cref="Convert.ToDecimal(string)"/>.
        /// </remarks>
        /// <returns>A decimal number equivalent to the number represented by
        /// the string represented by this instance.
        /// </returns>
        /// <exception cref="FormatException">The string represented by this
        /// instance does not represent a decimal number.</exception>
        /// <exception cref="OverflowException">If the string represented by
        /// this instance represents value less than
        /// <see cref="Decimal.MinValue"/> or greater than
        /// <see cref="Decimal.MaxValue"/></exception>
        /// <seealso cref="Convert.ToDecimal(string)"/>
        public override decimal ToDecimal() => Convert.ToDecimal(value);

        /// <summary>
        /// Converts the value represented by this instance to a date and time
        /// value.
        /// </summary>
        /// <remarks>
        /// This method performs the same conversion as
        /// <see cref="Convert.ToDateTime(string)"/>.
        /// </remarks>
        /// <returns>A date and time equivalent to the string represented by
        /// this instance.
        /// </returns>
        /// <exception cref="FormatException">The string represented by this
        /// instance is not a valid date and time string.
        /// </exception>
        /// <seealso cref="Convert.ToDateTime(string)"/>
        public override DateTime ToDateTime() => Convert.ToDateTime(value);

        /// <inheritdoc/>
        public override void SerializeAsJson(Utf8JsonWriter writer,
            JsonOutputOptions options = null)
        {
            writer.WriteStringValue(value);
        }

        internal override int QueryCompare(FieldValue other, int nullRank)
        {
            switch (other.DbType)
            {
                case DbType.String:
                    return string.Compare(AsString, other.AsString,
                        StringComparison.Ordinal);
                case DbType.Boolean:
                    return -1;
                case DbType.Null:
                case DbType.JsonNull:
                case DbType.Empty:
                    return -nullRank;
                default:
                    return other.SupportsComparison ? 1 :
                        throw ComparisonNotSupported(other);
            }
        }

        internal override bool QueryEquals(FieldValue other)
        {
            return other.DbType == DbType.String &&
                   AsString.Equals(other.AsString);
        }

        internal override int QueryHashCode() => value.GetHashCode();

        internal override long GetMemorySize() =>
            GetObjectSize(GetStringSize(value));
    }

}
