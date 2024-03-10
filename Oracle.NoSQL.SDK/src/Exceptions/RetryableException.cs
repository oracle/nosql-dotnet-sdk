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
    /// Base class for all exceptions that may be retried with a reasonable
    /// expectation that the retry may succeed.
    /// </summary>
    /// <seealso cref="NoSQLException.IsRetryable"/>
    public abstract class RetryableException : NoSQLException
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RetryableException"/>.
        /// </summary>
        protected RetryableException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RetryableException"/>
        /// with the message that describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        protected RetryableException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RetryableException"/>
        /// with the message that describes the current exception and an inner
        /// exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        protected RetryableException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Gets the value indicating whether the operation that has thrown
        /// this exception may be retried.
        /// </summary>
        /// <value>
        /// For instances of <see cref="RetryableException"/> this value is
        /// always <c>true</c>.
        /// </value>
        public override bool IsRetryable => true;
    }
}
