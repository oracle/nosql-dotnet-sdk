/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

    /// <summary>
    /// Cloud Service/Cloud Simulator only.  The exception that is thrown when
    /// the provisioned read throughput has been exceeded.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exception is retryable and <see cref="NoSQLRetryHandler"/> uses
    /// progressive delays before retrying to minimize the chance that a retry
    /// is also throttled.
    /// </para>
    /// <para>
    /// In general, applications should attempt to avoid throttling exceptions
    /// by rate limiting themselves to the degree possible, because the rate
    /// limiting results in a better throughput than performing operation
    /// retries.
    /// </para>
    /// </remarks>
    /// <seealso cref="NoSQLRetryHandler"/>
    public class ReadThrottlingException : RetryableException
    {
        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ReadThrottlingException"/>.
        /// </summary>
        public ReadThrottlingException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ReadThrottlingException"/> with the message that
        /// describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public ReadThrottlingException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ReadThrottlingException"/> with the message that
        /// describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public ReadThrottlingException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
