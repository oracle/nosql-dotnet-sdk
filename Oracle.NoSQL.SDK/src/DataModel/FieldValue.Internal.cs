/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections.Generic;
    using System.Text.Json;

    public abstract partial class FieldValue
    {
        // These are used in the exception messages
        internal const string AdditionOp = "addition";
        internal const string SubtractionOp = "subtraction";
        internal const string MultiplicationOp = "multiplication";
        internal const string DivisionOp = "division";
        internal const string FloatDivisionOp = "floating point division";

        private static FieldValue GetJsonNumberValue(
            ref Utf8JsonReader reader, bool preferDecimal)
        {
            if (reader.TryGetInt32(out var int32Value))
            {
                return new IntegerValue(int32Value);
            }

            if (reader.TryGetInt64(out var int64Value))
            {
                return new LongValue(int64Value);
            }

            if (preferDecimal && reader.TryGetDecimal(out var decimalValue))
            {
                return new NumberValue(decimalValue);
            }

            if (reader.TryGetDouble(out var doubleValue))
            {
                return new DoubleValue(doubleValue);
            }

            throw new JsonException(
                $"Invalid or unsupported numeric value {reader.GetString()}");
        }

        internal Exception CannotCastTo(Type type, bool toConvert = false)
        {
            var verb = toConvert ? "convert" : "cast";
            return new InvalidCastException(
                $"Cannot {verb} field value {this} of type {DbType} to " +
                type.Name);
        }

        internal Exception CannotConvertTo(Type type) =>
            CannotCastTo(type, true);

        internal Exception ValueOutOfRange(Type type)
        {
            return new OverflowException(
                $"Field value {this} of type {DbType} is out of range for " +
                type.Name);
        }

        internal static FieldValue DeserializeFromJson(
            ref Utf8JsonReader reader, JsonInputOptions options,
            bool hasReadToken)
        {
            if (!hasReadToken && !reader.Read())
            {
                throw new JsonException("No tokens available for FieldValue");
            }

            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    return MapValue.DeserializeFromJson(ref reader,
                        options, true);
                case JsonTokenType.StartArray:
                    return ArrayValue.DeserializeFromJson(ref reader,
                        options, true);
                case JsonTokenType.String:
                    return reader.GetString();
                case JsonTokenType.Number:
                    return GetJsonNumberValue(ref reader,
                        options?.PreferDecimal ?? false);
                case JsonTokenType.True:
                    return BooleanValue.True;
                case JsonTokenType.False:
                    return BooleanValue.False;
                case JsonTokenType.Null:
                    return JsonNull;
                default:
                    throw new JsonException(
                        "Invalid token type for AtomicValue: " +
                        reader.TokenType);
            }
        }

        internal virtual bool SupportsComparison => true;

        internal virtual bool IsSpecial => false;

        internal virtual bool IsNumeric => false;

        internal virtual bool IsAtomic => true;

        internal Exception ComparisonNotSupported(FieldValue other)
        {
            return new NotSupportedException(
                $"Cannot compare values of type {DbType} and {other.DbType}");
        }

        internal virtual int QueryCompare(FieldValue other, int nullRank) =>
            throw ComparisonNotSupported(other);

        internal int QueryCompare(FieldValue other) => QueryCompare(other, 1);

        // Includes comparison of complex types, such as arrays and maps, as
        // well as binary. The order between different types is
        // Arrays > Maps > Atomic values, and Binary values (which are atomic)
        // are greater than any other atomic values except special values
        // (NullValue, JsonNullValue, EmptyValue), for which nullRank is used.
        // Below implementation is only for atomic non-binary types. This
        // should be overriden for BinaryValue, ArrayValue and MapValue.
        internal virtual int QueryCompareTotalOrder(FieldValue other,
            int nullRank) =>
            other.IsAtomic
                ? (other.DbType == DbType.Binary
                    ? (IsSpecial ? nullRank : -1)
                    : QueryCompare(other, nullRank))
                : -1;

        internal int QueryCompareTotalOrder(FieldValue other) =>
            QueryCompareTotalOrder(other, 1);

        internal virtual bool QueryEquals(FieldValue other) =>
            throw new NotSupportedException(
                $"Field value {this} of type {DbType} does not support " +
                "equality comparison");

        internal abstract int QueryHashCode();

        internal abstract long GetMemorySize();

        internal Exception NonNumericOperand(string op) =>
            new InvalidOperationException(
            $"Encountered non-numeric operand {this} of type {DbType} " +
            $"for {op}");

        internal virtual FieldValue QueryAdd(FieldValue other) =>
            throw NonNumericOperand(AdditionOp);

        internal virtual FieldValue QuerySubtract(FieldValue other) =>
            throw NonNumericOperand(SubtractionOp);

        internal virtual FieldValue QueryMultiply(FieldValue other) =>
            throw NonNumericOperand(MultiplicationOp);

        internal virtual FieldValue QueryDivide(FieldValue other,
            bool isFloating) => throw NonNumericOperand(
            isFloating ? FloatDivisionOp : DivisionOp);

    }

}
