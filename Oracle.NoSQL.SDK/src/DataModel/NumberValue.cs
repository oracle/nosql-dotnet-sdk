/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Globalization;
    using System.Text.Json;
    using static SizeOf;

    /// <summary>
    /// Represents a decimal number value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used to represent values of NoSQL data type
    /// <em>Number</em>.  Data type <em>Number</em> represents arbitrary
    /// precision signed decimal numbers.  This class represents the value
    /// as C# type <see cref="Decimal"/> and thus can only hold a subset of
    /// values that data type <em>Number</em> can hold. <see cref="Decimal"/>
    /// holds approximate range of ±1.0 x 10^28 to ±7.9228 x 10^28 and has a
    /// precision of 28 to 29 decimal digits.  This makes it suitable for
    /// financial calculations more than for scientific calculations.
    /// </para>
    /// <para>
    /// Currently it is not possible to retrieve values outside this range
    /// from a <em>Number</em> field of a table.  Trying to retrieve a value
    /// less than <see cref="Decimal.MinValue"/> or greater than
    /// <see cref="Decimal.MaxValue"/> will throw
    /// <see cref="OverflowException"/>.  Retrieving the value within this
    /// range but with a greater precision than allowed by
    /// <see cref="Decimal"/> will round this value to the nearest
    /// <see cref="Decimal"/>.
    /// </para>
    /// <para>
    /// Note that because <c>decimal</c> has higher precision but smaller
    /// range than <c>double</c>, when comparing
    /// <see cref="NumberValue"/> and <see cref="DoubleValue"/> both are
    /// compared as <c>decimal</c> values if the value represented by
    /// <see cref="DoubleValue"/> is within the <c>decimal</c> range,
    /// otherwise the result of comparison is based on the sign of the
    /// <c>double</c> value.  See <see cref="FieldValue.CompareTo"/> and
    /// <see cref="FieldValue.Equals(FieldValue)"/> for more information.
    /// </para>
    /// </remarks>
    /// <seealso cref="FieldValue"/>
    public class NumberValue : FieldValue
    {
        private decimal value;

        // Note that the default conversion as below may be imprecise since
        // the result will be rounded to a maximum of 15 significant digits.
        private static decimal DoubleToDecimal(double value) =>
            Convert.ToDecimal(value);

        internal static int CompareDecimalDouble(decimal value1,
            double value2)
        {
            try
            {
                return value1.CompareTo(DoubleToDecimal(value2));
            }
            catch (OverflowException)
            {
                return value2 < 0 ? 1 : -1;
            }
        }

        internal static bool DecimalDoubleEquals(decimal value1,
            double value2)
        {
            try
            {
                return value1.Equals(DoubleToDecimal(value2));
            }
            catch (OverflowException)
            {
                return false;
            }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NumberValue"/> with
        /// the specified <c>decimal</c> value.
        /// </summary>
        /// <param name="value">The value which this instance will represent.
        /// </param>
        public NumberValue(decimal value)
        {
            this.value = value;
        }

        /// <inheritdoc cref="FieldValue.DbType" path="summary"/>
        /// <value>
        /// <see cref="SDK.DbType.Number"/>
        /// </value>
        public override DbType DbType => DbType.Number;

        /// <summary>
        /// Gets the value of this instance as <c>decimal</c>.
        /// </summary>
        /// <value>
        /// The <c>decimal</c> value that this instance represents.
        /// </value>
        public override decimal AsDecimal => value;

        /// <summary>
        /// Converts the value represented by this instance to a 32-bit signed
        /// integer.
        /// </summary>
        /// <remarks>
        /// This method performs the same conversion as
        /// <see cref="Convert.ToInt32(decimal)"/>.
        /// </remarks>
        /// <returns>The value rounded to the nearest 32-bit signed integer.
        /// </returns>
        /// <exception cref="OverflowException">If this
        /// instance represents value less than <see cref="Int32.MinValue"/>
        /// or greater than <see cref="Int32.MaxValue"/></exception>
        /// <seealso cref="Convert.ToInt32(decimal)"/>
        public override int ToInt32() => Convert.ToInt32(value);

        /// <summary>
        /// Converts the value represented by this instance to a 64-bit signed
        /// integer.
        /// </summary>
        /// <remarks>
        /// This method performs the same conversion as
        /// <see cref="Convert.ToInt64(decimal)"/>.
        /// </remarks>
        /// <returns>The value rounded to the nearest 32-bit signed integer.
        /// </returns>
        /// <exception cref="OverflowException">If this
        /// instance represents value less than <see cref="Int64.MinValue"/>
        /// or greater than <see cref="Int64.MaxValue"/></exception>
        /// <seealso cref="Convert.ToInt64(decimal)"/>
        public override long ToInt64() => Convert.ToInt64(value);

        /// <summary>
        /// Converts the value represented by this instance to a double
        /// precision floating point number.
        /// </summary>
        /// <remarks>
        /// This method performs the same conversion as
        /// <see cref="Convert.ToDouble(decimal)"/>.
        /// </remarks>
        /// <returns>A double precision floating point number equivalent to
        /// the value represented by this instance. </returns>
        /// <seealso cref="Convert.ToDouble(decimal)"/>
        public override double ToDouble() => Convert.ToDouble(value);

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
                case DbType.Number:
                case DbType.Integer:
                case DbType.Long:
                    return AsDecimal.CompareTo(other.ToDecimal());
                case DbType.Double:
                    return CompareDecimalDouble(AsDecimal, other.AsDouble);
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
                case DbType.Number:
                case DbType.Long:
                case DbType.Integer:
                    return AsDecimal.Equals(other.ToDecimal());
                case DbType.Double:
                    return DecimalDoubleEquals(AsDecimal, other.AsDouble);
                default:
                    return false;
            }
        }

        internal static int QueryHashCode(decimal value)
        {
            var doubleValue = Convert.ToDouble(value);
            return DecimalDoubleEquals(value, doubleValue)
                ? DoubleValue.QueryHashCode(doubleValue)
                : value.GetHashCode();
        }

        internal override long GetMemorySize() =>
            GetObjectSize(sizeof(decimal));

        internal override int QueryHashCode() => QueryHashCode(value);

        internal override FieldValue QueryAdd(FieldValue other)
        {
            switch (other.DbType)
            {
                case DbType.Long:
                case DbType.Integer:
                case DbType.Number:
                    try
                    {
                        value += other.ToDecimal();
                        return this;
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Double;
                    }
                case DbType.Double:
                    return new DoubleValue((double)value + other.AsDouble);
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
                case DbType.Number:
                    try
                    {
                        value -= other.ToDecimal();
                        return this;
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Double;
                    }
                case DbType.Double:
                    return new DoubleValue((double)value - other.AsDouble);
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
                case DbType.Number:
                    try
                    {
                        value *= other.ToDecimal();
                        return this;
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Double;
                    }
                case DbType.Double:
                    return new DoubleValue((double)value * other.AsDouble);
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
                case DbType.Number:
                    try
                    {
                        value /= other.ToDecimal();
                        return this;
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Double;
                    }
                case DbType.Double:
                    return new DoubleValue((double)value / other.AsDouble);
                default:
                    throw other.NonNumericOperand(DivisionOp);
            }
        }

    }

}
