/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
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
    /// Represents a 64-bit signed integer value.
    /// </summary>
    /// <remarks>
    /// This class is used to represent values of NoSQL data type
    /// <em>Long</em>.  This value is represented by a C# type <c>long</c>.
    /// </remarks>
    /// <seealso cref="FieldValue"/>
    public class LongValue : FieldValue
    {
        private long value;

        /// <summary>
        /// Initializes a new instance of the <see cref="LongValue"/> with
        /// the specified <c>long</c> value.
        /// </summary>
        /// <param name="value">The value which this instance will represent.
        /// </param>
        public LongValue(long value)
        {
            this.value = value;
        }

        /// <inheritdoc cref="FieldValue.DbType" path="summary"/>
        /// <value>
        /// <see cref="SDK.DbType.Long"/>
        /// </value>
        public override DbType DbType => DbType.Long;

        /// <summary>
        /// Gets the value of this instance as <c>long</c>.
        /// </summary>
        /// <value>
        /// The <c>long</c> value that this instance represents.
        /// </value>
        public override long AsInt64 => value;

        /// <summary>
        /// Converts the value represented by this instance to a 32-bit signed
        /// integer.
        /// </summary>
        /// <remarks>
        /// This method performs the same conversion as
        /// <see cref="Convert.ToInt32(long)"/>.
        /// </remarks>
        /// <returns>A 32-bit signed integer that is equivalent to the value
        /// represented by this instance.
        /// </returns>
        /// <exception cref="OverflowException">If this
        /// instance represents value less than <see cref="Int32.MinValue"/>
        /// or greater than <see cref="Int32.MaxValue"/></exception>
        /// <seealso cref="Convert.ToInt32(long)"/>
        public override int ToInt32() => Convert.ToInt32(value);

        /// <summary>
        /// Converts the value represented by this instance to a double
        /// precision floating point number.
        /// </summary>
        /// <remarks>
        /// This method performs implicit conversion from <c>long</c> to
        /// <c>double</c>.
        /// </remarks>
        /// <returns>A double precision floating point number equivalent to
        /// the value represented by this instance.
        /// </returns>
        public override double ToDouble() => value;

        /// <summary>
        /// Converts the value represented by this instance to a decimal
        /// number.
        /// </summary>
        /// <remarks>
        /// This method performs implicit conversion from <c>long</c> to
        /// <c>decimal</c>.
        /// </remarks>
        /// <returns>A decimal number equivalent to the value represented by
        /// this instance.
        /// </returns>
        public override decimal ToDecimal() => value;

        /// <summary>
        /// Converts the value represented by this instance to a
        /// <see cref="DateTime"/> object.
        /// </summary>
        /// <remarks>
        /// This method constructs <see cref="DateTime"/> object from the
        /// value represented by this instance interpreted as a number of
        /// milliseconds since the Unix Epoch, 00:00:00 UTC, January 1, 1970.
        /// If the value represented by this instance is negative, it is then
        /// interpreted as a number of milliseconds before the Unix Epoch.
        /// </remarks>
        /// <returns>A <see cref="DateTime"/> value constructed from the
        /// value represented by this instance as a number of milliseconds
        /// since the Unix Epoch.</returns>
        /// <seealso cref="DateTime.UnixEpoch"/>
        /// <exception cref="OverflowException">If the given number of
        /// milliseconds produces a <see cref="TimeSpan"/> value less than
        /// <see cref="TimeSpan.MinValue"/> or greater than
        /// <see cref="TimeSpan.MaxValue"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">If the resulting
        /// <see cref="DateTime"/> value is less than
        /// <see cref="DateTime.MinValue"/> or greater than
        /// <see cref="DateTime.MaxValue"/></exception>
        public override DateTime ToDateTime() =>
            DateTimeUtils.UnixMillisToDateTime(value);

        /// <inheritdoc/>
        public override void SerializeAsJson(Utf8JsonWriter writer,
            JsonOutputOptions options = null)
        {
            writer.WriteNumberValue(value);
        }

        internal override bool IsNumeric => true;

        internal override int QueryCompare(FieldValue other, int nullRank)
        {
            switch (other.DbType)
            {
                case DbType.Long:
                case DbType.Integer:
                    return ToInt64().CompareTo(other.AsInt64);
                case DbType.Double:
                    return ToDouble().CompareTo(other.AsDouble);
                case DbType.Number:
                    return ToDecimal().CompareTo(other.AsDecimal);
                case DbType.Boolean:
                case DbType.String:
                case DbType.Timestamp:
                    return -1;
                case DbType.Null:
                case DbType.JsonNull:
                case DbType.Empty:
                    return -nullRank;
                default:
                    throw ComparisonNotSupported(other);
            }
        }

        internal override bool QueryEquals(FieldValue other)
        {
            switch (other.DbType)
            {
                case DbType.Long:
                case DbType.Integer:
                    return ToInt64().Equals(other.AsInt64);
                case DbType.Double:
                    return ToDouble().Equals(other.AsDouble);
                case DbType.Number:
                    return ToDecimal().Equals(other.AsDecimal);
                default:
                    return false;
            }
        }

        internal static int QueryHashCode(long value) =>
            (int)(value ^ (value >> 32));

        internal override long GetMemorySize() => GetObjectSize(sizeof(long));

        internal override int QueryHashCode() => QueryHashCode(value);

        internal override FieldValue QueryAdd(FieldValue other)
        {
            switch (other.DbType)
            {
                case DbType.Long:
                case DbType.Integer:
                    try
                    {
                        value = checked(value + other.ToInt64());
                        return this;
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Number;
                    }
                case DbType.Number:
                    try
                    {
                        return new NumberValue(value + other.AsDecimal);
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Double;
                    }
                case DbType.Double:
                    return new DoubleValue(value + other.AsDouble);
                default:
                    throw other.NonNumericOperand(AdditionOp);
            }
        }

        internal override FieldValue QuerySubtract(FieldValue other)
        {
            switch (other.DbType)
            {
                case DbType.Long:
                case DbType.Integer:
                    try
                    {
                        value = checked(value - other.ToInt64());
                        return this;
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Number;
                    }
                case DbType.Number:
                    try
                    {
                        return new NumberValue(value - other.AsDecimal);
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Double;
                    }
                case DbType.Double:
                    return new DoubleValue(value - other.AsDouble);
                default:
                    throw other.NonNumericOperand(SubtractionOp);
            }
        }

        internal override FieldValue QueryMultiply(FieldValue other)
        {
            switch (other.DbType)
            {
                case DbType.Long:
                case DbType.Integer:
                    try
                    {
                        value = checked(value * other.ToInt64());
                        return this;
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Number;
                    }
                case DbType.Number:
                    try
                    {
                        return new NumberValue(value * other.AsDecimal);
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Double;
                    }
                case DbType.Double:
                    return new DoubleValue(value * other.AsDouble);
                default:
                    throw other.NonNumericOperand(MultiplicationOp);
            }
        }

        internal override FieldValue QueryDivide(FieldValue other,
            bool isFloating)
        {
            switch (other.DbType)
            {
                case DbType.Long:
                case DbType.Integer:
                    if (isFloating)
                    {
                        return new DoubleValue(value / other.ToDouble());
                    }
                    value /= other.ToInt64();
                    return this;
                case DbType.Number:
                    try
                    {
                        return new NumberValue(value / other.AsDecimal);
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Double;
                    }
                case DbType.Double:
                    return new DoubleValue(value / other.AsDouble);
                default:
                    throw other.NonNumericOperand(DivisionOp);
            }
        }

    }

}
