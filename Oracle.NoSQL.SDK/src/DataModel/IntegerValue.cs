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
    /// Represents a 32-bit signed integer value.
    /// </summary>
    /// <remarks>
    /// This class is used to represent values of NoSQL data type
    /// <em>Integer</em>.  This value is represented by a C# type <c>int</c>.
    /// </remarks>
    /// <seealso cref="FieldValue"/>
    public class IntegerValue : FieldValue
    {
        private int value;

        /// <summary>
        /// Initializes a new instance of the <see cref="IntegerValue"/> with
        /// the specified <c>int</c> value.
        /// </summary>
        /// <param name="value">The value which this instance will represent.
        /// </param>
        public IntegerValue(int value)
        {
            this.value = value;
        }

        /// <inheritdoc cref="FieldValue.DbType" path="summary"/>
        /// <value>
        /// <see cref="SDK.DbType.Integer"/>
        /// </value>
        public override DbType DbType => DbType.Integer;

        /// <summary>
        /// Gets the value of this instance as <c>int</c>.
        /// </summary>
        /// <value>
        /// The <c>int</c> value that this instance represents.
        /// </value>
        public override int AsInt32 => value;

        /// <summary>
        /// Converts the value represented by this instance to a 64-bit signed
        /// integer.
        /// </summary>
        /// <remarks>
        /// This method performs implicit conversion from <c>int</c> to
        /// <c>long</c>.
        /// </remarks>
        /// <returns>A 64-bit signed integer equivalent to the value
        /// represented by this instance.
        /// </returns>
        public override long ToInt64() => value;

        /// <summary>
        /// Converts the value represented by this instance to a double
        /// precision floating point number.
        /// </summary>
        /// <remarks>
        /// This method performs implicit conversion from <c>int</c> to
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
        /// This method performs implicit conversion from <c>int</c> to
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
        /// This method performs the same conversion as
        /// <see cref="LongValue.ToDateTime"/>.
        /// </remarks>
        /// <returns>A <see cref="DateTime"/> value constructed from the
        /// value represented by this instance as a number of milliseconds
        /// since the Unix Epoch.</returns>
        /// <seealso cref="LongValue.ToDateTime"/>
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
                case DbType.Integer:
                    return AsInt32.CompareTo(other.AsInt32);
                case DbType.Long:
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
                case DbType.Integer:
                    return AsInt32.Equals(other.AsInt32);
                case DbType.Long:
                    return ToInt64().Equals(other.AsInt64);
                case DbType.Double:
                    return ToDouble().Equals(other.AsDouble);
                case DbType.Number:
                    return ToDecimal().Equals(other.AsDecimal);
                default:
                    return false;
            }
        }

        internal override int QueryHashCode() =>
            LongValue.QueryHashCode(value);

        internal override long GetMemorySize() => GetObjectSize(sizeof(int));

        // For efficiency, the arithmetic operations below will modify
        // the value in place when possible and return the same FieldValue
        // object.  Otherwise the value is promoted to the bigger numeric
        // type and new value is returned.  Same for other numeric types.

        internal override FieldValue QueryAdd(FieldValue other)
        {
            switch (other.DbType)
            {
                case DbType.Integer:
                    try
                    {
                        value = checked(value + other.AsInt32);
                        return this;
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Long;
                    }
                case DbType.Long:
                    try
                    {
                        return new LongValue(checked(value + other.AsInt64));
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
                case DbType.Integer:
                    try
                    {
                        value = checked(value - other.AsInt32);
                        return this;
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Long;
                    }
                case DbType.Long:
                    try
                    {
                        return new LongValue(checked(value - other.AsInt64));
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
                case DbType.Integer:
                    try
                    {
                        value = checked(value * other.AsInt32);
                        return this;
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Long;
                    }
                case DbType.Long:
                    try
                    {
                        return new LongValue(checked(value * other.AsInt64));
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
                case DbType.Integer:
                    if (isFloating)
                    {
                        return new DoubleValue(value / other.AsDouble);
                    }
                    value /= other.AsInt32;
                    return this;
                case DbType.Long:
                    return isFloating ?
                        (FieldValue)new DoubleValue(value / other.AsDouble) :
                        new LongValue(value / other.AsInt64);
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
