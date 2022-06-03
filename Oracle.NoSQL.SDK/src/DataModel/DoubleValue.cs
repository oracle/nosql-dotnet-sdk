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
    /// Represents a double precision floating point value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used to represent values of NoSQL data types
    /// <em>Double</em> and <em>Float</em>.  This value is represented
    /// by a C# type <c>double</c>.
    /// </para>
    /// <para>
    /// Note that when converted to JSON, instances of
    /// <see cref="DoubleValue"/> that contain special values such as
    /// <see cref = "Double.PositiveInfinity" />,
    /// <see cref = "Double.NegativeInfinity" /> and
    /// <see cref="Double.NaN"/> will be represented by strings "Infinity",
    /// "-Infinity" and "NaN" respectively and thus cannot be converted back
    /// from JSON to instances of <see cref="DoubleValue"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="FieldValue"/>
    public class DoubleValue : FieldValue
    {
        private double value;

        /// <summary>
        /// Initializes a new instance of the <see cref="DoubleValue"/> with
        /// the specified <c>double</c> value.
        /// </summary>
        /// <param name="value">The value which this instance will represent.
        /// </param>
        public DoubleValue(double value)
        {
            this.value = value;
        }

        /// <inheritdoc cref="FieldValue.DbType" path="summary"/>
        /// <value>
        /// <see cref="SDK.DbType.Double"/>
        /// </value>
        public override DbType DbType => DbType.Double;

        /// <summary>
        /// Gets the value of this instance as <c>double</c>.
        /// </summary>
        /// <value>
        /// The <c>double</c> value that this instance represents.
        /// </value>
        public override double AsDouble => value;

        /// <summary>
        /// Converts the value represented by this instance to a 32-bit signed
        /// integer.
        /// </summary>
        /// <remarks>
        /// This method performs the same conversion as
        /// <see cref="Convert.ToInt32(double)"/>.
        /// </remarks>
        /// <returns>The value rounded to the nearest 32-bit signed integer.
        /// </returns>
        /// <exception cref="OverflowException">If this
        /// instance represents value less than <see cref="Int32.MinValue"/>
        /// or greater than <see cref="Int32.MaxValue"/></exception>
        /// <seealso cref="Convert.ToInt32(double)"/>
        public override int ToInt32() => Convert.ToInt32(value);

        /// <summary>
        /// Converts the value represented by this instance to a 64-bit signed
        /// integer.
        /// </summary>
        /// <remarks>
        /// This method performs the same conversion as
        /// <see cref="Convert.ToInt64(double)"/>.
        /// </remarks>
        /// <returns>The value rounded to the nearest 64-bit signed integer.
        /// </returns>
        /// <exception cref="OverflowException">If this
        /// instance represents value less than <see cref="Int64.MinValue"/>
        /// or greater than <see cref="Int64.MaxValue"/></exception>
        /// <seealso cref="Convert.ToInt64(double)"/>
        public override long ToInt64() => Convert.ToInt64(value);

        /// <summary>
        /// Converts the value represented by this instance to a decimal
        /// number.
        /// </summary>
        /// <remarks>
        /// This method performs the same conversion as
        /// <see cref="Convert.ToDecimal(double)"/>.
        /// </remarks>
        /// <returns>A decimal number equivalent to the value represented by
        /// this instance.</returns>
        /// <exception cref="OverflowException">If this
        /// instance represents value less than <see cref="Decimal.MinValue"/>
        /// or greater than <see cref="Decimal.MaxValue"/></exception>
        /// <seealso cref="Convert.ToDecimal(double)"/>
        public override decimal ToDecimal() => Convert.ToDecimal(value);

        /// <inheritdoc/>
        public override void SerializeAsJson(Utf8JsonWriter writer,
            JsonOutputOptions options = null)
        {
            if (double.IsFinite(value))
            {
                writer.WriteNumberValue(value);
            }
            else
            {
                writer.WriteStringValue(value.ToString(
                    CultureInfo.InvariantCulture));
            }
        }

        internal override bool IsNumeric => true;

        internal override int QueryCompare(FieldValue other, int nullRank)
        {
            switch (other.DbType)
            {
                case DbType.Double:
                case DbType.Integer:
                case DbType.Long:
                    return AsDouble.CompareTo(other.ToDouble());
                case DbType.Number:
                    return -NumberValue.CompareDecimalDouble(other.AsDecimal,
                        AsDouble);
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
                case DbType.Double:
                case DbType.Long:
                case DbType.Integer:
                    return AsDouble.Equals(other.ToDouble());
                case DbType.Number:
                    return NumberValue.DecimalDoubleEquals(other.AsDecimal,
                        AsDouble);
                default:
                    return false;
            }
        }

        internal static int QueryHashCode(double value)
        {
            var longValue = unchecked((long)value);
            return value.Equals(longValue)
                ? LongValue.QueryHashCode(longValue)
                : value.GetHashCode();
        }

        internal override int QueryHashCode() => QueryHashCode(value);

        internal override long GetMemorySize() =>
            GetObjectSize(sizeof(double));

        internal override FieldValue QueryAdd(FieldValue other)
        {
            switch (other.DbType)
            {
                case DbType.Long:
                case DbType.Integer:
                case DbType.Double:
                    value += other.ToDouble();
                    return this;
                case DbType.Number:
                    try
                    {
                        return new NumberValue(
                            (decimal)value + other.AsDecimal);
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Double;
                    }
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
                case DbType.Double:
                    value -= other.ToDouble();
                    return this;
                case DbType.Number:
                    try
                    {
                        return new NumberValue(
                            (decimal)value - other.AsDecimal);
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Double;
                    }
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
                case DbType.Double:
                    value *= other.ToDouble();
                    return this;
                case DbType.Number:
                    try
                    {
                        return new NumberValue(
                            (decimal)value * other.AsDecimal);
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Double;
                    }
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
                case DbType.Double:
                    value /= other.ToDouble();
                    return this;
                case DbType.Number:
                    try
                    {
                        return new NumberValue(
                            (decimal)value / other.AsDecimal);
                    }
                    catch (OverflowException)
                    {
                        goto case DbType.Double;
                    }
                default:
                    throw other.NonNumericOperand(DivisionOp);
            }
        }

    }

}
