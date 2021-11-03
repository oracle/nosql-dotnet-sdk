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
    /// Cloud Service/Cloud Simulator only.  The exception that is thrown when
    /// an attempt has been made to evolve the schema of a table more times
    /// than allowed by the system defined limit.
    /// </summary>
    public class EvolutionLimitException : NoSQLException
    {

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="EvolutionLimitException"/>.
        /// </summary>
        public EvolutionLimitException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="EvolutionLimitException"/> with the message that
        /// describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public EvolutionLimitException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="EvolutionLimitException"/> with the message that
        /// describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public EvolutionLimitException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
