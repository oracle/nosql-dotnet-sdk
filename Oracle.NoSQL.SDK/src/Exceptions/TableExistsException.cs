/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

    /// <summary>
    /// The exception that is thrown when attempting to a table when a table
    /// with the same name already exists.
    /// </summary>
    public class TableExistsException : NoSQLException
    {
        /// <summary>
        /// Initializes a new instance of <see cref="TableExistsException"/>.
        /// </summary>
        public TableExistsException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TableExistsException"/>
        /// with the message that describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public TableExistsException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TableExistsException"/>
        /// with the message that describes the current exception and an inner
        /// exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public TableExistsException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
