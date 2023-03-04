/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using static ValidateUtils;

    // Used internally to provide query request info separate from
    // the row type

    /// <summary>
    /// Base class for <see cref="QueryRequest{TRow}"/>.  Only used
    /// internally.
    /// </summary>
    public abstract class QueryRequest : Request
    {
        // "5" == PrepareCallback.QueryOperation.SELECT
        internal const int OperationCodeSelect = 5;

        internal QueryRequest(NoSQLClient client, string statement,
            QueryOptions options) : base(client)
        {
            Statement = statement;
            Options = options;
        }

        internal QueryRequest(NoSQLClient client,
            PreparedStatement preparedStatement,
            QueryOptions options) : base(client)
        {
            PreparedStatement = preparedStatement;
            Options = options;
        }

        internal override IOptions BaseOptions => Options;

        internal string Statement { get; }

        internal PreparedStatement PreparedStatement { get; set; }

        internal QueryOptions Options { get; set; }

        internal Consistency Consistency =>
            Options?.Consistency ?? Config.Consistency;

        internal Durability? Durability =>
            Options?.Durability ?? Config.Durability;

        internal long MaxMemory =>
            Options?.MaxMemory ?? Config.MaxMemoryMB * 0x100000;

        internal int ShardId { get; set; } = -1;

        internal bool IsInternal { get; set; }

        internal override bool SupportsRateLimiting => true;

        internal override bool DoesReads => true;

        internal override bool DoesWrites =>
            PreparedStatement != null &&
            PreparedStatement.OperationCode == OperationCodeSelect;

        internal override string InternalTableName =>
            PreparedStatement?.TableName;

        internal QueryContinuationKey ContinuationKey =>
            Options?.ContinuationKey;
    }

    /// <summary>
    /// Represents information about Query operation performed by
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> and
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>
    /// APIs.
    /// </summary>
    /// <typeparam name="TRow">The type of value representing the rows
    /// returned by the Query operation.  Must be a reference type.  Currently
    /// the only supported type is <see cref="RecordValue"/>.</typeparam>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>
    /// <seealso cref="Request"/>
    public class QueryRequest<TRow> : QueryRequest
    {
        internal QueryRequest(NoSQLClient client, string statement,
            QueryOptions options) : base(client, statement, options)
        {
        }

        internal QueryRequest(NoSQLClient client,
            PreparedStatement preparedStatement,
            QueryOptions options) : base(client, preparedStatement, options)
        {
            IsPrepared = true;
        }

        /// <summary>
        /// Gets the query SQL statement.
        /// </summary>
        /// <value>
        /// The query SQL statement if the Query operation was passed a SQL
        /// statement or <c>null</c> if the Query operation was passed a
        /// <see cref="PreparedStatement"/>.
        /// </value>
        public new string Statement => base.Statement;

        /// <summary>
        /// Gets the prepared query statement.
        /// </summary>
        /// <value>
        /// The prepared query statement if the Query operation was passed a
        /// <see cref="PreparedStatement"/> or <c>null</c> if the Query
        /// operation was passed a SQL statement.
        /// </value>
        public new PreparedStatement PreparedStatement
        {
            get => base.PreparedStatement;
            internal set => base.PreparedStatement = value;
        }

        /// <summary>
        /// Gets a value indicating whether this request represents a prepared
        /// query.
        /// </summary>
        /// <value>
        /// <c>true</c> if the Query operation was passed a
        /// <see cref="PreparedStatement"/>, <c>false</c> if the Query
        /// operation was passed a SQL statement.
        /// </value>
        public bool IsPrepared { get; }

        /// <summary>
        /// Gets the options for the Query operation.
        /// </summary>
        /// <value>
        /// The options or <c>null</c> if options were not provided.
        /// </value>
        public new QueryOptions Options
        {
            get => base.Options;
            internal set => base.Options = value;
        }

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializeQuery(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializeQuery(stream, this);
        }

        internal override void Validate()
        {
            base.Validate();
            if (IsPrepared)
            {
                CheckNotNull(PreparedStatement, nameof(PreparedStatement));
                PreparedStatement.Validate();
            }
            else
            {
                CheckNotNullOrEmpty(Statement, nameof(Statement));
            }
        }

        internal override void ApplyResult(object result)
        {
            base.ApplyResult(result);

            var queryResult = (QueryResult<TRow>)result;

            // Received prepared statement
            if (queryResult.PreparedStatement != null)
            {
                Debug.Assert(PreparedStatement == null);
                PreparedStatement = queryResult.PreparedStatement;
                // Advanced query will be executed on the next Query() call,
                // so we need continuation key.
                if (PreparedStatement.DriverQueryPlan != null &&
                    queryResult.ContinuationKey == null)
                {
                    queryResult.ContinuationKey = new QueryContinuationKey();
                }
            }

            if (queryResult.TopologyInfo != null)
            {
                PreparedStatement.SetTopologyInfo(queryResult.TopologyInfo);
            }

            // Once we have prepared statement, it will be part of
            // continuation key to be used for subsequent Query() calls
            // (if Prepare() was not initially called)
            if (queryResult.ContinuationKey != null)
            {
                queryResult.ContinuationKey.PreparedStatement =
                    PreparedStatement;
            }
        }
    }

}
