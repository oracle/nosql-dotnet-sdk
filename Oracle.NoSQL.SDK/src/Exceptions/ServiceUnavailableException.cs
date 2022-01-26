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
    /// The exception that is thrown when the service is unavailable.
    /// </summary>
    /// <remarks>
    /// Most service problems are temporary, so this is a retryable exception.
    /// </remarks>
    public class ServiceUnavailableException : RetryableException
    {
        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ServiceUnavailableException"/>.
        /// </summary>
        public ServiceUnavailableException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ServiceUnavailableException"/> with the message that
        /// describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public ServiceUnavailableException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ServiceUnavailableException"/> with the message that
        /// describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public ServiceUnavailableException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
