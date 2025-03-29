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
    /// Cloud Service only.
    /// The exception that is thrown by table DDL and add/drop replica
    /// operations that indicates that
    /// <see cref="TableDDLOptions.MatchETag"/> provided for the operation
    /// does not match the current ETag of the table.
    /// </summary>
    /// <remarks>
    /// This condition usually means that the table has been changed after the
    /// provided ETag was obtained.
    /// </remarks>
    /// <seealso cref="TableDDLOptions.MatchETag"/>
    public class ETagMismatchException : NoSQLException
    {

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ETagMismatchException"/>.
        /// </summary>
        public ETagMismatchException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ETagMismatchException"/> with the message that
        /// describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public ETagMismatchException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ETagMismatchException"/> with the message that
        /// describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public ETagMismatchException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
