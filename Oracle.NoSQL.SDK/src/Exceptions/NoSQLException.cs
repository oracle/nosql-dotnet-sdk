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
    /// The base class for most exceptions thrown by the driver.
    /// </summary>
    /// <remarks>
    /// All exception classes defined by the driver are subclasses of
    /// <see cref="NoSQLException"/>.  In addition to these exception classes
    /// the driver may also throw standard exceptions such as
    /// <see cref="ArgumentException"/>,
    /// <see cref="InvalidOperationException"/>, etc.  See relevant method
    /// documentation for details.  In addition, <see cref="NoSQLException"/>
    /// itself may be thrown if the service responds with an unknown error.
    /// </remarks>
    public class NoSQLException : Exception
    {
        // In future, expand this to work on exceptions that are not instances
        // of NoSQLException.
        internal static void SetRequest(Exception ex, Request request)
        {
            if (ex is NoSQLException noSqlEx)
            {
                noSqlEx.Request = request;
            }
        }

        /// <summary>
        /// Initializes a new instance of <see cref="NoSQLException"/>.
        /// </summary>
        public NoSQLException()
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="NoSQLException"/> with
        /// the message that describes the current exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        public NoSQLException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="NoSQLException"/> with
        /// the message that describes the current exception and an inner
        /// exception.
        /// </summary>
        /// <param name="message">A message that describes the current
        /// exception.</param>
        /// <param name="inner">The inner exception.</param>
        public NoSQLException(string message, Exception inner)
            : base(message, inner)
        {
        }

        /// <summary>
        /// Gets the <see cref="Request"/> object that describes the
        /// operation that caused the exception.
        /// </summary>
        /// <remarks>
        /// The request object describes the operation initiated by a method
        /// of <see cref="NoSQLClient"/>.
        /// </remarks>
        /// <value>
        /// The <see cref="Request"/> object that describes the operation that
        /// caused the exception or <c>null</c> if the exception was not
        /// caused an operation on <see cref="NoSQLClient"/> instance (e.g.
        /// when using <see cref="FieldValue"/> subclasses).
        /// </value>
        /// <seealso cref="Request"/>
        public Request Request { get; internal set; }

        // Default implementation

        /// <summary>
        /// Gets the value indicating whether the operation that has thrown
        /// this exception may be retried.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Operation retries are handled automatically by the driver.  The
        /// application may customize the retry behavior by setting
        /// <see cref="NoSQLConfig.RetryHandler"/>.  It is possible that an
        /// application may also choose to retry the operation, in which case
        /// this property indicates when the operation should be retried.
        /// </para>
        /// <para>
        /// For instances of <see cref="NoSQLException"/> the driver will
        /// retry only those that have <see cref="IsRetryable"/> equal
        /// <c>true</c>.  In addition, standard exception types indicating
        /// a network error will be retried as well.  See
        /// <see cref="IRetryHandler"/> for details.
        /// </para>
        /// <para>
        /// Instances of <see cref="RetryableException"/> are always
        /// retryable.  Other subclasses of <see cref="NoSQLException"/> are
        /// usually not retryable but certain instances could be retried if
        /// indicated in the documentation for corresponding exception class.
        /// </para>
        /// </remarks>
        /// <value>
        /// <c>true</c> if the operation that has thrown this exception may be
        /// retried, otherwise <c>false</c>.  The default implementation of
        /// <see cref="NoSQLException"/> always returns <c>false</c>.
        /// </value>
        public virtual bool IsRetryable => false;

    }

}
