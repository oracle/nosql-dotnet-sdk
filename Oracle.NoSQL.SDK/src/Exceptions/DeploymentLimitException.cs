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
    /// Cloud Service/Cloud Simulator only.  The exception that is thrown when
    /// supplied table limits exceed the maximum allowed.
    /// </summary>
    /// <remarks>
    /// This exception is thrown when an attempt has been made to create or
    /// modify a table using limits that exceed the maximum allowed for a
    /// single table or that cause the tenant's aggregate resources to exceed
    /// the maximum allowed for a tenant.  These are system-defined limits.
    /// </remarks>
    public class DeploymentLimitException : NoSQLException
    {
        /// <summary>
        /// Initializes a new instance of
        /// <see cref="DeploymentLimitException"/>.
        /// </summary>
        public DeploymentLimitException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="DeploymentLimitException"/> with the message that
        /// describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public DeploymentLimitException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="DeploymentLimitException"/> with the message that
        /// describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public DeploymentLimitException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
