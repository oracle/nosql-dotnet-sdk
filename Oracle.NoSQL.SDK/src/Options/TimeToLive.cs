/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using static ValidateUtils;

    /// <summary>
    /// Represents time unit used in a time to live (TTL) value.
    /// </summary>
    /// <seealso cref="TimeToLive"/>
    public enum TTLTimeUnit : byte
    {
        /// <summary>
        /// TTL is in hours.
        /// </summary>
        Hours = 1,

        /// <summary>
        /// TTL is in days.
        /// </summary>
        Days = 2
    }

    /// <summary>
    /// Represents time to live (TTL) of a row in the NoSQL Database.
    /// </summary>
    /// <remarks>
    /// <see cref="TimeToLive"/> is used used to specify time to live (TTL)
    /// for rows provided for Put operations such as
    /// <see cref="NoSQLClient.PutAsync"/> and others.
    /// <para>
    /// TTL is restricted to durations of days and hours, with day being 24
    /// hours.  <see cref="TimeToLive"/> may specify number of days or number
    /// of hours, but not both and may be created with methods
    /// <see cref="TimeToLive.OfDays"/> and <see cref="TimeToLive.OfHours"/>.
    /// </para>
    /// <para>
    /// Sometimes you may need to indicate explicitly that the record does not
    /// expire.  This is needed when you perform a Put operation on existing
    /// record and want to remove its expiration.  You may indicate no
    /// expiration by using the value <see cref="TimeToLive.DoNotExpire"/>.
    /// </para>
    /// <para>
    /// The record expiration time is determined as follows:<br/>
    /// Records expire on day or hour boundaries, depending on whether
    /// the <see cref="TimeToLive"/> instance is created with days or hours.
    /// At the time of the Put operation, <see cref="PutOptions.TTL"/>
    /// property is used to compute the record's expiration time by first
    /// converting it from days (or hours) to milliseconds, and then adding it
    /// to the current system time.  If the resulting expiration time is not
    /// evenly divisible by the number of milliseconds in one day (or hour),
    /// it is rounded up to the nearest day (or hour).  The day and hour
    /// boundaries (the day boundary is at midnight) are considered in UTC
    /// time zone.<br/>
    /// The minimum TTL that can be specified is 1 hour.  Because of the
    /// rounding behavior described above, the actual record duration will be
    /// longer than specified in the TTL (because of rounding up).
    /// </para>
    /// <para>
    /// Using the duration of days is recommended as it will result in the
    /// least amount of storage overhead compared to the duration of hours.
    /// </para>
    /// </remarks>
    public readonly struct TimeToLive
    {
        private static readonly TimeSpan OneDay = TimeSpan.FromDays(1);
        private static readonly TimeSpan OneHour = TimeSpan.FromHours(1);

        private TimeToLive(long value, TTLTimeUnit timeUnit)
        {
            Value = value;
            TimeUnit = timeUnit;
        }

        private static DateTime RoundUpDateTime(DateTime value,
            TimeSpan interval) =>
            new DateTime(
            (value.Ticks + interval.Ticks - 1) / interval.Ticks *
            interval.Ticks,
            value.Kind);

        /// <summary>
        /// Represents a special TTL value that indicates no expiration.
        /// </summary>
        /// <remarks>
        /// Use this value when you perform a Put operation on existing row
        /// that has TTL set on it and you want to remove its TTL.
        /// </remarks>
        public static readonly TimeToLive DoNotExpire =
            new TimeToLive(0, TTLTimeUnit.Days);

        /// <summary>
        /// Gets the number of days or hours in this instance.
        /// </summary>
        /// <value>
        /// Number of days or hours.
        /// </value>
        public long Value { get; }

        /// <summary>
        /// Gets the time unit used in this instance.
        /// </summary>
        /// <value>
        /// The time unit that indicates whether this instance represents TTL
        /// in days or hours.
        /// </value>
        public TTLTimeUnit TimeUnit { get; }

        /// <summary>
        /// Returns the string representation of this instance.
        /// </summary>
        /// <returns>String representation, e.g. "5 days" or "5 hours"
        /// </returns>
        public override string ToString() => Value +
            (TimeUnit == TTLTimeUnit.Days ? " days" : " hours");

        /// <summary>
        /// Creates TTL with duration of days.
        /// </summary>
        /// <param name="value">Number of days.  Must be a positive value.
        /// </param>
        /// <returns>TTL object representing specified duration in days.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">If
        /// <paramref name="value"/> is negative or zero.</exception>
        public static TimeToLive OfDays(long value)
        {
            CheckPositiveInt64(value, nameof(value));
            return new TimeToLive(value, TTLTimeUnit.Days);
        }

        /// <summary>
        /// Creates TTL with duration of hours.
        /// </summary>
        /// <param name="value">Number of hours.  Must be a positive value.
        /// </param>
        /// <returns>TTL object representing specified duration in hours.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">If
        /// <paramref name="value"/> is negative or zero.</exception>
        public static TimeToLive OfHours(long value)
        {
            CheckPositiveInt64(value, nameof(value));
            return new TimeToLive(value, TTLTimeUnit.Hours);
        }

        /// <summary>
        /// Converts TTL to absolute expiration time.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method converts TTL to absolute expiration time given the
        /// reference time from which to measure the expiration.  If the
        /// reference time is not provided, it defaults to current time.  The
        /// semantics follows the rounding behavior described so that the
        /// returned value will be rounded up to the nearest hour or day
        /// boundary.
        /// </para>
        /// <para>
        /// The value returned is always in UTC (its
        /// <see cref="DateTime.Kind"/> is <see cref="DateTimeKind.Utc"/>). If
        /// <paramref name="referenceTime"/> is not in UTC it will be converted
        /// to UTC via <see cref="DateTime.ToUniversalTime"/> before
        /// computing the expiration time (this also means that both
        /// <see cref="DateTimeKind.Unspecified"/> and
        /// <see cref="DateTimeKind.Local"/> will be treated as local time
        /// and the conversion would be performed).
        /// </para>
        /// </remarks>
        /// <param name="referenceTime">(Optional) Reference time.  Defaults
        /// to <see cref="DateTime.UtcNow"/>.</param>
        /// <returns>Expiration time, in UTC.</returns>
        /// <exception cref="NotSupportedException">If this instance is
        /// <see cref="TimeToLive.DoNotExpire"/>.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If the resulting
        /// expiration time is greater than <see cref="DateTime.MaxValue"/>.
        /// </exception>
        public DateTime ToExpirationTime(DateTime? referenceTime = null)
        {
            if (Value == 0)
            {
                throw new NotSupportedException(
                    "ToExpirationTime is not supported for " +
                    "TimeToLive.DoNotExpire");
            }

            var startTime = referenceTime?.ToUniversalTime() ?? DateTime.UtcNow;
            var interval = TimeUnit == TTLTimeUnit.Days ? OneDay : OneHour;
            return RoundUpDateTime(startTime + Value * interval, interval);
        }

        /// <summary>
        /// Constructs TTL from absolute expiration time given reference time
        /// from which to measure record expiration.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The TTL is computed as follows depending on whether
        /// <paramref name="timeUnit"/> parameter is specified:
        /// <list type="bullet"/>
        /// <item>
        /// <description>
        /// If <paramref name="timeUnit"/> is specified, expiration time is
        /// rounded up to the nearest day or hour boundary depending on the
        /// value of <paramref name="timeUnit"/>.  Then the duration is
        /// computed as the difference between the adjusted expiration time
        /// and the reference time and is rounded up to the nearest day or
        /// hour boundary depending on the value of
        /// <paramref name="timeUnit"/>.  TTL with the resulting number of
        /// days or hours and specified <paramref name="timeUnit"/> is
        /// returned.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// If <paramref name="timeUnit"/> is not specified, expiration time
        /// is rounded up to the nearest hour boundary.  If the adjusted
        /// expiration time indicates midnight in UTC time zone, the returned
        /// TTL will be in days, otherwise it will be in hours.  Then the
        /// duration is computed as the difference between the adjusted
        /// expiration time and the reference time and is rounded up to the
        /// nearest day or hour boundary as determined above.  TTL with the
        /// resulting number of days or hours and the time unit as determined
        /// above is returned.
        /// </description>
        /// </item>
        /// </para>
        /// <para>
        /// If the reference time is not specified, it defaults to current
        /// time in UTC (<see cref="DateTime.UtcNow"/>).  The expiration time
        /// specified must be later than the reference time.
        /// </para>
        /// <para>
        /// If not in UTC already, <paramref name="expirationTime"/> and
        /// <paramref name="referenceTime"/> (if specified) are converted to
        /// UTC via <see cref="DateTime.ToUniversalTime"/> before computing
        /// the returned TTL (this also means that both
        /// <see cref="DateTimeKind.Unspecified"/> and
        /// <see cref="DateTimeKind.Local"/> will be treated as local time
        /// and the conversion would be performed).
        /// </para>
        /// </remarks>
        /// <param name="expirationTime">Expiration time.  Must be greater
        /// than <paramref name="referenceTime"/>.</param>
        /// <param name="referenceTime">(Optional) Reference time.  Defaults
        /// to <see cref="DateTime.UtcNow"/>.</param>
        /// <param name="timeUnit">(Optional) Specifies
        /// <see cref="TimeToLive.TimeUnit"/> of the resulting TTL.  If not
        /// specified, the time unit is determined as described.</param>
        /// <returns>TTL computed according to the description.</returns>
        /// <exception cref="ArgumentException">If <paramref name="timeUnit"/>
        /// has invalid value.</exception>
        /// <exception cref="ArgumentOutOfRangeException">If
        /// <paramref name="expirationTime"/> is less then or equal
        /// <paramref name="referenceTime"/> (or current UTC time if
        /// <paramref name="referenceTime"/> is not specified).</exception>
        public static TimeToLive FromExpirationTime(
            DateTime expirationTime,
            DateTime? referenceTime = null,
            TTLTimeUnit? timeUnit = null)
        {
            if (timeUnit.HasValue)
            {
                CheckEnumValue(timeUnit.Value);
            }

            var startTime =
                referenceTime?.ToUniversalTime() ?? DateTime.UtcNow;
            var endTime = expirationTime.ToUniversalTime();

            if (endTime <= startTime)
            {
                throw new ArgumentOutOfRangeException(
                    $"{nameof(expirationTime)} cannot be prior or equal to " +
                    nameof(referenceTime));
            }

            endTime = RoundUpDateTime(endTime,
                timeUnit.HasValue && timeUnit.Value == TTLTimeUnit.Days ?
                    OneDay : OneHour);

            var duration = endTime - startTime;
            var timeUnitToUse = timeUnit ??
                (endTime.Hour == 0 ? TTLTimeUnit.Days : TTLTimeUnit.Hours);
            var interval = timeUnitToUse == TTLTimeUnit.Days ?
                OneDay : OneHour;

            return new TimeToLive((long)Math.Ceiling(duration / interval),
                timeUnitToUse);
        }
    }

}
