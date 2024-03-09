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
    /// Represents a boolean value.
    /// </summary>
    /// <remarks>
    /// This class is used to represent values of NoSQL data type
    /// <em>Boolean</em>.  The values of this class are never instantiated
    /// and the two possible values are represented by constants
    /// <see cref="BooleanValue.True"/> and <see cref="BooleanValue.False"/>.
    /// </remarks>
    /// <seealso cref="FieldValue"/>
    public abstract class BooleanValue : FieldValue
    {
        private BooleanValue()
        {
        }

        private class TrueBooleanValue : BooleanValue
        {
            public override bool AsBoolean => true;
        }

        private class FalseBooleanValue : BooleanValue
        {
            public override bool AsBoolean => false;
        }

        /// <summary>
        /// Represents the value <c>true</c>.  This field is read-only.
        /// </summary>
        public static readonly BooleanValue True = new TrueBooleanValue();

        /// <summary>
        /// Represents the value <c>false</c>.  This field is read-only.
        /// </summary>
        public static readonly BooleanValue False = new FalseBooleanValue();

        /// <inheritdoc cref="FieldValue.DbType" path="summary"/>
        /// <value>
        /// <see cref="SDK.DbType.Boolean"/>
        /// </value>
        public override DbType DbType => DbType.Boolean;

        /// <summary>
        /// Gets the value of this instance as boolean.
        /// </summary>
        /// <value>
        /// The <c>bool</c> value that this instance represents.
        /// </value>
        public abstract override bool AsBoolean { get; }

        /// <inheritdoc/>
        public override void SerializeAsJson(Utf8JsonWriter writer,
            JsonOutputOptions options = null)
        {
            writer.WriteBooleanValue(AsBoolean);
        }

        internal override int QueryCompare(FieldValue other, int nullRank)
        {
            switch (other.DbType)
            {
                case DbType.Boolean:
                    return AsBoolean.CompareTo(other.AsBoolean);
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
            // This way will work even if the instance was deep-cloned.
            return other.DbType == DbType.Boolean &&
                   AsBoolean == other.AsBoolean;
        }

        internal override int QueryHashCode() => AsBoolean.GetHashCode();

        internal override long GetMemorySize() => 0;
    }

}
