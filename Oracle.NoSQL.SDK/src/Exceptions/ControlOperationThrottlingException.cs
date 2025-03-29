/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

    /// <summary>
    /// Cloud Service/Cloud Simulator only.  The exception that is thrown when
    /// a non-data operation is throttled.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exception may be thrown if an application attempts too many
    /// control operations such as table creation, deletion, or similar
    /// methods.  Such operations do not use the throughput or capacity
    /// provisioned for a given table but they consume system resources and
    /// their use is limited.
    /// </para>
    /// <para>
    /// This exception is retryable and the default
    /// <see cref="NoSQLRetryHandler"/> uses a large delay in order to
    /// minimize the change that a retry will also be throttled.  This delay
    /// can be configured as
    /// <see cref="NoSQLRetryHandler.ControlOperationBaseDelay"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="NoSQLRetryHandler.ControlOperationBaseDelay"/>
    public class ControlOperationThrottlingException : RetryableException
    {
        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ControlOperationThrottlingException"/>.
        /// </summary>
        public ControlOperationThrottlingException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ControlOperationThrottlingException"/> with the message
        /// that describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public ControlOperationThrottlingException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ControlOperationThrottlingException"/> with the message
        /// that describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public ControlOperationThrottlingException(string message,
            Exception inner)
            : base(message, inner)
        {
        }
    }
}
