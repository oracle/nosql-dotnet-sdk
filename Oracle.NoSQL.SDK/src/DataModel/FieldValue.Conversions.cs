/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

    public abstract partial class FieldValue
    {
        /// <summary>
        /// Defines implicit conversion from <c>byte[]</c> value to
        /// <see cref="FieldValue"/> instance of subclass
        /// <see cref="BinaryValue"/>.
        /// </summary>
        /// <param name="binaryValue"><c>byte[]</c> value to implicitly
        /// convert.</param>
        /// <returns><see cref="BinaryValue"/> instance.</returns>
        /// <seealso cref="BinaryValue"/>
        public static implicit operator FieldValue(byte[] binaryValue) =>
            binaryValue != null ? new BinaryValue(binaryValue) :
                (FieldValue)Null;

        /// <summary>
        /// Defines implicit conversion from <c>bool</c> value to
        /// <see cref="FieldValue"/> instance of subclass
        /// <see cref="BooleanValue"/>.
        /// </summary>
        /// <param name="booleanValue"><c>bool</c> value to implicitly
        /// convert.</param>
        /// <returns><see cref="BooleanValue"/> instance.</returns>
        /// <seealso cref="BooleanValue"/>
        public static implicit operator FieldValue(bool booleanValue) =>
            booleanValue ? BooleanValue.True : BooleanValue.False;

        /// <summary>
        /// Defines implicit conversion from <c>double</c> value to
        /// <see cref="FieldValue"/> instance of subclass
        /// <see cref="DoubleValue"/>.
        /// </summary>
        /// <param name="doubleValue"><c>bool</c> value to implicitly
        /// convert.</param>
        /// <returns><see cref="DoubleValue"/> instance.</returns>
        /// <seealso cref="DoubleValue"/>
        public static implicit operator FieldValue(double doubleValue) =>
            new DoubleValue(doubleValue);

        /// <summary>
        /// Defines implicit conversion from <c>int</c> value to
        /// <see cref="FieldValue"/> instance of subclass
        /// <see cref="IntegerValue"/>.
        /// </summary>
        /// <param name="integerValue"><c>int</c> value to implicitly
        /// convert.</param>
        /// <returns><see cref="IntegerValue"/> instance.</returns>
        /// <seealso cref="IntegerValue"/>
        public static implicit operator FieldValue(int integerValue) =>
            new IntegerValue(integerValue);

        /// <summary>
        /// Defines implicit conversion from <c>long</c> value to
        /// <see cref="FieldValue"/> instance of subclass
        /// <see cref="LongValue"/>.
        /// </summary>
        /// <param name="longValue"><c>long</c> value to implicitly
        /// convert.</param>
        /// <returns><see cref="LongValue"/> instance.</returns>
        /// <seealso cref="LongValue"/>
        public static implicit operator FieldValue(long longValue) =>
            new LongValue(longValue);

        /// <summary>
        /// Defines implicit conversion from <see cref="String"/> value to
        /// <see cref="FieldValue"/> instance of subclass
        /// <see cref="StringValue"/>.
        /// </summary>
        /// <param name="stringValue"><see cref="String"/> value to implicitly
        /// convert.</param>
        /// <returns><see cref="StringValue"/> instance.</returns>
        /// <seealso cref="StringValue"/>
        public static implicit operator FieldValue(string stringValue) =>
            stringValue != null ? new StringValue(stringValue) :
                (FieldValue)Null;

        /// <summary>
        /// Defines implicit conversion from <see cref="DateTime"/> value to
        /// <see cref="FieldValue"/> instance of subclass
        /// <see cref="TimestampValue"/>.
        /// </summary>
        /// <param name="timestampValue"><see cref="String"/> value to
        /// implicitly convert.</param>
        /// <returns><see cref="TimestampValue"/> instance.</returns>
        /// <seealso cref="TimestampValue"/>
        public static implicit operator FieldValue(DateTime timestampValue) =>
            new TimestampValue(timestampValue);

        /// <summary>
        /// Defines implicit conversion from <c>decimal</c> value to
        /// <see cref="FieldValue"/> instance of subclass
        /// <see cref="NumberValue"/>.
        /// </summary>
        /// <param name="numberValue"><c>decimal</c> value to implicitly
        /// convert.</param>
        /// <returns><see cref="NumberValue"/> instance.</returns>
        /// <seealso cref="NumberValue"/>
        public static implicit operator FieldValue(decimal numberValue) =>
            new NumberValue(numberValue);

        /// <summary>
        /// Gets the value of this instance as byte array.
        /// </summary>
        /// <remarks>
        /// Valid only if this instance is <see cref="BinaryValue"/>.
        /// </remarks>
        /// <value><c>byte[]</c> value of this instance.</value>
        /// <exception cref="InvalidCastException">If this instance
        /// is not <see cref="BinaryValue"/>.</exception>
        /// <seealso cref="BinaryValue"/>
        public virtual byte[] AsByteArray =>
            throw CannotCastTo(typeof(byte[]));

        /// <summary>
        /// Gets the value of this instance as boolean.
        /// </summary>
        /// <remarks>
        /// Valid only if this instance is <see cref="BooleanValue"/>.
        /// </remarks>
        /// <value><c>bool</c> value of this instance.</value>
        /// <exception cref="InvalidCastException">If this instance
        /// is not <see cref="BooleanValue"/>.</exception>
        /// <seealso cref="BooleanValue"/>
        public virtual bool AsBoolean => throw CannotCastTo(typeof(bool));

        /// <summary>
        /// Gets the value of this instance as double precision floating
        /// point number.
        /// </summary>
        /// <remarks>
        /// Valid only if this instance is <see cref="DoubleValue"/>.
        /// </remarks>
        /// <value><c>double</c> value of this instance.</value>
        /// <exception cref="InvalidCastException">If this instance
        /// is not <see cref="DoubleValue"/>.</exception>
        /// <seealso cref="DoubleValue"/>
        public virtual double AsDouble => throw CannotCastTo(typeof(double));

        /// <summary>
        /// Gets the value of this instance as 32-bit signed integer.
        /// </summary>
        /// <remarks>
        /// Valid only if this instance is <see cref="IntegerValue"/>.
        /// </remarks>
        /// <value><c>int</c> value of this instance.</value>
        /// <exception cref="InvalidCastException">If this instance
        /// is not <see cref="IntegerValue"/>.</exception>
        /// <seealso cref="IntegerValue"/>
        public virtual int AsInt32 => throw CannotCastTo(typeof(int));

        /// <summary>
        /// Gets the value of this instance as 64-bit signed integer.
        /// </summary>
        /// <remarks>
        /// Valid only if this instance is <see cref="LongValue"/>.
        /// </remarks>
        /// <value><c>long</c> value of this instance.</value>
        /// <exception cref="InvalidCastException">If this instance
        /// is not <see cref="LongValue"/>.</exception>
        /// <seealso cref="LongValue"/>
        public virtual long AsInt64 => throw CannotCastTo(typeof(long));

        /// <summary>
        /// Gets the value of this instance as string.
        /// </summary>
        /// <remarks>
        /// Valid only if this instance is <see cref="StringValue"/>.
        /// </remarks>
        /// <value><c>string</c> value of this instance.</value>
        /// <exception cref="InvalidCastException">If this instance
        /// is not <see cref="StringValue"/>.</exception>
        /// <seealso cref="StringValue"/>
        public virtual string AsString => throw CannotCastTo(typeof(string));

        /// <summary>
        /// Gets the value of this instance as date and time.
        /// </summary>
        /// <remarks>
        /// Valid only if this instance is <see cref="TimestampValue"/>.
        /// </remarks>
        /// <value><see cref="DateTime"/> value.</value>
        /// <exception cref="InvalidCastException">If this instance
        /// is not <see cref="TimestampValue"/>.</exception>
        /// <seealso cref="TimestampValue"/>
        public virtual DateTime AsDateTime =>
            throw CannotCastTo(typeof(DateTime));

        /// <summary>
        /// Gets the value of this instance as decimal number.
        /// </summary>
        /// <remarks>
        /// Valid only if this instance is <see cref="NumberValue"/>.
        /// </remarks>
        /// <value><c>decimal</c> value.</value>
        /// <exception cref="InvalidCastException">If this instance
        /// is not <see cref="NumberValue"/>.</exception>
        /// <seealso cref="NumberValue"/>
        public virtual decimal AsDecimal =>
            throw CannotCastTo(typeof(decimal));

        /// <summary>
        /// Gets the value of this instance as <see cref="ArrayValue"/>.
        /// </summary>
        /// <remarks>
        /// This is a convenience cast.  Valid only if this instance is
        /// <see cref="ArrayValue"/>.
        /// </remarks>
        /// <value><see cref="ArrayValue"/> instance.</value>
        /// <exception cref="InvalidCastException">If this instance
        /// is not <see cref="ArrayValue"/>.</exception>
        /// <seealso cref="ArrayValue"/>
        public virtual ArrayValue AsArrayValue => (ArrayValue)this;

        /// <summary>
        /// Gets the value of this instance as <see cref="MapValue"/>.
        /// </summary>
        /// <remarks>
        /// This is a convenience cast.  Valid only if this instance is
        /// <see cref="MapValue"/> or <see cref="RecordValue"/>.
        /// </remarks>
        /// <value><see cref="MapValue"/> instance.</value>
        /// <exception cref="InvalidCastException">If this instance
        /// is not <see cref="MapValue"/>.</exception>
        /// <seealso cref="MapValue"/>
        public virtual MapValue AsMapValue => (MapValue)this;

        /// <summary>
        /// Gets the value of this instance as <see cref="RecordValue"/>.
        /// </summary>
        /// <remarks>
        /// This is a convenience cast.  Valid only if this instance is
        /// <see cref="RecordValue"/>.
        /// </remarks>
        /// <value><see cref="RecordValue"/> instance.</value>
        /// <exception cref="InvalidCastException">If this instance
        /// is not <see cref="RecordValue"/>.</exception>
        /// <seealso cref="RecordValue"/>
        public virtual RecordValue AsRecordValue => (RecordValue)this;

        //For users who prefer to use explicit conversion operators instead of
        //methods, we should provide them.  Note that using implicit
        //conversions is not appropriate here, since converting to a wrong
        //type will throw exception.

        /// <summary>
        /// Defines an explicit conversion from <see cref="FieldValue"/>
        /// instance to byte array.
        /// </summary>
        /// <remarks>
        /// This operation is the same as
        /// <see cref="FieldValue.AsByteArray"/>.  Valid only if this instance
        /// is <see cref="BinaryValue"/>.
        /// </remarks>
        /// <param name="value">A <see cref="FieldValue"/> instance to
        /// explicitly convert.</param>
        /// <returns>Value of the instance as <c>byte[]</c>.</returns>
        /// <exception cref="ArgumentNullException">If
        /// <paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidCastException">If <paramref name="value"/>
        /// is not <see cref="BinaryValue"/>.</exception>
        /// <seealso cref="FieldValue.AsByteArray"/>
        public static explicit operator byte[](FieldValue value) =>
            value != null ?
                value.AsByteArray :
                throw new ArgumentNullException(nameof(value));

        /// <summary>
        /// Defines an explicit conversion from <see cref="FieldValue"/>
        /// instance to boolean.
        /// </summary>
        /// <remarks>
        /// This operation is the same as
        /// <see cref="FieldValue.AsBoolean"/>.  Valid only if this instance
        /// is <see cref="BooleanValue"/>.
        /// </remarks>
        /// <param name="value">A <see cref="FieldValue"/> instance to
        /// explicitly convert.</param>
        /// <returns>Value of the instance as <c>bool</c>.</returns>
        /// <exception cref="ArgumentNullException">If
        /// <paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidCastException">If <paramref name="value"/>
        /// is not <see cref="BooleanValue"/>.</exception>
        /// <seealso cref="FieldValue.AsBoolean"/>
        public static explicit operator bool(FieldValue value) =>
            value != null ?
                value.AsBoolean :
                throw new ArgumentNullException(nameof(value));

        /// <summary>
        /// Defines an explicit conversion from <see cref="FieldValue"/>
        /// instance to double precision floating point number.
        /// </summary>
        /// <remarks>
        /// This operation is the same as
        /// <see cref="FieldValue.AsDouble"/>.  Valid only if this instance
        /// is <see cref="DoubleValue"/>.
        /// </remarks>
        /// <param name="value">A <see cref="FieldValue"/> instance to
        /// explicitly convert.</param>
        /// <returns>Value of the instance as <c>double</c>.</returns>
        /// <exception cref="ArgumentNullException">If
        /// <paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidCastException">If <paramref name="value"/>
        /// is not <see cref="DoubleValue"/>.</exception>
        /// <seealso cref="FieldValue.AsDouble"/>
        public static explicit operator double(FieldValue value) =>
            value != null ?
                value.AsDouble :
                throw new ArgumentNullException(nameof(value));

        /// <summary>
        /// Defines an explicit conversion from <see cref="FieldValue"/>
        /// instance to 32-bit signed integer.
        /// </summary>
        /// <remarks>
        /// This operation is the same as
        /// <see cref="FieldValue.AsInt32"/>.  Valid only if this instance
        /// is <see cref="IntegerValue"/>.
        /// </remarks>
        /// <param name="value">A <see cref="FieldValue"/> instance to
        /// explicitly convert.</param>
        /// <returns>Value of the instance as <c>int</c>.</returns>
        /// <exception cref="ArgumentNullException">If
        /// <paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidCastException">If <paramref name="value"/>
        /// is not <see cref="IntegerValue"/>.</exception>
        /// <seealso cref="FieldValue.AsInt32"/>
        public static explicit operator int(FieldValue value) =>
            value != null ?
                value.AsInt32 :
                throw new ArgumentNullException(nameof(value));

        /// <summary>
        /// Defines an explicit conversion from <see cref="FieldValue"/>
        /// instance to 64-bit signed integer.
        /// </summary>
        /// <remarks>
        /// This operation is the same as
        /// <see cref="FieldValue.AsInt64"/>.  Valid only if this instance
        /// is <see cref="LongValue"/>.
        /// </remarks>
        /// <param name="value">A <see cref="FieldValue"/> instance to
        /// explicitly convert.</param>
        /// <returns>Value of the instance as <c>long</c>.</returns>
        /// <exception cref="ArgumentNullException">If
        /// <paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidCastException">If <paramref name="value"/>
        /// is not <see cref="LongValue"/>.</exception>
        /// <seealso cref="FieldValue.AsInt64"/>
        public static explicit operator long(FieldValue value) =>
            value != null ?
                value.AsInt64 :
                throw new ArgumentNullException(nameof(value));

        /// <summary>
        /// Defines an explicit conversion from <see cref="FieldValue"/>
        /// instance to string.
        /// </summary>
        /// <remarks>
        /// This operation is the same as
        /// <see cref="FieldValue.AsString"/>.  Valid only if this instance
        /// is <see cref="StringValue"/>.
        /// </remarks>
        /// <param name="value">A <see cref="FieldValue"/> instance to
        /// explicitly convert.</param>
        /// <returns>Value of the instance as <c>string</c>.</returns>
        /// <exception cref="ArgumentNullException">If
        /// <paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidCastException">If <paramref name="value"/>
        /// is not <see cref="StringValue"/>.</exception>
        /// <seealso cref="FieldValue.AsString"/>
        public static explicit operator string(FieldValue value) =>
            value != null ?
                value.AsString :
                throw new ArgumentNullException(nameof(value));

        /// <summary>
        /// Defines an explicit conversion from <see cref="FieldValue"/>
        /// instance to date and time.
        /// </summary>
        /// <remarks>
        /// This operation is the same as
        /// <see cref="FieldValue.AsDateTime"/>.  Valid only if this instance
        /// is <see cref="TimestampValue"/>.
        /// </remarks>
        /// <param name="value">A <see cref="FieldValue"/> instance to
        /// explicitly convert.</param>
        /// <returns>Value of the instance as <see cref="DateTime"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">If
        /// <paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidCastException">If <paramref name="value"/>
        /// is not <see cref="TimestampValue"/>.</exception>
        /// <seealso cref="FieldValue.AsDateTime"/>
        public static explicit operator DateTime(FieldValue value) =>
            value != null ?
                value.AsDateTime :
                throw new ArgumentNullException(nameof(value));

        /// <summary>
        /// Defines an explicit conversion from <see cref="FieldValue"/>
        /// instance to decimal number.
        /// </summary>
        /// <remarks>
        /// This operation is the same as
        /// <see cref="FieldValue.AsDecimal"/>.  Valid only if this instance
        /// is <see cref="NumberValue"/>.
        /// </remarks>
        /// <param name="value">A <see cref="FieldValue"/> instance to
        /// explicitly convert.</param>
        /// <returns>Value of the instance as <c>decimal</c>.</returns>
        /// <exception cref="ArgumentNullException">If
        /// <paramref name="value"/> is <c>null</c>.</exception>
        /// <exception cref="InvalidCastException">If <paramref name="value"/>
        /// is not <see cref="NumberValue"/>.</exception>
        /// <seealso cref="FieldValue.AsDecimal"/>
        public static explicit operator decimal(FieldValue value) =>
            value != null ?
                value.AsDecimal :
                throw new ArgumentNullException(nameof(value));

        /// <summary>
        /// Converts value of this instance to byte array.
        /// </summary>
        /// <remarks>
        /// This conversion is valid only for instances of
        /// <see cref="BinaryValue"/> or instances of
        /// <see cref="StringValue"/> containing Base64-encoded binary
        /// data.
        /// </remarks>
        /// <returns>Value of this instance as byte array.</returns>
        /// <exception cref="InvalidCastException">If this instance is not
        /// <see cref="BinaryValue"/> or <see cref="StringValue"/>.
        /// </exception>
        /// <exception cref="FormatException">If this instance is
        /// <see cref="StringValue"/> and contains invalid Base64 string.
        /// </exception>
        /// <seealso cref="StringValue.ToByteArray"/>
        public virtual byte[] ToByteArray() => AsByteArray;

        /// <summary>
        /// Converts value of this instance to boolean.
        /// </summary>
        /// <remarks>
        /// This conversion is valid only for instances of
        /// <see cref="BooleanValue"/> or instances of
        /// <see cref="StringValue"/> with values "true" or "false", ignoring
        /// case and leading/trailing whitespace (see
        /// <see cref="Convert.ToBoolean(String)"/>).
        /// </remarks>
        /// <returns>Value of this instance as boolean.</returns>
        /// <exception cref="InvalidCastException">If this instance is not
        /// <see cref="BooleanValue"/> or <see cref="StringValue"/>.
        /// </exception>
        /// <exception cref="FormatException">If this instance is
        /// <see cref="StringValue"/> and does not have value "true" or
        /// "false".
        /// </exception>
        /// <seealso cref="StringValue.ToBoolean"/>
        public virtual bool ToBoolean() => AsBoolean;

        /// <summary>
        /// Converts value of this instance to 32-bit signed integer.
        /// </summary>
        /// <remarks>
        /// This conversion is valid only for numeric instances such as
        /// <see cref="IntegerValue"/>, <see cref="LongValue"/>,
        /// <see cref="DoubleValue"/>, <see cref="NumberValue"/> or instances
        /// of <see cref="StringValue"/> containing valid representation of
        /// 32-bit signed integer.
        /// </remarks>
        /// <returns>Value of this instance as 32-bit signed integer.
        /// </returns>
        /// <exception cref="InvalidCastException">If this instance is
        /// not numeric and not <see cref="StringValue"/>.</exception>
        /// <exception cref="OverflowException">In a checked context, if this
        /// instance is numeric and represents value less than
        /// <see cref="Int32.MinValue"/> or greater than
        /// <see cref="Int32.MaxValue"/></exception>
        /// <exception cref="FormatException">If this instance is
        /// <see cref="StringValue"/> and it does not contain valid
        /// representation of integer.</exception>
        /// <seealso cref="LongValue.ToInt32"/>
        /// <seealso cref="DoubleValue.ToInt32"/>
        /// <seealso cref="NumberValue.ToInt32"/>
        /// <seealso cref="StringValue.ToInt32"/>
        public virtual int ToInt32() => AsInt32;

        /// <summary>
        /// Converts value of this instance to 64-bit signed integer.
        /// </summary>
        /// <remarks>
        /// This conversion is valid only for numeric instances such as
        /// <see cref="IntegerValue"/>, <see cref="LongValue"/>,
        /// <see cref="DoubleValue"/>, <see cref="NumberValue"/>, instances
        /// of <see cref="TimestampValue"/> or instances
        /// of <see cref="StringValue"/> containing valid representation of
        /// 64-bit signed integer.  For <see cref="TimestampValue"/>, it
        /// returns number of milliseconds since Unix Epoch
        /// (1970-01-01T00:00:00).
        /// </remarks>
        /// <returns>Value of this instance as 64-bit signed integer.
        /// </returns>
        /// <exception cref="InvalidCastException">If this instance is
        /// not numeric and not <see cref="TimestampValue"/> or
        /// <see cref="StringValue"/>.</exception>
        /// <exception cref="OverflowException">In a checked context, if this
        /// instance is numeric and represents value less than
        /// <see cref="Int64.MinValue"/> or greater than
        /// <see cref="Int64.MaxValue"/></exception>
        /// <exception cref="FormatException">If this instance is
        /// <see cref="StringValue"/> and it does not contain valid
        /// representation of integer.</exception>
        /// <seealso cref="IntegerValue.ToInt64"/>
        /// <seealso cref="DoubleValue.ToInt64"/>
        /// <seealso cref="NumberValue.ToInt64"/>
        /// <seealso cref="TimestampValue.ToInt64"/>
        /// <seealso cref="StringValue.ToInt64"/>
        public virtual long ToInt64() => AsInt64;

        /// <summary>
        /// Converts value of this instance to double precision floating point
        /// number.
        /// </summary>
        /// <remarks>
        /// This conversion is valid only for numeric instances such as
        /// <see cref="IntegerValue"/>, <see cref="LongValue"/>,
        /// <see cref="DoubleValue"/>, <see cref="NumberValue"/> or instances
        /// of <see cref="StringValue"/> containing valid numeric
        /// representation.
        /// </remarks>
        /// <returns>Value of this instance as double precision floating point
        /// number.
        /// </returns>
        /// <exception cref="InvalidCastException">If this instance is
        /// not numeric and not <see cref="StringValue"/>.</exception>
        /// <exception cref="OverflowException">In a checked context, if this
        /// instance is numeric and represents value less than
        /// <see cref="Double.MinValue"/> or greater than
        /// <see cref="Double.MaxValue"/></exception>
        /// <exception cref="FormatException">If this instance is
        /// <see cref="StringValue"/> and it does not contain valid numeric
        /// representation.</exception>
        /// <seealso cref="IntegerValue.ToDouble"/>
        /// <seealso cref="LongValue.ToDouble"/>
        /// <seealso cref="NumberValue.ToDouble"/>
        /// <seealso cref="StringValue.ToDouble"/>
        public virtual double ToDouble() => AsDouble;

        /// <summary>
        /// Converts value of this instance to decimal number.
        /// </summary>
        /// <remarks>
        /// This conversion is valid only for numeric instances such as
        /// <see cref="IntegerValue"/>, <see cref="LongValue"/>,
        /// <see cref="DoubleValue"/>, <see cref="NumberValue"/> or instances
        /// of <see cref="StringValue"/> containing valid numeric
        /// representation.
        /// </remarks>
        /// <returns>Value of this instance as decimal.
        /// </returns>
        /// <exception cref="InvalidCastException">If this instance is
        /// not numeric and not <see cref="StringValue"/>.</exception>
        /// <exception cref="OverflowException">In a checked context, if this
        /// instance is numeric and represents value less than
        /// <see cref="Decimal.MinValue"/> or greater than
        /// <see cref="Decimal.MaxValue"/></exception>
        /// <exception cref="FormatException">If this instance is
        /// <see cref="StringValue"/> and it does not contain valid numeric
        /// representation.</exception>
        /// <seealso cref="IntegerValue.ToDecimal"/>
        /// <seealso cref="LongValue.ToDecimal"/>
        /// <seealso cref="DoubleValue.ToDecimal"/>
        /// <seealso cref="StringValue.ToDecimal"/>
        public virtual decimal ToDecimal() => AsDecimal;

        /// <summary>
        /// Converts value of this instance to date and time value.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This conversion is valid only for instances of
        /// <see cref="TimestampValue"/>, <see cref="IntegerValue"/>,
        /// <see cref="LongValue"/> or instances of
        /// <see cref="StringValue"/> containing valid representation of
        /// date and time.
        /// </para>
        /// <para>
        /// For instances of <see cref="IntegerValue"/> and
        /// <see cref="LongValue"/>, the input value represents number
        /// of milliseconds since Unix Epoch (1970-01-01T00:00:00).  For
        /// instances of <see cref="StringValue"/>, this operation is
        /// equivalent to calling <see cref="Convert.ToDateTime(String)"/> and
        /// supports the same date and time formats.
        /// </para>
        /// </remarks>
        /// <returns>Value of this instance as date and time.</returns>
        /// <exception cref="InvalidCastException">If this instance is not
        /// <see cref="TimestampValue"/>, <see cref="IntegerValue"/>,
        /// <see cref="LongValue"/> or <see cref="StringValue"/>.
        /// </exception>
        /// <exception cref="FormatException">If this instance is
        /// <see cref="StringValue"/> and does not have valid representation
        /// of date and time.
        /// </exception>
        /// <seealso cref="IntegerValue.ToDateTime"/>
        /// <seealso cref="LongValue.ToDateTime"/>
        /// <seealso cref="StringValue.ToDateTime"/>
        public virtual DateTime ToDateTime() => AsDateTime;

        /// <summary>
        /// Converts value of this instance to <c>string</c>.
        /// </summary>
        /// <remarks>
        /// This conversion is valid for all subclasses of
        /// <see cref="FieldValue"/> and returns representation of this
        /// instance in JSON format, which is equivalent to the result of
        /// <see cref="FieldValue.ToJsonString"/> with default options.  Note
        /// that for instances of <see cref="StringValue"/> this means that a
        /// double-quoted string will be returned.
        /// </remarks>
        /// <returns>String representation of this instance.</returns>
        /// <seealso cref="FieldValue.ToJsonString"/>
        public override string ToString() => ToJsonString();
    }

}
