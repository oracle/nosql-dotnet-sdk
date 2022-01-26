/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;
    using System.Diagnostics;
    using static ValidateUtils;

    /// <summary>
    /// Represents the built-in retry handler.
    /// </summary>
    /// <remarks>
    /// This is the default retry handler used by the driver, but it can
    /// also be customized via the provided properties.  The general semantics
    /// is as follows:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// The operations are retried up to the maximum number of times indicated by
    /// <see cref="MaxRetryAttempts"/> property (with the default value of 10).
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// The retry delay follows the exponential back-off.  The initial base
    /// delay is given by <see cref="BaseDelay"/> property (with the default
    /// value of 1 second).  The total delay is computed as a sum of the base
    /// delay component which starts with <see cref="BaseDelay"/> and
    /// increases two-fold on each retry and a small random delay between 0
    /// and initial  <see cref="BaseDelay"/>.
    /// </description>
    /// </item>
    /// </list>
    /// However, there are several exceptions:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// Operations that fail with
    /// <see cref="ControlOperationThrottlingException"/> use much longer base
    /// delay given by <see cref="ControlOperationBaseDelay"/> property.
    /// These are usually table DDL operations that have much more stringent
    /// rate limitations in the cloud and thus should be retried much less
    /// often.  The delay algorithm is still exponential back-off but uses
    /// the base delay of <see cref="ControlOperationBaseDelay"/> (with the
    /// default value of 1 minute).
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Network-related exceptions and
    /// <see cref="SecurityInfoNotReadyException"/> are always retried (up to
    /// the operation timeout) regardless of the number of retries done so
    /// far (thus ignoring <see cref="MaxRetryAttempts"/>).
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// For <see cref="SecurityInfoNotReadyException"/> the delay algorithm
    /// starts with constant delays of 1 second up to 10 retries and after
    /// that follows the exponential back-off.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <see cref="InvalidAuthorizationException"/> will be retried as long as
    /// the exception on the previous retry (if any) was not
    /// <see cref="InvalidAuthorizationException"/>.
    /// <see cref="InvalidAuthorizationException"/> may indicate that the
    /// authentication token is expired in which case the operation should be
    /// retried, which will involve the refresh of the authentication token.
    /// On the other hand, if subsequent retries fail with
    /// <see cref="InvalidAuthorizationException"/>, this most likely
    /// indicates invalid credentials in which case the operation should not
    /// be retried again.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    /// <example>
    /// Instantiating <see cref="NoSQLRetryHandler"/> with the customized
    /// parameters.
    /// <code>
    /// var config = new NoSQLConfig();
    /// ...
    /// config.RetryHandler = new NoSQLRetryHandler
    /// {
    ///     MaxRetryAttempts = 20,
    ///     BaseDelay = TimeSpan.FromSeconds(2),
    ///     ControlOperationBaseDelay = TimeSpan.FromSeconds(30),
    ///     SecurityInfoConstantDelayRetries = 50
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="IRetryHandler"/>
    public class NoSQLRetryHandler : IRetryHandler
    {
        private static TimeSpan BackOffDelay(int retryCount, TimeSpan baseDelay)
        {
            Debug.Assert(retryCount >= 1);
            var baseMs = (int)baseDelay.TotalMilliseconds;
            var ms = (1 << (retryCount - 1)) * baseMs +
                StaticRandom.Next(baseMs);
            return TimeSpan.FromMilliseconds(ms);
        }

        /// <summary>
        /// Gets or sets the maximum number of retries.
        /// </summary>
        /// <remarks>
        /// See the remarks section of <see cref="NoSQLRetryHandler"/> for
        /// special cases.  After the maximum number of retries is reached,
        /// <see cref="ShouldRetry"/> returns <c>false</c>.  Note that the
        /// retry count also includes the first (original) operation
        /// invocation
        /// </remarks>.
        /// <value>
        /// The maximum number of retries for most situations with the
        /// exceptions outlined in the remarks section of
        /// <see cref="NoSQLRetryHandler"/>.  The default is 10.
        /// </value>
        public int MaxRetryAttempts { get; set; } = 10;

        /// <summary>
        /// Gets or sets the base retry delay.
        /// </summary>
        /// <value>
        /// The base retry delay for the exponential back-off algorithm with
        /// the exceptions outlined in the remarks section of
        /// <see cref="NoSQLRetryHandler"/>.  The default is 1 second.
        /// </value>
        public TimeSpan BaseDelay { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the base retry delay when an operation fails with
        /// <see cref="ControlOperationThrottlingException"/>.
        /// </summary>
        /// <value>
        /// The base retry delay for the exponential back-off algorithm when
        /// an operation fails with
        /// <see cref="ControlOperationThrottlingException"/> as outlined in
        /// the remarks section of <see cref="NoSQLRetryHandler"/>.  The
        /// default is 60 seconds (1 minute).
        /// </value>
        public TimeSpan ControlOperationBaseDelay { get; set; } =
            TimeSpan.FromSeconds(60);

        // Made security info related settings invisible to the user as this
        // handling will be moved out of the driver.

        /// <summary>
        /// Gets or sets the base retry delay when an operation fails with
        /// <see cref="SecurityInfoNotReadyException"/>.
        /// </summary>
        /// <value>
        /// The base retry delay when an operation fails with
        /// <see cref="SecurityInfoNotReadyException"/> as outlined in
        /// the remarks section of <see cref="NoSQLRetryHandler"/>.  The
        /// default is 1 second.
        /// </value>
        internal TimeSpan SecurityInfoBaseDelay { get; set; } =
            TimeSpan.FromSeconds(1);

        /// <summary>
        /// Gets or sets the number of times the operation is retried
        /// with the constant delay when it fails with
        /// <see cref="SecurityInfoNotReadyException"/>.
        /// </summary>
        /// <value>
        /// The number of retries at which to switch from constant delay to
        /// the exponential back-off when an operation fails with
        /// <see cref="SecurityInfoNotReadyException"/> as outlined in the
        /// remarks section of <see cref="NoSQLRetryHandler"/>.  The default
        /// is 10.
        /// </value>
        internal int SecurityInfoConstantDelayRetries { get; set; } = 10;

        internal void Validate()
        {
            CheckNonNegativeInt32(MaxRetryAttempts, nameof(MaxRetryAttempts));
            CheckPositiveTimeSpan(BaseDelay, nameof(BaseDelay));
            CheckPositiveTimeSpan(ControlOperationBaseDelay,
                nameof(ControlOperationBaseDelay));
            CheckPositiveTimeSpan(SecurityInfoBaseDelay,
                nameof(SecurityInfoBaseDelay));
            CheckNonNegativeInt32(SecurityInfoConstantDelayRetries,
                nameof(SecurityInfoConstantDelayRetries));
        }

        /// <summary>
        /// Determines whether the operation should be retried.
        /// </summary>
        /// <remarks>
        /// You do not need to call this method.  It is called by the driver
        /// to determine whether the operation should be retried.
        /// </remarks>
        /// <param name="request">The <see cref="Request"/> object describing
        /// the running operation.</param>
        /// <returns><c>true</c> to retry the operation, otherwise
        /// <c>false</c>.</returns>
        /// <seealso cref="IRetryHandler.ShouldRetry"/>
        public bool ShouldRetry(Request request)
        {
            if (request.LastException is ControlOperationThrottlingException)
            {
                return request.Timeout > ControlOperationBaseDelay;
            }

            if (request.LastException is SecurityInfoNotReadyException ||
                request.Client.IsRetryableNetworkException(request.LastException))
            {
                return true;
            }

            if (request.LastException is InvalidAuthorizationException)
            {
                return !(request.PriorException is
                    InvalidAuthorizationException);
            }

            return request.ShouldRetry &&
                   request.RetryCount < MaxRetryAttempts;
        }

        /// <summary>
        /// Determines how long to wait between successive retries.
        /// </summary>
        /// <remarks>
        /// You do not need to call this method.  It is called by the driver
        /// to determine the delay before the next retry of the operation.
        /// </remarks>
        /// <param name="request">The <see cref="Request"/> object describing
        /// the running operation.</param>
        /// <returns>A time interval to wait before the next retry.</returns>
        /// <seealso cref="IRetryHandler.GetRetryDelay"/>
        public TimeSpan GetRetryDelay(Request request)
        {
            if (request.LastException is ControlOperationThrottlingException)
            {
                return BackOffDelay(request.RetryCount,
                    ControlOperationBaseDelay);
            }

            if (request.LastException is SecurityInfoNotReadyException)
            {
                var backOffCount = request.RetryCount -
                                   SecurityInfoConstantDelayRetries;
                return backOffCount > 0 ?
                    BackOffDelay(backOffCount, SecurityInfoBaseDelay) :
                    SecurityInfoBaseDelay;
            }

            return BackOffDelay(request.RetryCount, BaseDelay);
        }
    }

}
