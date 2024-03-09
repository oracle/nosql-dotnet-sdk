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
    /// Cloud Service only.  The exception that is thrown when security
    /// information is not ready in the system.
    /// </summary>
    /// <remarks>
    /// This exception will occur as the system acquires security information
    /// and must be retried in order for authorization to work properly.  It
    /// is automatically retried by <see cref="NoSQLRetryHandler"/>.
    /// </remarks>
    /// <seealso cref="NoSQLRetryHandler"/>
    public class SecurityInfoNotReadyException : RetryableException
    {
        /// <summary>
        /// Initializes a new instance of
        /// <see cref="SecurityInfoNotReadyException"/>.
        /// </summary>
        public SecurityInfoNotReadyException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="SecurityInfoNotReadyException"/> with the message that
        /// describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public SecurityInfoNotReadyException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="SecurityInfoNotReadyException"/> with the message that
        /// describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public SecurityInfoNotReadyException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
