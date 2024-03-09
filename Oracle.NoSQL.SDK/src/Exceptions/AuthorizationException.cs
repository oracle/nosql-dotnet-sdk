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
    /// The exception that is thrown while trying to obtain the authorization
    /// information needed to perform an operation on the Oracle NoSQL
    /// Database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This exception is thrown by the authorization provider when the driver
    /// driver <see cref="IAuthorizationProvider.ApplyAuthorizationAsync"/>.
    /// It indicates failure to obtain the authorization information.  This
    /// instance may contain the underlying exception that is specific to the
    /// implementation of the authorization provider as
    /// <see cref="Exception.InnerException"/> property.
    /// </para>
    /// <para>
    /// <see cref="AuthorizationException"/> instance is retryable if and only
    /// if it contains the inner exception and the inner exception itself is
    /// retryable (see <see cref="IRetryHandler"/> for the description of
    /// retryable exceptions).
    /// </para>
    /// </remarks>
    /// <seealso cref="IAuthorizationProvider"/>
    /// <seealso cref="NoSQLConfig.AuthorizationProvider"/>
    public class AuthorizationException : NoSQLException
    {
        /// <summary>
        /// Initializes a new instance of
        /// <see cref="AuthorizationException"/>.
        /// </summary>
        public AuthorizationException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="AuthorizationException"/>
        /// with the message that describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public AuthorizationException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="AuthorizationException"/>
        /// with the message that describes the current exception and an inner
        /// exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public AuthorizationException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Gets the value indicating whether the operation that has thrown
        /// this exception may be retried.
        /// </summary>
        /// <value>
        /// <c>true</c> if this instance contains non-null
        /// <see cref="Exception.InnerException"/> property and the inner
        /// exception itself is retryable, otherwise <c>false</c>.
        /// </value>
        /// <seealso cref="NoSQLException.IsRetryable"/>
        /// <seealso cref="IRetryHandler"/>
        public override bool IsRetryable =>
            InnerException != null && Request != null &&
            Request.Client.IsRetryableException(InnerException);
    }
}
