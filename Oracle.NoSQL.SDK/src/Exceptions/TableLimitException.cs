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
    /// Cloud Service/Cloud Simulator only.  The exception that is thrown when
    /// an attempt has been made to create a number of tables that exceeds the
    /// system defined limit.
    /// </summary>
    public class TableLimitException : NoSQLException
    {
        /// <summary>
        /// Initializes a new instance of <see cref="TableLimitException"/>.
        /// </summary>
        public TableLimitException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TableLimitException"/>
        /// with the message that describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public TableLimitException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TableLimitException"/>
        /// with the message that describes the current exception and an inner
        /// exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public TableLimitException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
