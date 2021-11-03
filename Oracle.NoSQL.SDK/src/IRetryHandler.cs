/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;

    /// <summary>
    /// An interface to handle automatic operation retries.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The driver supports automatic retrying of operations that fail with
    /// retryable exceptions.  The following exceptions can be retried:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// All subclasses of <see cref="RetryableException"/>.  Their
    /// <see cref="NoSQLException.IsRetryable"/> property is always
    /// <c>true</c>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Instances of other subclasses of <see cref="NoSQLException"/> that
    /// have <see cref="NoSQLException.IsRetryable"/> equal <c>true</c>.
    /// Currently these include instances of
    /// <see cref="ServiceResponseException"/> with retryable HTTP status
    /// codes.  See <see cref="ServiceResponseException"/>.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// All network-related errors.  Currently those are instances of
    /// <see cref="System.Net.Http.HttpRequestException"/>.
    /// </description>
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// The driver uses the retry handler to control the number of retries as
    /// well as the frequency of retries (via the duration of a delay before
    /// the retry happens).  For most applications, it is sufficient to use
    /// built-in retry handler implemented by <see cref="NoSQLRetryHandler"/>.
    /// It uses exponential back-off algorithm with special cases for certain
    /// operations and has customizable parameters.  See
    /// <see cref="NoSQLRetryHandler"/>.
    /// </para>
    /// <para>
    /// The retry handler instance is specified as
    /// <see cref="NoSQLConfig.RetryHandler"/> in the initial configuration.
    /// You have the following options:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// Leave <see cref="NoSQLConfig.RetryHandler"/> unset, in which case the
    /// driver will use an instance of <see cref="NoSQLRetryHandler"/> with
    /// default property values.  This should be sufficient for most
    /// applications.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Set <see cref="NoSQLConfig.RetryHandler"/> to a new instance of
    /// <see cref="NoSQLRetryHandler"/> created with customized property
    /// values.  This will allow you to customize such parameters as maximum
    /// number of retries, base delay between retries and retry delays for
    /// special situations.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Create your own custom class implementing <see cref="IRetryHandler"/>
    /// and set its instance as <see cref="NoSQLConfig.RetryHandler"/>.  This
    /// allows for most customization of operation retries.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If you want to disable operation retries altogether, set
    /// <see cref="NoSQLConfig.RetryHandler"/> to
    /// <see cref="NoSQLConfig.NoRetries"/>.
    /// </description>
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// It is not recommended that applications rely on the retry handler for
    /// regulating provisioned throughput as this will result in low
    /// efficiency.  It is best to add rate limiting to the application based
    /// on a table's capacity and access patterns to avoid throttling
    /// exceptions.
    /// </para>
    /// <para>
    /// Instances of this interface will be shared between threads, so they
    /// must be immutable or otherwise thread-safe.
    /// </para>
    /// </remarks>
    /// <seealso cref="NoSQLConfig.RetryHandler"/>
    /// <seealso cref="NoSQLRetryHandler"/>
    public interface IRetryHandler
    {
        /// <summary>
        /// Determines whether the operation should be retried.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is called every time after an operation fails with a
        /// retryable exception.  The operations may be retried multiple
        /// times.  This method is called before each retry.  The retries will
        /// continue until one of the following occurs:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// The operation is successful.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// This method returns <c>false</c>.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// The operation timeout elapses.
        /// </description>
        /// </item>
        /// </list>
        /// </para>
        /// <para>
        /// The <see cref="Request"/> object includes information on the
        /// number of retries done so far (see
        /// <see cref="Request.RetryCount"/>) as well as all the exceptions
        /// occurred during previous retries (see
        /// <see cref="Request.Exceptions"/> and
        /// <see cref="Request.LastException"/>).  You may decide whether the
        /// operation should be retried based on this information as well as any
        /// operation-specific information provided by the
        /// <see cref="Request"/> instance.
        /// </para>
        /// </remarks>
        /// <param name="request">The <see cref="Request"/> object describing
        /// the running operation.</param>
        /// <returns><c>true</c> to retry the operation, otherwise
        /// <c>false</c>.</returns>
        /// <seealso cref="Request"/>
        bool ShouldRetry(Request request);

        /// <summary>
        /// Determines how long to wait between successive retries.
        /// </summary>
        /// <remarks>
        /// This method is called after <see cref="ShouldRetry"/> if
        /// <see cref="ShouldRetry"/> returned true.  It determines how long
        /// to delay before the operation is retried.  The delay is
        /// non-blocking (asynchronous).  The delay returned may vary
        /// depending on the current state of the operation, e.g.
        /// <see cref="Request.RetryCount"/>,
        /// <see cref="Request.LastException"/>, etc.  For example, the
        /// delay could increase with the number of retries performed so far.
        /// </remarks>
        /// <param name="request">The <see cref="Request"/> object describing
        /// the running operation.</param>
        /// <returns>A time interval to wait before the next retry.</returns>
        /// <seealso cref="ShouldRetry"/>
        TimeSpan GetRetryDelay(Request request);
    }

}
