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
    /// The exception that is thrown when an operation attempted to create a
    /// resource that already exists.
    /// </summary>
    public class ResourceExistsException : NoSQLException
    {
        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ResourceExistsException"/>.
        /// </summary>
        public ResourceExistsException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ResourceExistsException"/> with the message that
        /// describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public ResourceExistsException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ResourceExistsException"/> with the message that
        /// describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public ResourceExistsException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}