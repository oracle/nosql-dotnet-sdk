/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using static ValidateUtils;

    /// <summary>
    /// Represents options for the Query operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These options are passed to
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> and
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>
    /// APIs.  For properties not specified or <c>null</c>,
    /// appropriate defaults will be used as indicated below.
    /// </para>
    /// <para>
    /// Note that for options <see cref="Timeout"/>, <see cref="Limit"/>,
    /// <see cref="MaxReadKB"/> and <see cref="MaxWriteKB"/> the corresponding
    /// limit is applied to each query request, not to the query as a whole.
    /// This means the limit is applied to each call to
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> or to each
    /// iteration of the <c>await foreach</c> loop when iterating over
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// Executing Query operation with provided <see cref="QueryOptions"/>.
    /// <code>
    /// var enumerable = client.GetQueryAsyncEnumerable(
    ///     "SELECT * FROM myTable",
    ///     new QueryOptions
    ///     {
    ///         Timeout = TimeSpan.FromSeconds(20),
    ///         MaxReadKB = 128,
    ///         MaxMemoryMB = 512
    ///     });
    ///
    /// await foreach(var result in enumerable)
    /// {
    ///     // .....
    /// }
    /// </code>
    /// </example>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>
    public class QueryOptions : IReadOptions
    {
        internal const int MaxReadKBLimit = 2048;
        internal const int MaxWriteKBLimit = 2048;

        /// <inheritdoc cref="GetOptions.Compartment"/>
        public string Compartment { get; set; }

        /// <inheritdoc cref="GetOptions.Namespace"/>
        public string Namespace
        {
            get => Compartment;
            set => Compartment = value;
        }

        /// <inheritdoc cref="GetOptions.Timeout"/>
        public TimeSpan? Timeout { get; set; }

        /// <inheritdoc cref="GetOptions.Consistency"/>
        public Consistency? Consistency { get; set; }

        /// <summary>
        /// On-premise only.
        /// Gets or sets <see cref="Durability"/> value to use for the
        /// update query operation.
        /// </summary>
        /// <remarks>
        /// This option only applies for update queries, i.e. queries issued
        /// via INSERT, UPDATE, UPSERT and DELETE statements.  For read-only
        /// SELECT queries this option is ignored.
        /// </remarks>
        /// <value>
        /// Durability used for the update query operation.  If not set,
        /// defaults to <see cref="NoSQLConfig.Durability"/>.
        /// </value>
        /// <seealso cref="Durability"/>
        public Durability? Durability { get; set; }

        /// <summary>
        /// Gets or sets the limit on the number of rows returned by this
        /// operation.
        /// </summary>
        /// <remarks>
        /// Settings this value allows an operation to return less than the
        /// default amount of data.  If set, this limit is applied to each
        /// call to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> or
        /// to each iteration of the <c>await foreach</c> loop over
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>.
        /// </remarks>
        /// <value>
        /// Limit on the number of rows returned by the query.
        /// </value>
        public int? Limit { get; set; }

        /// <summary>
        /// Gets or sets the limit on the total amount of data read by this
        /// operation, in KB.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value can only reduce the system defined limit.  This limit
        /// is independent of read units consumed by the operation.  It is
        /// recommended that for tables with relatively low provisioned read
        /// throughput that this limit be reduced to less than or equal to one
        /// half of the provisioned throughput in order to avoid or reduce
        /// throttling exceptions.
        /// </para>
        /// <para>
        /// If set, this limit is applied to each call to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> or to
        /// each iteration of the <c>await foreach</c> loop over
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>.
        /// </para>
        /// </remarks>
        /// <value>
        /// Limit on the total amount of data read, in KB.
        /// </value>
        public int? MaxReadKB { get; set; }

        /// <summary>
        /// Gets or sets the limit on the total data written by this
        /// operation, in KB.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This limit is relevant for <em>UPDATE</em> and <em>DELETE</em>
        /// queries.  This value can only reduce the system defined limit.
        /// This limit is independent of the write units consumed by the
        /// operation.
        /// </para>
        /// <para>
        /// If set, this limit is applied to each call to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> or to
        /// each iteration of the <c>await foreach</c> loop over
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>.
        /// </para>
        /// </remarks>
        /// <value>
        /// Limit on the total amount of data written, in KB.
        /// </value>
        public int? MaxWriteKB { get; set; }

        /// <summary>
        /// Gets or sets the maximum amount of memory that may be used by the
        /// driver executing this query, in MB.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value indicates the amount of memory that may be consumed
        /// locally by the query execution of operations such as duplicate
        /// elimination (which may be required if using an index on an array
        /// or a map) and sorting.  Such operations may require significant
        /// amount of memory as they need to cache full result set or a large
        /// subset of it locally.  If the memory consumption exceeds this
        /// value, error will result.
        /// </para>
        /// <para>
        /// If set, this limit is applied the query as a whole (including
        /// multiple calls to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> or full
        /// iteration over
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>).
        /// </para>
        /// </remarks>
        /// <value>
        /// The maximum amount of memory that may be used by the driver
        /// executing this query, in MB.  If not set, defaults to
        /// <see cref="NoSQLConfig.MaxMemoryMB"/> which itself has a default
        /// of 1GB.
        /// </value>
        public int? MaxMemoryMB { get; set; }

        /// <summary>
        /// Gets or sets the continuation key for the Query operation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Only for use with
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/>.
        /// This property is not needed if using
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>.
        /// </para>
        /// <para>
        /// The continuation key is returned as
        /// <see cref="QueryResult{TRow}.ContinuationKey"/> from the previous
        /// call to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/>
        /// and can be used to continue the query.
        /// </para>
        /// </remarks>
        /// <value>
        /// The continuation key on subsequent calls to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/>.
        /// </value>
        /// <seealso cref="QueryResult{TRow}.ContinuationKey"/>
        public QueryContinuationKey ContinuationKey { get; set; }

        internal int? TraceLevel { get; set; }

        internal virtual bool IsForTest => false;

        void IOptions.Validate()
        {
            CheckTimeout(Timeout);
            CheckEnumValue(Consistency);
            Durability?.Validate();
            CheckPositiveInt32(Limit, nameof(Limit));
            CheckPositiveInt32(MaxReadKB, nameof(MaxReadKB));
            CheckPositiveInt32(MaxWriteKB, nameof(MaxWriteKB));
            CheckPositiveInt32(MaxMemoryMB, nameof(MaxMemoryMB));

            // Currently the proxy returns BadProtocolException on these so we
            // check it here so that ArgumentException can be thrown.
            CheckNotAboveLimit(MaxReadKB, MaxReadKBLimit, nameof(MaxReadKB));
            CheckNotAboveLimit(MaxWriteKB, MaxWriteKBLimit,
                nameof(MaxWriteKB));
        }

        internal QueryOptions Clone() => (QueryOptions)MemberwiseClone();

        internal virtual long? MaxMemory => MaxMemoryMB * 0x100000;
    }

    internal class TestQueryOptions : QueryOptions
    {
        internal override bool IsForTest => true;

        public long? MaxMemoryBytes { get; set; }

        internal override long? MaxMemory => MaxMemoryBytes ?? base.MaxMemory;
    }

}
