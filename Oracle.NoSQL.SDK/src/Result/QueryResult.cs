/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using Query;

    internal class AllPartitionsSortPhase1
    {
        internal bool ToContinue { get; set; }

        internal int[] PartitionIds { get; set; }

        internal int[] ResultCounts { get; set; }

        internal byte[][] ContinuationKeys { get; set; }
    }

    internal struct QueryTraceRecord
    {
        internal string BatchName { get; set; }
        internal string BatchTrace { get; set; }
    }

    /// <summary>
    /// Represents the result of the Query operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class represents the result of
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> API and
    /// partial results when iterating with
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>.
    /// It contains a list of row instances representing the query results.
    /// </para>
    /// <para>
    /// The shape of the values is based on the schema implied by the query.
    /// For example, a query such as <em>SELECT * FROM ...</em> that returns
    /// an intact row will return values that conform to the schema of the
    /// table.  Projections return instances that conform to the schema
    /// implied by the statement. <em>UPDATE</em> queries either return values
    /// based on a <em>RETURNING</em> clause or, by default, the number of
    /// rows affected by the <em>UPDATE</em> statement.
    /// </para>
    /// <para>
    /// When using <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/>
    /// API, if the value of
    /// <see cref="QueryResult{TRow}.ContinuationKey"/> is not <c>null</c>,
    /// there are additional results available.  That value can be supplied as
    /// <see cref="QueryOptions.ContinuationKey"/> to the subsequent call to
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> to
    /// continue the query.  In general,
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> should be
    /// called in a loop (until the continuation key becomes <c>null</c>) to
    /// get all the results, as described in
    /// <see cref="NoSQLClient.QueryAsync(string, QueryOptions, CancellationToken)"/>.
    /// You don't need to set the continuation key if using
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>
    /// since the continuation key is managed internally in this case.
    /// </para>
    /// <para>
    /// It is possible that
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/>
    /// returns no rows in the result (<see cref="Rows"/> list is
    /// empty) but still have a non-null continuation key. The same
    /// situation may happen when using
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>
    /// and no rows are returned on a given iteration of <c>await foreach</c>
    /// loop. This may happen if the query reads the maximum amount of data
    /// allowed in a single request without matching a query predicate.  In
    /// either case you should continue the query by calling
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> or
    /// iterating via <c>await foreach</c> to get results, if any exist.
    /// </para>
    /// <para>
    /// It is also a normal situation if that the last call to
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> (when the
    /// continuation key becomes <c>null</c>) or the last iteration of
    /// <c>await foreach</c> loop returns no rows in the result.  This could
    /// happen if the last iteration returned all the remaining rows but was
    /// stopped due to the limits on the data read or written during a
    /// single request, so one more iteration was needed to find that no more
    /// results are available.
    /// </para>
    /// </remarks>
    /// <typeparam name="TRow">The type of value representing the returned
    /// rows.  Must be a reference type.  Currently the only supported type is
    /// <see cref="RecordValue"/>.</typeparam>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>
    public class QueryResult<TRow> : IDataResult
    {
        internal QueryResult()
        {
        }

        ConsumedCapacity IDataResult.ConsumedCapacity
        {
            get => ConsumedCapacity;
            set => ConsumedCapacity = value;
        }

        /// <inheritdoc cref="GetResult{TRow}.ConsumedCapacity"/>
        public ConsumedCapacity ConsumedCapacity { get; internal set; }

        /// <summary>
        /// Gets the list of query results.
        /// </summary>
        /// <value>
        /// The list of query results as row instances.  Currently the only
        /// supported type for a row instance is <see cref="RecordValue"/>.
        /// </value>
        public IReadOnlyList<TRow> Rows { get; internal set; }

        /// <summary>
        /// Gets the continuation key.
        /// </summary>
        /// <remarks>
        /// You only need to use this property if using
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/>.  It is
        /// not needed if
        /// using
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>.
        /// </remarks>
        /// <value>
        /// The continuation key indicating where the next call to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> can
        /// resume or <c>null</c> if the query has no more results.
        /// </value>
        /// <seealso cref="QueryContinuationKey"/>
        public QueryContinuationKey ContinuationKey { get; internal set; }

        internal AllPartitionsSortPhase1 SortPhase1 { get; set; }

        internal PreparedStatement PreparedStatement { get; set; }

        internal bool ReachedLimit { get; set; }

        internal IReadOnlyList<VirtualScan> VirtualScans { get; set; }

        internal IReadOnlyList<QueryTraceRecord> QueryTraces { get; set; }
    }

    /// <summary>
    /// Represents the continuation key for the Query operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="QueryContinuationKey"/> is an opaque type returned from
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> as
    /// <see cref="QueryResult{TRow}.ContinuationKey"/> when the Query
    /// operation has exceeded the maximum amount of data to be read or
    /// written and it needs to be continued with subsequent calls to
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/>.  For each
    /// subsequent call, set <see cref="QueryOptions.ContinuationKey"/> to the
    /// value of <see cref="QueryResult{TRow}.ContinuationKey"/> returned by
    /// the previous call.
    /// </para>
    /// <para>
    /// You do not need to use this type if iterating over
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetQueryAsyncEnumerable*"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/>
    /// <seealso cref="QueryResult{TRow}"/>
    /// <seealso cref="QueryOptions"/>
    public class QueryContinuationKey
    {
        internal byte[] Bytes { get; }

        internal PreparedStatement PreparedStatement { get; set; }

        internal QueryRuntime Runtime { get; set; }

        internal QueryContinuationKey()
        {
        }

        internal QueryContinuationKey(byte[] bytes)
        {
            Debug.Assert(bytes != null);
            Bytes = bytes;
        }
    }

}
