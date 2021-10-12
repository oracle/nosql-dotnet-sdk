/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{
    using System;

    /// <summary>
    /// The exception that is thrown when attempting to create an index for a
    /// table when an index with the same name already exists.
    /// </summary>
    public class IndexExistsException : NoSQLException
    {
        /// <summary>
        /// Initializes a new instance of <see cref="IndexExistsException"/>.
        /// </summary>
        public IndexExistsException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="IndexExistsException"/>
        /// with the message that describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public IndexExistsException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="IndexExistsException"/>
        /// with the message that describes the current exception and an inner
        /// exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public IndexExistsException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
