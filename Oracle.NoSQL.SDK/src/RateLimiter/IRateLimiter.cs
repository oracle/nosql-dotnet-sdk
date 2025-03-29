/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Cloud Service or Cloud Simulator only.
    /// An interface to handle rate limiting in the SDK.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Rate limiting is used to provide efficient access to Oracle NoSQL
    /// Database Cloud Service, maximize throughput and protect from resource
    /// starvation.  The SDK provides built-in rate limiting functionality for
    /// data operations on tables such as <see cref="NoSQLClient.GetAsync"/>,
    /// <see cref="NoSQLClient.PutAsync"/>,
    /// <see cref="NoSQLClient.DeleteAsync"/>,
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.DeleteRangeAsync*"/>,
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/>,
    /// <see cref="NoSQLClient.PrepareAsync"/>,
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> and their
    /// variants.  Rate limiting in the driver allows spreading the operations
    /// over time thus maximizing throughput by avoiding costly throttling
    /// exceptions (see <see cref="ReadThrottlingException"/> and
    /// <see cref="WriteThrottlingException"/>) and the associated retry
    /// handling.
    /// </para>
    /// <para>
    /// Each operation listed above does reads and/or writes on a table and
    /// thus consumes certain number of read and write units (see
    /// <see cref="ConsumedCapacity"/>).  Each table in NoSQL Database Cloud
    /// Service defines limits on the maximum number of read and write units
    /// that can be consumed by all clients accessing the table, per 1 second
    /// time.  For provisioned tables (see
    /// <see cref="CapacityMode.Provisioned"/>) these limits are defined in
    /// <see cref="TableLimits"/>, for on-demand tables (see
    /// <see cref="CapacityMode.OnDemand"/>), higher predefined limits are
    /// used. Thus, the total rate of operations on a given table is limited.
    /// If a client tries to perform the operations at faster rate, the
    /// service will respond with a read or write throttling exception, in
    /// which case the client can retry the operation after certain time
    /// (this behavior may be defined by <see cref="NoSQLRetryHandler"/>).
    /// Handling throttling exceptions and their retries is costly both for
    /// the service and the client application since the application has to
    /// wait before an operation can be retried (retrying immediately or not
    /// waiting enough will only result in another throttling exception and
    /// more load on the service).  A much better strategy for a client is to
    /// spread out the operations over time to an extent that the table limits
    /// for a given table are not exceeded and thus avoiding throttling errors
    /// and their retries.  Rate limiting built in to the SDK provides this
    /// functionality.
    /// </para>
    /// <para>
    /// Enable rate limiting by setting the property
    /// <see cref="NoSQLConfig.RateLimitingEnabled"/> of the initial
    /// configuration to <c>true</c>.
    /// </para>
    /// <para>
    /// An instance of <see cref="IRateLimiter"/> is used to enforce one table
    /// limit.  Thus the driver will instantiate two instances of
    /// <see cref="IRateLimiter"/> for each table in use, one for read limit
    /// and another for write limit.  You have a choice of using a default
    /// implementation of rate limiter provided by the SDK or providing a
    /// custom implementation of <see cref="IRateLimiter"/> interface.
    /// See <see cref="NoSQLRateLimiter"/> for details on default rate limiter
    /// used by the SDK.  To use custom rate limiter implementation, set
    /// property <see cref="NoSQLConfig.RateLimiterCreator"/> of the initial
    /// configuration, which is factory function to create your custom
    /// instances of <see cref="IRateLimiter"/>.
    /// </para>
    /// <para>
    /// In order to create pair of rate limiters for a table, the driver will
    /// need to know its table limits.  The table limits are already known if
    /// one of table APIs have been called, such as
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>,
    /// <see cref="NoSQLClient.SetTableLimitsAsync"/>,
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetTableAsync*"/>,
    /// <see cref="TableResult.WaitForCompletionAsync"/> or any of their
    /// variants. Otherwise, the driver will call
    /// <see cref="NoSQLClient.GetTableAsync(string,GetTableOptions,CancellationToken)"/>
    /// in the background to obtain the table limits as soon as any data
    /// operation is issued for that table.  This means that enabling rate
    /// limiting for a table may be delayed until its table limits are
    /// obtained successfully in the background.
    /// </para>
    /// <para>
    /// The main operation of rate limiting in the driver is to call
    /// <see cref="ConsumeUnitsAsync"/> to consume a number of (read or write)
    /// units for a given operation.  Depending on rate limiting
    /// implementation, its current state and the number of units to
    /// consume, this call may asynchronously block (sleep) for certain amount
    /// of time (and return <see cref="Task"/> returning this amount of time)
    /// before letting the operation proceed.  This API also needs to
    /// correctly operate in the presence of timeout set by the caller.  Note
    /// that it may be possible to consume certain amount of units without
    /// blocking (e.g. if there has been no or very little recent activity).
    /// In this state the rate limiter is said to be under its limit.
    /// Conversely, even consuming 0 units may block as a result of consuming
    /// units for recent past operations.  In this state the rate limiter is
    /// said to be over its limit.
    /// </para>
    /// <para>
    /// Rate limiting works best when we know in advance how many units each
    /// operation will consume, which would allow to know precisely how long
    /// to wait before issuing each operation.  Unfortunately, we don't know
    /// how many units an operation requires until we get the result back,
    /// with this information returned as part of
    /// <see cref="ConsumedCapacity"/>.  It may be difficult or impossible to
    /// estimate number of units required before the operation completes.  The
    ///approach is taken where <see cref="ConsumeUnitsAsync"/> is called
    /// twice, once before the operation passing 0 units to (possibly) wait
    /// until the rate limiter is under its limit and then after the operation
    /// passing the number of units returned as part of
    /// <see cref="ConsumedCapacity"/> of the result.  This will allow to
    /// delay subsequent operations and stagger subsequent concurrent
    /// operations over time.
    /// </para>
    /// <para>
    /// Note that by default the rate limiting only works as expected when
    /// used within one <see cref="NoSQLClient"/> instance.  When using
    /// multiple <see cref="NoSQLClient"/> instances against the same service,
    /// whether in the same process, different process or even running on
    /// different machines, rate limiters in one instance will not be aware of
    /// operations performed by other instances and thus will not correctly
    /// rate limit the operations.  If multiple concurrent
    /// <see cref="NoSQLClient"/> instances are required, you can set
    /// <see cref="NoSQLConfig.RateLimiterPercent"/> property of the
    /// initial configuration to allocate only a percentage of each table's
    /// limits to each <see cref="NoSQLClient"/> instance.  Although not
    /// optimal (not accounting for under-utilization at some instances), this
    /// will allow to rate limit operations on multiple concurrent instances.
    /// </para>
    /// <para>
    ///  Unfortunately, there is no perfect driver-side rate limiting strategy
    /// so it is still possible for throttling exceptions to occur.
    /// <see cref="IRateLimiter"/> provides
    /// <see cref="HandleThrottlingException"/> method that the driver will
    /// call when an operation results in throttling error.  When creating
    /// custom rate limiter, implementing this method will allow you to adjust
    /// the rate limiter's state to account for this information.
    /// </para>
    /// <para>
    /// Instances of <see cref="IRateLimiter"/> are used by multiple
    /// concurrent threads and thus must be thread-safe.
    /// </para>
    /// </remarks>
    /// <seealso cref="NoSQLRateLimiter"/>
    /// <seealso cref="NoSQLConfig.RateLimitingEnabled"/>
    /// <see cref="NoSQLConfig.RateLimiterPercent"/>
    /// <seealso cref="NoSQLConfig.RateLimiterCreator"/>
    public interface IRateLimiter
    {
        /// <summary>
        /// Consumes the specified number of units and blocks (sleeps)
        /// asynchronously if required before an operation can proceed.
        /// </summary>
        /// <param name="units">Number of units to consume.</param>
        /// <param name="timeout">
        /// Timeout. The resulting amount of time to sleep should not exceed
        /// this timeout.  If the sleep time exceeds this timeout, the
        /// behavior should be according to <paramref name="consumeOnTimeout"/>
        /// parameter.
        /// </param>
        /// <param name="consumeOnTimeout">
        /// Defines how rate limiter behaves when the timeout is reached. If
        /// <c>false</c>, this method should throw
        /// <see cref="TimeoutException"/> and the units should not be
        /// consumed. If <c>true</c>, the units should be consumed even when
        /// timeout is reached and this method should return successfully. In
        /// either case, if the computed wait time exceeds timeout, this
        /// method should still block (sleep) for the amount of time equal to
        /// the timeout before either throwing an exception or returning
        /// successfully (depending on the value of this parameter).  The
        /// driver uses the value <c>true</c> when calling
        /// <see cref="ConsumeUnitsAsync"/> after an operation completes
        /// successfully (see description in remarks), in which case the
        /// exception should not be thrown and the result of the operation
        /// should be returned to the application.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task returning <see cref="TimeSpan"/> of the amount of time
        /// blocked (slept) by this method.
        /// </returns>
        /// <exception cref="TimeoutException">
        /// If the specified timeout is reached and
        /// <paramref name="consumeOnTimeout"/> is <c>false</c>.
        /// </exception>
        Task<TimeSpan> ConsumeUnitsAsync(int units, TimeSpan timeout,
            bool consumeOnTimeout, CancellationToken cancellationToken);

        /// <summary>
        /// Configures rate limiter by setting its limit in units per second.
        /// </summary>
        /// <remarks>
        /// Note that this method is called both when rate limiter is
        /// configured for the first time and also if/when table limits
        /// change, so it may need to account for the current state due to
        /// outstanding operations already rate-limited by this instance,
        /// however there is no need to change state or block (sleep) time of
        /// these outstanding operations and the new limit only needs to apply
        /// to the new operations issued.
        /// </remarks>
        /// <param name="limitPerSecond">Limit in units per second</param>
        void SetLimit(double limitPerSecond);

        /// <summary>
        /// Defines the behavior of the rate limiter when throttling exception
        /// occurs.
        /// </summary>
        /// <remarks>
        /// If throttling exception has occurred, this usually means that
        /// current rate limiter state does not correctly reflect the rate of
        /// incoming operations and needs to be adjusted.  For example, you
        /// may remove any unused credits that were previously used to allow
        /// operations to proceed without waiting.
        /// </remarks>
        /// <param name="ex">
        /// An exception, which is instance of either
        /// <see cref="ReadThrottlingException"/> or
        /// <see cref="WriteThrottlingException"/>
        /// </param>
        void HandleThrottlingException(RetryableException ex);
    }

}
