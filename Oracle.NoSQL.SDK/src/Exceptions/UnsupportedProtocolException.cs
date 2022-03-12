/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

    /// <summary>
    /// The exception that is thrown if the service does not support the
    /// current driver protocol version.
    /// </summary>
    /// <remarks>
    /// This exception indicates that the service is running at a lower
    /// protocol version than the driver (i.e. the driver is using a newer
    /// protocol version than the service supports).  The driver will attempt
    /// to decrement its internal protocol version and retry the operation.
    /// If the retries fail, this exception will be thrown.  This exception is
    /// not retryable (it is only retried internally and should not be retried
    /// by the retry handler).
    /// </remarks>
    public class UnsupportedProtocolException : NoSQLException
    {
        /// <summary>
        /// Initializes a new instance of
        /// <see cref="UnsupportedProtocolException"/>.
        /// </summary>
        public UnsupportedProtocolException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="UnsupportedProtocolException"/> with the message that
        /// describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public UnsupportedProtocolException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="UnsupportedProtocolException"/> with the message that
        /// describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public UnsupportedProtocolException(string message, Exception inner) :
            base(message, inner)
        {
        }
    }
}
