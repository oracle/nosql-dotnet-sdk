/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
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
    /// Represents a date and time value.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used to represent values of NoSQL data type
    /// <em>Timestamp</em>. This value is represented by a C# type
    /// <see cref="DateTime"/> indicating an instance of time in UTC.
    /// Instances of <see cref="TimestampValue"/> always represent their value
    /// in UTC.
    /// </para>
    /// <para>
    /// The values of this instance can also be converted to <c>long</c>
    /// representing number of milliseconds since Unix Epoch,
    /// 1970-01-01 00:00:00 UTC.  Reverse conversion is also supported (see
    /// <see cref="LongValue.ToDateTime"/>).
    /// </para>
    /// <para>
    /// In JSON, the instances of this
    /// class may be represented as either <em>String</em> or <em>Number</em>
    /// depending on the value of
    /// <see cref="JsonOutputOptions.DateTimeAsMillis"/>.  If represented as
    /// <em>String</em>, the format is given by
    /// <see cref="JsonOutputOptions.DateTimeFormatString"/>.  The default
    /// format is ISO8601 in UTC given by
    /// <see cref="JsonOutputOptions.DefaultDateTimeFormatString"/>.  If
    /// represented as <em>Number</em>, the value is the number of
    /// milliseconds since the Unix Epoch.
    /// </para>
    /// </remarks>
    /// <seealso cref="FieldValue"/>
    public class TimestampValue : FieldValue
    {
        private readonly DateTime value;

        /// <summary>
        /// Initializes a new instance of <see cref="TimestampValue"/>
        /// with the specified <see cref="DateTime"/> value.
        /// </summary>
        /// <remarks>
        /// The date and time value represented by this instance is obtained
        /// by converting the provided <paramref name="value"/> to UTC by
        /// <see cref="DateTime.ToUniversalTime"/> (no conversion is performed
        /// if the <see cref="DateTime.Kind"/> of <paramref name="value"/> is
        /// <see cref="DateTimeKind.Utc"/>.
        /// </remarks>
        /// <param name="value">A date and time value which this instance will
        /// represent.
        /// </param>
        /// <seealso cref="DateTime.ToUniversalTime"/>
        public TimestampValue(DateTime value)
        {
            this.value = value.ToUniversalTime();
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TimestampValue"/> with
        /// the specified number of milliseconds since the Unix Epoch.
        /// </summary>
        /// <remarks>
        /// This method performs the same conversion as
        /// <see cref="LongValue.ToDateTime"/>.  The resulting
        /// <see cref="DateTime"/> value is always in UTC.
        /// </remarks>
        /// <param name="value">The number of milliseconds since the Unix
        /// Epoch.</param>
        /// <exception cref="OverflowException">If <paramref name="value"/>
        /// milliseconds produces a <see cref="TimeSpan"/> value less than
        /// <see cref="TimeSpan.MinValue"/> or greater than
        /// <see cref="TimeSpan.MaxValue"/>.
        /// </exception>
        /// <exception cref="ArgumentOutOfRangeException">If the resulting
        /// <see cref="DateTime"/> value is less than
        /// <see cref="DateTime.MinValue"/> or greater than
        /// <see cref="DateTime.MaxValue"/></exception>
        /// <seealso cref="LongValue.ToDateTime"/>
        public TimestampValue(long value)
        {
            this.value = DateTimeUtils.UnixMillisToDateTime(value);
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TimestampValue"/> with
        /// the specified date and time string.
        /// </summary>
        /// <remarks>
        /// This method performs the same conversion as
        /// <see cref="Convert.ToDateTime(string)"/> and then converts the
        /// resulting <see cref="DateTime"/> to UTC using
        /// <see cref="DateTime.ToUniversalTime"/>.
        /// </remarks>
        /// <param name="value">A date and time string from which to
        /// initialize this instance.</param>
        /// <exception cref="FormatException">The <paramref name="value"/> is
        /// not a valid date and time string.
        /// </exception>
        /// <seealso cref="Convert.ToDateTime(string)"/>
        /// <seealso cref="DateTime.ToUniversalTime"/>
        public TimestampValue(string value)
        {
            this.value = Convert.ToDateTime(value).ToUniversalTime();
        }

        /// <inheritdoc cref="FieldValue.DbType" path="summary"/>
        /// <value>
        /// <see cref="SDK.DbType.Timestamp"/>
        /// </value>
        public override DbType DbType => DbType.Timestamp;

        /// <summary>
        /// Gets the value of this instance as date and time.
        /// </summary>
        /// <value>
        /// The <see cref="DateTime"/> value that this instance
        /// represents.
        /// </value>
        public override DateTime AsDateTime => value;

        /// <summary>
        /// Converts the value represented by this instance to a 64-bit sighed
        /// integer representing the number of milliseconds since the Unix
        /// Epoch.
        /// </summary>
        /// <remarks>
        /// This method performs reverse conversion to that done by
        /// <see cref="LongValue.ToDateTime"/>.  It returns the number of
        /// milliseconds since the Unix Epoch, 00:00:00 UTC, January 1, 1970,
        /// of the value represented by this instance.  If this instance
        /// represents date and time before the Unix Epoch, the returned value
        /// is negative.
        /// </remarks>
        /// <returns>A number of milliseconds since the Unix Epoch of the
        /// value represented by this instance.
        /// </returns>
        /// <seealso cref="LongValue.ToDateTime"/>
        public override long ToInt64() => DateTimeUtils.GetUnixMillis(value);

        /// <inheritdoc/>
        public override void SerializeAsJson(Utf8JsonWriter writer,
            JsonOutputOptions options = null)
        {
            if (options == null)
            {
                writer.WriteStringValue(value);
                return;
            }

            if (options.DateTimeAsMillis)
            {
                writer.WriteNumberValue(DateTimeUtils.GetUnixMillis(value));
                return;
            }

            if (options.DateTimeFormatString != null)
            {
                writer.WriteStringValue(value.ToString(
                    options.DateTimeFormatString));
                return;
            }

            writer.WriteStringValue(value);
        }

        internal override int QueryCompare(FieldValue other, int nullRank)
        {
            switch (other.DbType)
            {
                case DbType.Timestamp:
                    return AsDateTime.CompareTo(other.AsDateTime);
                case DbType.Boolean:
                case DbType.String:
                    return -1;
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
            return other.DbType == DbType.Timestamp &&
                   AsDateTime.Equals(other.AsDateTime);
        }

        internal override int QueryHashCode() => value.GetHashCode();

        internal override long GetMemorySize() => GetObjectSize(DateTimeSize);

    }

}
