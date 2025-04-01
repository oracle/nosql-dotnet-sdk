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
    /// The exception that is thrown if the service does not support the
    /// current query protocol version.
    /// </summary>
    /// <remarks>
    /// This exception indicates that the service is running at a lower
    /// query protocol version than the driver (i.e. the driver is using a
    /// newer query protocol version than the service supports). The driver
    /// will attempt to decrement its internal query protocol version and
    /// retry the operation. If the query protocol version cannot be
    /// decremented, this exception will be thrown. This exception is not
    /// retryable (it is only retried internally and should not be retried by
    /// the retry handler).
    /// </remarks>
    public class UnsupportedQueryVersionException : NoSQLException
    {
        /// <summary>
        /// Initializes a new instance of
        /// <see cref="UnsupportedQueryVersionException"/>.
        /// </summary>
        public UnsupportedQueryVersionException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="UnsupportedQueryVersionException"/> with the message
        /// that describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public UnsupportedQueryVersionException(string message) :
            base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="UnsupportedQueryVersionException"/> with the message
        /// that describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public UnsupportedQueryVersionException(string message,
            Exception inner) : base(message, inner)
        {
        }
    }
}
