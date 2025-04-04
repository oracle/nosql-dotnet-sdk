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
    /// The exception that indicates an internal service problem.
    /// </summary>
    /// <remarks>
    /// Most system problems are temporary, so this is a retryable exception.
    /// </remarks>
    public class ServerException : RetryableException
    {
        /// <summary>
        /// Initializes a new instance of <see cref="ServerException"/>.
        /// </summary>
        public ServerException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ServerException"/> with
        /// the message that describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public ServerException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="ServerException"/> with
        /// the message that describes the current exception and an inner
        /// exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public ServerException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
