/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

    /// <summary>
    /// The exception thrown when a prepared query must be prepared again
    /// before it can execute.
    /// </summary>
    /// <remarks>
    /// An advanced query requires the topology for the store identified by
    /// its prepared statement. If that topology is unavailable, prepare the
    /// query again using the client that will execute it. This is also required
    /// if protocol negotiation falls back to a version that cannot represent
    /// the prepared statement's store identity. Retrying the same
    /// prepared statement without preparing again does not resolve the error.
    /// </remarks>
    public class PrepareQueryException : NoSQLException
    {
        /// <summary>
        /// Initializes a new instance of <see cref="PrepareQueryException"/>.
        /// </summary>
        public PrepareQueryException()
        {
        }

        /// <summary>
        /// Initializes a new instance with the specified message.
        /// </summary>
        /// <param name="message">The exception message.</param>
        public PrepareQueryException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance with a message and an inner exception.
        /// </summary>
        /// <param name="message">The exception message.</param>
        /// <param name="inner">The inner exception.</param>
        public PrepareQueryException(string message, Exception inner) :
            base(message, inner)
        {
        }
    }
}
