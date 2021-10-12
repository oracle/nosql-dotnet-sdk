/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{
    using System;

    public abstract partial class FieldValue
    {
        /// <summary>
        /// Compares this instance to the specified <see cref="FieldValue"/>
        /// instance and returns and returns an integer that indicates
        /// whether this instance is less than, the same as, or greater than
        /// the specified <see cref="FieldValue"/> instance.
        /// </summary>
        /// <remarks>
        /// The comparison semantics roughly follows the one described in
        /// section <em>Value Comparison Operators</em> in the SQL Reference
        /// Guide.  The following comparisons are allowed:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// A numeric <see cref="FieldValue"/> instance is comparable with any
        /// other numeric <see cref="FieldValue"/> instance.  When comparing
        /// numeric instances of different types, the value of the "smaller"
        /// type is converted to the value of the "larger" type in the rough
        /// order of <c>int</c>, <c>long</c>, <c>double</c>, <c>decimal</c>
        /// (although comparison of <c>double</c> and <c>decimal</c> values is
        /// treated specially given that <c>decimal</c> has higher precision
        /// but smaller range than <c>double</c>).
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// A <see cref="StringValue"/> instance is comparable to another
        /// <see cref="StringValue"/> instance.  The strings are compared
        /// using <see cref="StringComparison.Ordinal"/> mode as in
        /// <c>string.Compare(value1, value2, StringComparison.Ordinal)</c>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// A <see cref="BooleanValue"/> instance is comparable to another
        /// <see cref="BooleanValue"/> instance with values being compared
        /// via <see cref="Boolean.CompareTo"/>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// A <see cref="TimestampValue"/> instance is comparable to another
        /// <see cref="TimestampValue"/> instance with values being compared
        /// via <see cref="DateTime.CompareTo"/>.
        /// </description>
        /// </item>
        /// </list>
        /// All other comparisons are disallowed and will throw
        /// <see cref="NotSupportedException"/>.
        /// </remarks>
        /// <param name="other">The value to compare with this instance.
        /// </param>
        /// <returns>An integer indicating whether this value is less than,
        /// same as or greater than <paramref name="other"/>.
        /// <list type="table">
        /// <listheader>
        /// <term>Value</term>
        /// <term>Description</term>
        /// </listheader>
        /// <item>
        /// <term>Less than zero</term>
        /// <term>This instance is less than <paramref name="other"/></term>
        /// </item>
        /// <item>
        /// <term>Zero</term>
        /// <term>This instance equals <paramref name="other"/></term>
        /// </item>
        /// <item>
        /// <term>Greater than zero</term>
        /// <term>This instance is greater than <paramref name="other"/></term>
        /// </item>
        /// </list>
        /// </returns>
        /// <exception cref="ArgumentNullException"><paramref name="other"/>
        /// is <c>null</c>.</exception>
        /// <exception cref="NotSupportedException">Comparison between this
        /// instance and <paramref name="other"/> is not supported.
        /// </exception>
        /// <seealso cref="FieldValue.Equals(FieldValue)"/>
        public int CompareTo(FieldValue other)
        {
            if (other is null)
            {
                throw new ArgumentNullException(nameof(other));
            }

            if (DbType == other.DbType || IsNumeric && other.IsNumeric)
            {
                return QueryCompare(other);
            }

            throw ComparisonNotSupported(other);
        }

        /// <summary>
        /// Returns a value indicating whether the value of this instance is
        /// equal to the value of the specified <see cref="FieldValue"/>
        /// instance.
        /// </summary>
        /// <remarks>
        /// The equality comparison semantics roughly follows the one
        /// described in section <em>Value Comparison Operators</em> in the
        /// SQL Reference Guide.  All comparisons supported by
        /// <see cref="FieldValue.CompareTo"/> are also supported as equality
        /// comparisons.  In addition to these, the following comparisons are
        /// also supported:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// An instance of <see cref="BinaryValue"/> is comparable with
        /// another instance of <see cref="BinaryValue"/> for equality.  They
        /// are equal if their byte array values are the same length and are
        /// equal byte-per-byte.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// Singleton instance of <see cref="NullValue"/> is equal to itself
        /// and not equal to instance of any other type.  Same goes for
        /// singleton instance of <see cref="JsonNullValue"/>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// An instance of <see cref="ArrayValue"/> is comparable with another
        /// instance of <see cref="ArrayValue"/> for equality.  They are equal
        /// if the two arrays are the same length and the elements of the two
        /// arrays are equal pair-wise, with equality of elements defined in
        /// recursive fashion.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// An instance of <see cref="MapValue"/> is comparable with another
        /// instance of <see cref="MapValue"/> for equality.  They are equal
        /// if the two dictionaries have the same size and contain equal keys
        /// and values, that is for each key in the first dictionary there
        /// must exist the same key in the second map with the value equal
        /// to the corresponding value in the first map, with equality of the
        /// values defined in recursive fashion.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// An instance of <see cref="RecordValue"/> is comparable with
        /// another instance of <see cref="RecordValue"/> for equality.  They
        /// are equal if the two ordered dictionaries have the same size and
        /// for each position their keys and values are equal pair-wise, with
        /// equality of values defined in recursive fashion.
        /// </description>
        /// </item>
        /// </list>
        /// Other than the cases listed above and in
        /// <see cref="FieldValue.CompareTo"/> the values are not comparable
        /// for equality and this method returns <c>false</c>.
        /// </remarks>
        /// <param name="other">The value to compare with this instance for
        /// equality.</param>
        /// <returns><c>true</c> if <paramref name="other"/> is not
        /// <c>null</c> and the value of this instance is equal to
        /// <paramref name="other"/> according to the defined rules,
        /// otherwise <c>false</c>.</returns>
        /// <seealso cref="FieldValue.CompareTo"/>
        public virtual bool Equals(FieldValue other) =>
            !(other is null) &&
            (ReferenceEquals(this, other) || QueryEquals(other));

        /// <summary>
        /// Returns a value indicating whether this instance is equal to a
        /// specified object.
        /// </summary>
        /// <param name="obj">The object to compare to this instance.</param>
        /// <returns><c>true</c> if <paramref name="obj"/> is an instance of
        /// <see cref="FieldValue"/> and equals to this instance according
        /// to the rules of <see cref="FieldValue.Equals(FieldValue)"/>,
        /// otherwise <c>false</c>.</returns>
        public override bool Equals(object obj) =>
            obj is FieldValue other && QueryEquals(other);

        /// <summary>
        /// Returns the hash code for this instance.
        /// </summary>
        /// <remarks>
        /// If two instances of <see cref="FieldValue"/> are equal according
        /// to the equality comparison by
        /// <see cref="FieldValue.Equals(FieldValue)"/> the hash codes will be
        /// identical.  The implementation is aimed for non-equal instances to
        /// return different hash codes, however this is not guaranteed and
        /// for two non-equal instances this method may return the same hash
        /// code.
        /// </remarks>
        /// <returns>A 32-bit signed integer hash code.</returns>
        public override int GetHashCode() => QueryHashCode();

        /// <summary>
        /// Determines whether two specified instances of
        /// <see cref="FieldValue"/> are equal.
        /// </summary>
        /// <remarks>
        /// The equality is determined by
        /// <see cref="FieldValue.Equals(FieldValue)"/>.
        /// </remarks>
        /// <param name="value1">The first instance to compare for equality,
        /// or <c>null</c>.
        /// </param>
        /// <param name="value2">The second instance to compare for equality,
        /// or <c>null</c>.
        /// </param>
        /// <returns><c>true</c> if two instances are equal or both are
        /// <c>null</c>, otherwise <c>false</c>.</returns>
        /// <seealso cref="FieldValue.Equals(FieldValue)"/>
        public static bool operator ==(FieldValue value1, FieldValue value2)
        {
            if (value1 is null)
            {
                return value2 is null;
            }

            return value1.Equals(value2);
        }

        /// <summary>
        /// Determines whether two specified instances of
        /// <see cref="FieldValue"/> are not equal.
        /// </summary>
        /// <remarks>
        /// The equality is determined by
        /// <see cref="FieldValue.Equals(FieldValue)"/>.
        /// </remarks>
        /// <param name="value1">The first instance to compare for
        /// non-equality, or <c>null</c>.
        /// </param>
        /// <param name="value2">The second instance to compare for
        /// non-equality, or <c>null</c>.
        /// </param>
        /// <returns><c>true</c> if two instances are not equal, otherwise
        /// <c>false</c>.</returns>
        /// <seealso cref="FieldValue.Equals(FieldValue)"/>
        public static bool operator !=(FieldValue value1,
            FieldValue value2) => !(value1 == value2);

        /// <summary>
        /// Determines whether one specified instance of
        /// <see cref="FieldValue"/> is less than another specified instance
        /// of <see cref="FieldValue"/>.
        /// </summary>
        /// <remarks>The comparison is done according to the rules of
        /// <see cref="FieldValue.CompareTo"/>.</remarks>
        /// <param name="value1">The first instance to compare.</param>
        /// <param name="value2">The second instance to compare.</param>
        /// <returns><c>true</c> if <paramref name="value1"/> is less than
        /// <paramref name="value2"/>, otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">At least one of
        /// <paramref name="value1"/> and <paramref name="value2"/> is
        /// <c>null</c>.</exception>
        /// <exception cref="NotSupportedException">Comparison between
        /// <paramref name="value1"/> and <paramref name="value2"/> is not
        /// supported.</exception>
        /// <seealso cref="FieldValue.CompareTo"/>
        public static bool operator <(FieldValue value1, FieldValue value2) =>
            !(value1 is null) ? value1.CompareTo(value2) < 0 :
                throw new ArgumentNullException(nameof(value1));

        /// <summary>
        /// Determines whether one specified instance of
        /// <see cref="FieldValue"/> is greater than another specified
        /// instance of <see cref="FieldValue"/>.
        /// </summary>
        /// <remarks>The comparison is done according to the rules of
        /// <see cref="FieldValue.CompareTo"/>.</remarks>
        /// <param name="value1">The first instance to compare.</param>
        /// <param name="value2">The second instance to compare.</param>
        /// <returns><c>true</c> if <paramref name="value1"/> is greater than
        /// <paramref name="value2"/>, otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentNullException">At least one of
        /// <paramref name="value1"/> and <paramref name="value2"/> is
        /// <c>null</c>.</exception>
        /// <exception cref="NotSupportedException">Comparison between
        /// <paramref name="value1"/> and <paramref name="value2"/> is not
        /// supported.</exception>
        /// <seealso cref="FieldValue.CompareTo"/>
        public static bool operator >(FieldValue value1, FieldValue value2) =>
            !(value1 is null) ? value1.CompareTo(value2) > 0 :
                throw new ArgumentNullException(nameof(value1));

        /// <summary>
        /// Determines whether one specified instance of
        /// <see cref="FieldValue"/> is less than or equal to another
        /// specified instance of <see cref="FieldValue"/>.
        /// </summary>
        /// <remarks>The comparison is done according to the rules of
        /// <see cref="FieldValue.CompareTo"/>.</remarks>
        /// <param name="value1">The first instance to compare.</param>
        /// <param name="value2">The second instance to compare.</param>
        /// <returns><c>true</c> if <paramref name="value1"/> is less than
        /// or equal to <paramref name="value2"/>, otherwise <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">At least one of
        /// <paramref name="value1"/> and <paramref name="value2"/> is
        /// <c>null</c>.</exception>
        /// <exception cref="NotSupportedException">Comparison between
        /// <paramref name="value1"/> and <paramref name="value2"/> is not
        /// supported.</exception>
        /// <seealso cref="FieldValue.CompareTo"/>
        public static bool operator <=(FieldValue value1,
            FieldValue value2) => !(value1 is null) ?
                value1.CompareTo(value2) <= 0 :
                throw new ArgumentNullException(nameof(value1));

        /// <summary>
        /// Determines whether one specified instance of
        /// <see cref="FieldValue"/> is greater than or equal to another
        /// specified instance of <see cref="FieldValue"/>.
        /// </summary>
        /// <remarks>The comparison is done according to the rules of
        /// <see cref="FieldValue.CompareTo"/>.</remarks>
        /// <param name="value1">The first instance to compare.</param>
        /// <param name="value2">The second instance to compare.</param>
        /// <returns><c>true</c> if <paramref name="value1"/> is greater than
        /// or equal to <paramref name="value2"/>, otherwise <c>false</c>.
        /// </returns>
        /// <exception cref="ArgumentNullException">At least one of
        /// <paramref name="value1"/> and <paramref name="value2"/> is
        /// <c>null</c>.</exception>
        /// <exception cref="NotSupportedException">Comparison between
        /// <paramref name="value1"/> and <paramref name="value2"/> is not
        /// supported.</exception>
        /// <seealso cref="FieldValue.CompareTo"/>
        public static bool operator >=(FieldValue value1,
            FieldValue value2) => !(value1 is null) ?
                value1.CompareTo(value2) >= 0 :
                throw new ArgumentNullException(nameof(value1));
    }

}
