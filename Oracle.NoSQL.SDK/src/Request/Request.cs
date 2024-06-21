/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Runtime.ExceptionServices;

    /// <summary>
    /// A base class for the requests classes that represent information about
    /// operations issued by <see cref="NoSQLClient"/> APIs.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The subclasses of this class describe operations issued by methods of
    /// <see cref="NoSQLClient"/>.  From an application perspective, these
    /// classes are only used to provide information about an operation such
    /// as the type of operation (as indicated by the particular subclass),
    /// arguments passed, number of retry attempts, etc.
    /// </para>
    /// <para>
    /// In general, the <see cref="Request"/> classes are for advanced usage
    /// and most applications don't need to use them.  They could be used
    /// in the following cases:
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// When a <see cref="NoSQLException"/> (or its subclass) is thrown,
    /// <see cref="NoSQLException.Request"/> provides additional information
    /// about an operation that caused the exception.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If you create your own retry handler by implementing
    /// <see cref="IRetryHandler"/> interface,
    /// <see cref="IRetryHandler.ShouldRetry"/> and
    /// <see cref="IRetryHandler.GetRetryDelay"/> take a <see cref="Request"/>
    /// object describing the operation as an argument that allows you to
    /// customize the retry behavior based on the operation.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If you create your own authorization provider by implementing
    /// <see cref="IAuthorizationProvider"/> or extending
    /// <see cref="AuthorizationStringProvider"/>,
    /// <see cref="IAuthorizationProvider.ApplyAuthorizationAsync"/> and
    /// <see cref="AuthorizationStringProvider.GetAuthorizationStringAsync"/>
    /// take a <see cref="Request"/> object describing the operation as an
    /// argument that allows you to customize authorization implementation
    /// based on the operation if needed.
    /// </description>
    /// </item>
    /// </list>
    /// </para>
    /// </remarks>
    public abstract class Request
    {
        internal const int MaxRequestTimeoutMillis = 30000;

        private TimeSpan timeout;

        private List<Exception> exceptions;

        private Exception ProtocolNotSupported(string name,
            short supportedVersion)
        {
            return new NotSupportedException(
                $"{name} is not supported because it requires minimum " +
                $"protocol version {supportedVersion}. The service is " +
                $"running protocol version {ProtocolVersion}");
        }

        internal Request(NoSQLClient client)
        {
            Client = client;
            ProtocolVersion = Client.ProtocolHandler.SerialVersion;
        }

        private protected void CheckProtocolVersion(string name,
            short supportedVersion)
        {
            if (ProtocolVersion < supportedVersion)
            {
                throw ProtocolNotSupported(name, supportedVersion);
            }
        }

        internal NoSQLClient Client { get; }

        // We save protocol version inside the request because the protocol
        // version in the client handle may change while this request is
        // executing if another parallel request has performed protocol
        // version fallback.
        internal short ProtocolVersion { get; private set; }

        internal virtual TimeSpan GetDefaultTimeout()
        {
            return Config.Timeout;
        }

        internal abstract IOptions BaseOptions { get; }

        internal string Compartment =>
            BaseOptions?.Compartment ?? Config.Compartment;

        internal string Namespace =>
            BaseOptions?.Namespace ?? Config.Namespace;

        internal int RequestTimeoutMillis { get; set; }

        internal TimeSpan Timeout
        {
            get
            {
                Debug.Assert(timeout != TimeSpan.Zero,
                    "Request.Init has not been called");
                return timeout;
            }
            set
            {
                timeout = value;
                RequestTimeoutMillis = Math.Min(
                    (int)timeout.TotalMilliseconds,
                    MaxRequestTimeoutMillis);
            }
        }

        internal virtual void UpdateProtocolVersion()
        {
            ProtocolVersion = Client.ProtocolHandler.SerialVersion;
        }

        internal virtual void Validate()
        {
            BaseOptions?.Validate();
        }

        // Prepare request to be executed (requests in iterators may be
        // executed multiple times).
        internal void Init()
        {
            Timeout = BaseOptions?.Timeout ?? GetDefaultTimeout();
            exceptions?.Clear();
        }

        internal abstract void Serialize(IRequestSerializer serializer,
            MemoryStream stream);

        internal abstract object Deserialize(IRequestSerializer serializer,
            MemoryStream stream);

        internal virtual void ApplyResult(object result)
        {
        }

        // Returns true if the operation can be retried immediately because we
        // got UnsupportedProtocolException.
        internal virtual bool HandleUnsupportedProtocol(Exception ex)
        {
            // Check if we got UnsupportedProtocolException and can retry
            // with older protocol, in which case we can immediately retry
            // (otherwise use retry handler as usual). If protocol fallback
            // fails, we cannot retry this exception and thus rethrow.
            if (ex is UnsupportedProtocolException upEx &&
                !Config.DisableProtocolFallback)
            {
                if (!Client.ProtocolHandler.DecrementSerialVersion(
                        ProtocolVersion))
                {
                    throw new UnsupportedProtocolException(
                        $"Protocol version {ProtocolVersion} is not " +
                        "supported and protocol fallback was unsuccessful",
                        ex);
                }

                return true;
            }

            return false;
        }

        // Whether client protocol version has been changed by a concurrent
        // request.
        internal virtual bool HasProtocolChanged() =>
            ProtocolVersion != Client.ProtocolHandler.SerialVersion;

        internal void AddException(Exception ex)
        {
            exceptions ??= new List<Exception>();

            RetryCount++;

            // Do not chain consecutive security info not ready exceptions
            if (ex is SecurityInfoNotReadyException &&
                PriorException is SecurityInfoNotReadyException)
            {
                return;
            }

            NoSQLException.SetRequest(ex, this);
            exceptions.Add(ex);
        }

        internal virtual bool ShouldRetry => true;

        internal virtual bool SupportsRateLimiting => false;

        // Used by rate limiting.
        internal virtual bool DoesReads => false;

        // Used by rate limiting.
        internal virtual bool DoesWrites => false;

        // Used by rate limiting via TopTableName.
        internal virtual string InternalTableName => null;

        // Currently we send latest topology seqNo for every request, but
        // we override this method for QueryRequest to send base topology
        // seqNo.
        internal virtual int QueryTopologySequenceNumber =>
            Client.QueryTopologySequenceNumber;

        // Used by rate limiting.
        internal string TopTableName
        {
            get
            {
                var tableName = InternalTableName;
                if (tableName == null)
                {
                    return null;
                }
                var idx = tableName.IndexOf('.');
                return idx < 0 ? tableName : tableName.Substring(0, idx);
            }
        }

        internal NoSQLConfig Config => Client.Config;

        // Cloud only. Requests that may require cross-region auth in the
        // proxy have to have their content signed (via "x-content-sha256"
        // header).
        internal virtual bool NeedsContentSigned => false;

        /// <summary>
        /// Gets the list of exceptions that occurred while retrying the
        /// operation.
        /// </summary>
        /// <remarks>
        /// This only includes retries performed by the driver using the
        /// configured retry handler (not the retries performed manually
        /// by the application).
        /// Some duplicate exceptions may be eliminated from this list, so
        /// the size of this list does not always indicate the number of
        /// retries.  For the number of retries, use <see cref="RetryCount"/>.
        /// </remarks>
        /// <value>
        /// The list of exceptions occurred during the operation retries.
        /// </value>
        /// <see cref="NoSQLConfig.RetryHandler"/>
        public IReadOnlyList<Exception> Exceptions => exceptions;

        /// <summary>
        /// Gets the number of times the operation has been retried.
        /// </summary>
        /// <remarks>
        /// This only includes retries performed by the driver using the
        /// configured retry handler (not the retries performed manually
        /// by the application).
        /// </remarks>
        /// <value>
        /// The number of retries performed, including the original try.
        /// </value>
        /// <seealso cref="NoSQLConfig.RetryHandler"/>
        public int RetryCount { get; private set; }

        /// <summary>
        /// Gets the last exception that occurred while retrying the operation
        /// or during its original invocation.
        /// </summary>
        /// <value>
        /// The last exception in the <see cref="Exceptions"/> list if the
        /// list is not empty, otherwise <c>null</c>.
        /// </value>
        /// <seealso cref="Exceptions"/>
        public Exception LastException =>
            Exceptions != null && Exceptions.Count > 0 ?
            Exceptions[^1] : null;

        /// <summary>
        /// Gets the prior to last exception that occurred while retrying the
        /// operation.
        /// </summary>
        /// <value>
        /// The prior to last exception in the <see cref="Exceptions"/> list
        /// if the list has at least two exceptions, otherwise <c>null</c>.
        /// </value>
        /// <seealso cref="Exceptions"/>
        public Exception PriorException =>
            Exceptions != null && Exceptions.Count > 1 ?
            Exceptions[^2] : null;
    }

}
