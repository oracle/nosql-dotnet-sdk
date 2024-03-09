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
    /// The exception that is thrown when table size limit has been exceeded
    /// by writing more data than the table can support.
    /// </summary>
    /// <remarks>
    /// This exception is not retryable because the conditions that lead to it
    /// being thrown, while potentially transient, typically require user
    /// intervention.
    /// </remarks>
    public class TableSizeLimitException : NoSQLException
    {
        /// <summary>
        /// Initializes a new instance of
        /// <see cref="TableSizeLimitException"/>.
        /// </summary>
        public TableSizeLimitException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="TableSizeLimitException"/> with the message that
        /// describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public TableSizeLimitException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="TableSizeLimitException"/> with the message that
        /// describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public TableSizeLimitException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
