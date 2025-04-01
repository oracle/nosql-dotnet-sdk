/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Cloud Service or Cloud Simulator only.
    /// Default rate limiter used by the SDK.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class implements default rate limiting algorithm used by the SDK.
    /// The driver will use it if rate limiting is enabled and you did not
    /// specify <see cref="NoSQLConfig.RateLimiterCreator"/> delegate.  You do
    /// not need to create or use instances of this class, the following is
    /// presented for information only.
    /// </para>
    /// <para>
    /// Two rate limiter instances are used per each table in use, one for
    /// reads and another for writes (see <see cref="IRateLimiter"/>).
    /// </para>
    /// <para>
    /// This implementation uses a token bucket based algorithm, although the
    /// state is kept in terms of time instead of tokens. It is assumed
    /// that the units refill at constant rate being equivalent to the set
    /// limit of units per second.  The state is kept in terms of
    /// <c>next</c>, which is the time when the next operation can proceed
    /// without waiting, meaning the limiter is at its limit.  All operations
    /// issued before <c>next</c> will have to wait accordingly.  Based on
    /// the value of <c>next</c> and the current time, the appropriate
    /// wait time may be computed before the wait begins.  If current time is
    /// greater than or equal to <c>next</c>, no wait is needed.
    /// </para>
    /// <para>
    /// Note that when <see cref="ConsumeUnitsAsync"/> is called, the entered
    /// units will only affect the wait time of subsequent operations and not
    /// the current operation, which will use the value of <c>next</c> as
    /// it currently is to determine the wait time.  This is done to avoid
    /// needless wait time when the operations come in rarely.
    /// </para>
    /// <para>
    /// Because every time when <see cref="ConsumeUnitsAsync"/> is called with
    /// positive number of units, <c>next</c> is pushed forward, the
    /// operations will be effectively staggered in time according to the
    /// order of their arrival with no preferential treatment given to any
    /// operation, thus avoiding starvation.
    /// </para>
    /// <para>
    /// This rate limiter uses burst mode, allowing a set maximum number of
    /// stored units that has not been used to be consumed immediately without
    /// waiting. This value is expressed as duration, which is effectively a
    /// maximum number of seconds worth of unused stored units. The minimum
    /// duration is internally bound such that at least one unused unit may
    /// be consumed without waiting.  The default value of duration is
    /// 30 seconds.
    /// </para>
    /// </remarks>
    public class NoSQLRateLimiter : IRateLimiter
    {
        private TimeSpan timePerUnit;
        private TimeSpan duration;
        private DateTime next;
        private bool removePast = true;
        private readonly object lockObj = new object();

        /// <summary>
        /// Initializes new instance of <see cref="NoSQLRateLimiter"/> with
        /// a default duration of 30 seconds.
        /// </summary>
        public NoSQLRateLimiter() : this(TimeSpan.FromSeconds(30))
        {
        }

        /// <summary>
        /// Initializes new instance of <see cref="NoSQLRateLimiter"/> with
        /// given a given duration.
        /// </summary>
        /// <param name="duration">Burst time (duration).</param>
        public NoSQLRateLimiter(TimeSpan duration)
        {
            this.duration = duration;
        }

        private TimeSpan ConsumeInternal(int units, DateTime now, TimeSpan timeout)
        {
            // If disabled, just return success.
            if (timePerUnit == TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            // Determine how much time we need to add based on units
            // requested.
            var timeNeeded = units * timePerUnit;

            // Ensure we never use more from the past than duration allows.
            DateTime maxPast;
            if (removePast)
            {
                maxPast = now;
                removePast = false;
            }
            else
            {
                maxPast = now - duration;
            }

            if (next < maxPast)
            {
                next = maxPast;
            }

            // Compute the new "next time used".
            var newNext = next + timeNeeded;

            // If units < 0, we're "returning" them.
            if (units < 0) {
                // Consume the units.
                next = newNext;
                return TimeSpan.Zero;
            }

            // If the limiter is currently under its limit, the consume
            // succeeds immediately (no sleep required).
            if (next <= now) {
                // Consume the units.
                next = newNext;
                return TimeSpan.Zero;
            }

            // Determine the amount of time that the caller needs to sleep for
            // this limiter to go below its limit. Note that the limiter is
            // not guaranteed to be below the limit after this time, as other
            // consume calls may come in after this one and push the "at the
            // limit time" further out.
            var sleepTime = next - now;

            if (timeout == TimeSpan.Zero || sleepTime < timeout)
            {
                next = newNext;
            }

            return sleepTime;
        }

        /// <inheritdoc cref="IRateLimiter.ConsumeUnitsAsync"/>
        public async Task<TimeSpan> ConsumeUnitsAsync(int units,
            TimeSpan timeout, bool consumeOnTimeout,
            CancellationToken cancellationToken)
        {
            TimeSpan timeToSleep;
            lock (lockObj)
            {
                timeToSleep = ConsumeInternal(units, DateTime.UtcNow,
                    consumeOnTimeout ? TimeSpan.Zero : timeout);
            }

            if (timeToSleep == TimeSpan.Zero)
            {
                return TimeSpan.Zero;
            }

            if (timeout != TimeSpan.Zero && timeToSleep >= timeout)
            {
                await Task.Delay(timeout, cancellationToken);
                if (consumeOnTimeout)
                {
                    return timeout;
                }

                throw new TimeoutException(
                    $"Rate limiter timed out waiting {timeout} for {units} units");
            }

            await Task.Delay(timeToSleep, cancellationToken);
            return timeToSleep;
        }

        /// <summary>
        /// Configures rate limiter by setting its limit in units per second.
        /// </summary>
        /// <remarks>
        /// This method also enforces minimum duration to be able to store at
        /// least one unused unit.  When changing table limits, this method
        /// will prorate any unused units according to the new limit.
        /// </remarks>
        /// <param name="limitPerSecond">Limit in units per second</param>
        public void SetLimit(double limitPerSecond)
        {
            lock (lockObj)
            {
                // If the limit is not positive, assume that the limiter is
                // disabled.
                if (limitPerSecond <= 0)
                {
                    timePerUnit = TimeSpan.Zero;
                    return;
                }

                var oldTimePerUnit = timePerUnit;
                timePerUnit = TimeSpan.FromSeconds(1) / limitPerSecond;

                if (duration < timePerUnit)
                {
                    duration = timePerUnit;
                }

                if (oldTimePerUnit != TimeSpan.Zero)
                {
                    var now = DateTime.UtcNow;
                    // Prorate any unused capacity.
                    if (next < now)
                    {
                        next = now - (now - next) *
                            (timePerUnit / oldTimePerUnit);
                    }
                }
            }
        }

        /// <summary>
        /// Defines the behavior of the rate limiter when throttling exception
        /// occurs.
        /// </summary>
        /// <remarks>
        /// Current implementation will remove any stored units by ensuring that
        /// <c>next</c>is at least the current time.
        /// </remarks>
        /// <param name="ex">
        /// An exception, which is instance of either
        /// <see cref="ReadThrottlingException"/> or
        /// <see cref="WriteThrottlingException"/>
        /// </param>
        public void HandleThrottlingException(RetryableException ex)
        {
            lock (lockObj)
            {
                removePast = true;
            }
        }
    }

}
