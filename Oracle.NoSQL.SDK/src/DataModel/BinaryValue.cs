/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections;
    using System.Text.Json;
    using static SizeOf;

    /// <summary>
    /// Represents a binary value.
    /// </summary>
    /// <remarks>
    /// This class is used to represent values of NoSQL data types
    /// <em>Binary</em> and <em>Fixed Binary</em>.  This value is represented
    /// by a C# type <c>byte[]</c>.  When converted to JSON, this value
    /// is represented as Base64 encoded string.
    /// </remarks>
    /// <seealso cref="FieldValue"/>
    public class BinaryValue : FieldValue
    {
        private readonly byte[] value;

        internal static bool ByteArraysEqual(byte[] array1, byte[] array2)
        {
            return StructuralComparisons.StructuralEqualityComparer.Equals(
                array1, array2);
        }

        internal static int GetByteArrayHashCode(byte[] array)
        {
            return StructuralComparisons.StructuralEqualityComparer
                .GetHashCode(array);
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BinaryValue"/> with
        /// the specified <c>byte[]</c> value.
        /// </summary>
        /// <param name="value">The value which this instance will represent.
        /// </param>
        /// <exception cref="ArgumentNullException">If
        /// <paramref name="value"/> is <c>null</c>.</exception>
        public BinaryValue(byte[] value)
        {
            this.value = value ?? throw new ArgumentNullException(
                nameof(value),
                "Argument to BinaryValue constructor cannot be null");
        }

        /// <inheritdoc cref="FieldValue.DbType" path="summary"/>
        /// <value>
        /// <see cref="SDK.DbType.Binary"/>
        /// </value>
        public override DbType DbType => DbType.Binary;

        /// <summary>
        /// Gets the value of this instance as byte array.
        /// </summary>
        /// <value>
        /// The <c>byte[]</c> value that this instance represents.
        /// </value>
        public override byte[] AsByteArray => value;

        /// <inheritdoc/>
        public override void SerializeAsJson(Utf8JsonWriter writer,
            JsonOutputOptions options = null)
        {
            writer.WriteBase64StringValue(value);
        }

        internal override bool SupportsComparison => false;

        internal override bool QueryEquals(FieldValue other)
        {
            return other.DbType == DbType.Binary &&
                   ByteArraysEqual(AsByteArray, other.AsByteArray);
        }

        internal override int QueryHashCode() => GetByteArrayHashCode(value);

        internal override long GetMemorySize() =>
            GetObjectSize(GetByteArraySize(value));
    }

}
