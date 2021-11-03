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
    /// The exception that is thrown when an operation attempted to access a
    /// resource that does not exist or is not in a visible state.
    /// </summary>
    public class ResourceNotFoundException : NoSQLException
    {

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ResourceNotFoundException"/>.
        /// </summary>
        public ResourceNotFoundException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ResourceNotFoundException"/> with the message that
        /// describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public ResourceNotFoundException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="ResourceNotFoundException"/> with the message that
        /// describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public ResourceNotFoundException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
