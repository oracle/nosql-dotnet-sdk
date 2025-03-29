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
    /// The exception that is thrown when a request presents invalid
    /// authorization information to the service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Note that this is different from <see cref="AuthorizationException"/>
    /// which indicates failure to obtain the authorization information.
    /// </para>
    /// <para>
    /// In most cases, <see cref="InvalidAuthorizationException"/> is due to
    /// presenting expired authorization information and thus it can be
    /// retried, at which time the driver will renew the authorization
    /// information.  The <see cref="NoSQLRetryHandler"/> will always retry
    /// <see cref="InvalidAuthorizationException"/> once but will not retry
    /// again if <see cref="InvalidAuthorizationException"/> occurs twice in a
    /// row, because this means that renewing of authorization information did
    /// not solve the problem and the error is due to some other reason.
    /// </para>
    /// </remarks>
    /// <seealso cref="NoSQLRetryHandler"/>
    public class InvalidAuthorizationException : RetryableException
    {
        /// <summary>
        /// Initializes a new instance of
        /// <see cref="InvalidAuthorizationException"/>.
        /// </summary>
        public InvalidAuthorizationException()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="InvalidAuthorizationException"/> with the message that
        /// describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public InvalidAuthorizationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="InvalidAuthorizationException"/> with the message that
        /// describes the current exception and an inner exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public InvalidAuthorizationException(string message, Exception inner)
            : base(message, inner)
        {
        }
    }
}
