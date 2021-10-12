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
    /// Cloud Service/Cloud Simulator only.  The exception that is thrown when
    /// an has been made to create a row with a size that exceeds the system
    /// defined limit.
    /// </summary>
    public class RowSizeLimitException : NoSQLException
    {
        /// <summary>
        /// Initializes a new instance of <see cref="RowSizeLimitException"/>.
        /// </summary>
        public RowSizeLimitException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RowSizeLimitException"/>
        /// with the message that describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public RowSizeLimitException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="RowSizeLimitException"/>
        /// with the message that describes the current exception and an inner
        /// exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public RowSizeLimitException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
