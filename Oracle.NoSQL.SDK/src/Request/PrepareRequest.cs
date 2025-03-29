/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.IO;
    using System.Runtime.ExceptionServices;
    using static ValidateUtils;

    /// <summary>
    /// Base class for prepare and query requests. Only used internally.
    /// </summary>
    public abstract class QueryRequestBase : Request
    {
        internal const short QueryV3 = 3;
        internal const short QueryV4 = 4;
        internal const short DefaultQueryVersion = QueryV4;

        internal QueryRequestBase(NoSQLClient client) : base(client)
        {
            QueryVersion = Client.ProtocolHandler.QueryVersion;
        }

        internal short QueryVersion { get; set; }

        internal override void UpdateProtocolVersion()
        {
            base.UpdateProtocolVersion();
            QueryVersion = Client.ProtocolHandler.QueryVersion;
        }

        // Returns true if the operation can be retried immediately because we
        // got UnsupportedProtocolException or
        // UnsupportedQueryVersionException.
        internal override bool HandleUnsupportedProtocol(Exception ex)
        {
            if (base.HandleUnsupportedProtocol(ex))
            {
                return true;
            }

            // Check if we got UnsupportedQueryVersionException and can retry
            // with older query version, in which case we can immediately
            // retry (otherwise use retry handler as usual). If query version
            // fallback fails, we cannot retry this exception and thus rethrow.
            if (ex is UnsupportedQueryVersionException uqvEx &&
                !Config.DisableProtocolFallback)
            {
                if (!Client.ProtocolHandler
                        .DecrementQueryVersion(QueryVersion))
                {
                    throw new UnsupportedQueryVersionException(
                        $"Query protocol version {QueryVersion} is not " +
                        "supported and query protocol fallback was " +
                        "unsuccessful", ex);
                }

                return true;
            }

            return false;
        }

        internal override bool HasProtocolChanged() =>
            base.HasProtocolChanged() ||
            QueryVersion != Client.ProtocolHandler.QueryVersion;
    }

    /// <summary>
    /// Represents information about the Prepare operation performed by
    /// <see cref="NoSQLClient.PrepareAsync"/> API.
    /// </summary>
    /// <seealso cref="NoSQLClient.PrepareAsync"/>
    public class PrepareRequest : QueryRequestBase
    {
        // Used by rate limiting.
        private string tableName;

        internal PrepareRequest(NoSQLClient client, string statement,
            PrepareOptions options) : base(client)
        {
            Statement = statement;
            Options = options;
        }

        internal override IOptions BaseOptions => Options;

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializePrepare(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializePrepare(stream, this);
        }

        internal override void Validate()
        {
            base.Validate();
            CheckNotNullOrEmpty(Statement, nameof(Statement));
        }

        internal override bool SupportsRateLimiting => true;

        internal override bool DoesReads => true;

        internal override string InternalTableName => tableName;

        internal override void ApplyResult(object result)
        {
            base.ApplyResult(result);
            tableName = ((PreparedStatement)result).TableName;
        }

        /// <summary>
        /// Gets the query SQL statement.
        /// </summary>
        /// <value>
        /// The query SQL statement.
        /// </value>
        public string Statement { get; internal set; }

        /// <summary>
        /// Gets the options for the Prepare operation.
        /// </summary>
        /// <value>
        /// The options or <c>null</c> if options were not provided.
        /// </value>
        public PrepareOptions Options { get; internal set; }

        internal bool GetQueryPlan => Options != null && Options.GetQueryPlan;

        internal bool GetResultSchema => Options != null && Options.GetResultSchema;
    }
}
