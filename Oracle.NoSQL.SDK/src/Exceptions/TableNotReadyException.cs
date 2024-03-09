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
    /// Cloud service only.
    /// The exception that is thrown when an operation is attempted on a
    /// replicated table that is not yet fully initialized.
    /// </summary>
    public class TableNotReadyException : RetryableException
    {
        /// <summary>
        /// Initializes a new instance of
        /// <see cref="TableNotReadyException"/>.
        /// </summary>
        public TableNotReadyException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="TableNotReadyException"/> with the message that
        /// describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public TableNotReadyException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="TableNotReadyException"/>
        /// with the message that describes the current exception and an inner
        /// exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public TableNotReadyException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
