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
    /// The exception that is thrown when  when a table operation fails
    /// because the table is in use or busy.
    /// </summary>
    /// <remarks>
    /// Only one modification operation at a time is allowed on a table. This
    /// is a retryable exception.
    /// </remarks>
    public class TableBusyException : RetryableException
    {
        /// <summary>
        /// Initializes a new instance of <see cref="TableBusyException"/>.
        /// </summary>
        public TableBusyException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TableBusyException"/>
        /// with the message that describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public TableBusyException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TableBusyException"/>
        /// with the message that describes the current exception and an inner
        /// exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public TableBusyException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
