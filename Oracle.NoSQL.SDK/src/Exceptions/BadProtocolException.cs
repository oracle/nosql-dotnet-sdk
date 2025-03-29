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
    /// The exception that is thrown when an invalid protocol message is
    /// received by the driver or the service.
    /// </summary>
    /// <remarks>
    /// This exception indicates communication problem between the driver and
    /// the service that resulted in the invalid protocol message received by
    /// either the driver or the service.  This exception may be due to the
    /// driver sending request to an invalid service endpoint, driver
    /// installation issues, environmental issues or other reasons.  This
    /// exception is not retryable.  Note that if the problem is because the
    /// service does not support the current driver protocol version,
    /// <see cref="UnsupportedProtocolException"/> will be thrown instead.
    /// </remarks>
    public class BadProtocolException : NoSQLException
    {
        /// <summary>
        /// Initializes a new instance of <see cref="BadProtocolException"/>.
        /// </summary>
        public BadProtocolException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="BadProtocolException"/>
        /// with the message that describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public BadProtocolException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="BadProtocolException"/>
        /// with the message that describes the current exception and an inner
        /// exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public BadProtocolException(string message, Exception inner) :
            base(message, inner)
        {
        }
    }
}
