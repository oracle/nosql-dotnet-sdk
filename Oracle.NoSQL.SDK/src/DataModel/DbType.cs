/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.ComponentModel;

    /// <summary>
    /// Represents the type of <see cref="FieldValue"/> instance.
    /// <remarks>Generally every subclass of <see cref="FieldValue"/> is
    /// represented by its own <see cref="DbType"/> value, with the exception
    /// of <see cref="MapValue"/> and <see cref="RecordValue"/> both
    /// represented by <see cref="DbType.Map"/>.
    /// </remarks>
    /// </summary>
    /// <seealso cref="FieldValue"/>
    public enum DbType
    {
        /// <summary>
        /// Represents <see cref="ArrayValue"/>.
        /// </summary>
        Array = 0,

        /// <summary>
        /// Represents <see cref="BinaryValue"/>.
        /// </summary>
        Binary = 1,

        /// <summary>
        /// Represents <see cref="BooleanValue"/>.
        /// </summary>
        Boolean = 2,

        /// <summary>
        /// Represents <see cref="DoubleValue"/>.
        /// </summary>
        Double = 3,

        /// <summary>
        /// Represents <see cref="IntegerValue"/>.
        /// </summary>
        Integer = 4,

        /// <summary>
        /// Represents <see cref="LongValue"/>.
        /// </summary>
        Long = 5,

        /// <summary>
        /// Represents <see cref="MapValue"/> or <see cref="RecordValue"/>.
        /// </summary>
        Map = 6,

        /// <summary>
        /// Represents <see cref="StringValue"/>.
        /// </summary>
        String = 7,

        /// <summary>
        /// Represents <see cref="TimestampValue"/>.
        /// </summary>
        Timestamp = 8,

        /// <summary>
        /// Represents <see cref="NumberValue"/>.
        /// </summary>
        Number = 9,

        /// <summary>
        /// Represents <see cref="JsonNullValue"/>.
        /// </summary>
        JsonNull = 10,

        /// <summary>
        /// Represents <see cref="NullValue"/>.
        /// </summary>
        Null = 11,

        /// <summary>
        /// For internal use only.
        /// </summary>
        Empty = 12
    }

}
