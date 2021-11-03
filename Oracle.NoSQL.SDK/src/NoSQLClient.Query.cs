/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {
    using System;
    using System.Collections.Generic;
    using System.Threading;
    using System.Threading.Tasks;

    public partial class NoSQLClient
    {
        /// <summary>
        /// Prepares a query for execution and reuse.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Query preparation allows queries to be compiled(prepared) and
        /// reused, saving time and resources.  Use of prepared queries vs
        /// direct execution of query strings is highly recommended.
        /// </para>
        /// <para>
        /// See <see cref="NoSQLClient.QueryAsync"/> for general information
        /// and restrictions.  Prepared queries are implemented as
        /// <see cref="PreparedStatement"/>  which supports bind variables
        /// in queries which can be used to more easily reuse a query by
        /// parameterization.
        /// </para>
        /// </remarks>
        /// <param name="statement">Query SQL statement.</param>
        /// <param name="options">(Optional) Options for the Prepare
        /// operation. If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="PrepareOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="PreparedStatement"/>.
        /// </returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="statement"/> is <c>null</c> or invalid or
        /// <paramref name="options"/> contains invalid values.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="QueryAsync(PreparedStatement, QueryOptions, CancellationToken)"/>
        /// <seealso cref="PreparedStatement"/>
        public async Task<PreparedStatement> PrepareAsync(
            string statement,
            PrepareOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (PreparedStatement)await ExecuteRequestAsync(
                new PrepareRequest(this, statement, options),
                cancellationToken);
        }

        /// <summary>
        /// Queries a table based on the query statement.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Queries that include a full shard key will execute much more
        /// efficiently than more distributed queries that must go to multiple
        /// shards.
        /// </para>
        /// <para>
        /// DDL-style queries such as "CREATE TABLE ..." or "DROP TABLE .."
        /// are not supported by this API.  Those operations must be performed
        /// using <see cref="ExecuteTableDDLAsync"/> and
        /// similar APIs.
        /// </para>
        /// <para>
        /// For performance reasons prepared queries are preferred for queries
        /// that may be reused, so you may prefer to use
        /// <see cref="QueryAsync(PreparedStatement, QueryOptions, CancellationToken)"/>
        /// over
        /// <see cref="QueryAsync(string, QueryOptions, CancellationToken)"/>
        /// if you are executing the query multiple times.  Prepared queries
        /// bypass compilation of the query. They also allow for parameterized
        /// queries using bind variables.
        /// </para>
        /// <para>
        /// The amount of data read by a single query request is limited by a
        /// system default and can be further limited by setting
        /// <see cref="QueryOptions.MaxReadKB"/>.  This limits the amount of
        /// data <em>read</em> and not the amount of data <em>returned</em>,
        /// which means that a query can return zero results but still have
        /// more data to read. This situation is detected by checking if
        /// <see cref="QueryResult{TRow}.ContinuationKey"/> is returned.  In
        /// addition, number of results returned by the query may be
        /// explicitly limited by setting <see cref="QueryOptions.Limit"/>.
        /// For these reasons queries should always in a loop, acquiring more
        /// results, until <see cref="QueryResult{TRow}.ContinuationKey"/> is
        /// <c>null</c>, indicating that the query is done.  Inside the loop
        /// the continuation key is applied to
        /// <see cref="QueryAsync"/> by setting
        /// <see cref="QueryOptions.ContinuationKey"/>.  The example shows
        /// this pattern.
        /// </para>
        /// <para>
        /// Alternatively to this method, you may call
        /// <see cref="GetQueryAsyncEnumerable"/> and iterate over resulting
        /// <see cref="IAsyncEnumerable{T}"/>.  This is
        /// equivalent to the loop described above but allows you to avoid
        /// dealing with the continuation key explicitly.
        /// </para>
        /// <para>
        /// The only exception to the above are queries that definitely use a
        /// complete primary key and thus can read only a single row.  These
        /// include <em>INSERT</em> statements as well as selects, updates or
        /// deletes of a single row based on the complete primary key.  In
        /// these cases, the looping is not required, but you may still wish
        /// to check <see cref="QueryResult{TRow}.ContinuationKey"/> to check
        /// for correctness.
        /// </para>
        /// </remarks>
        /// <example>
        /// Executing a query in a loop.
        /// <code>
        /// var statement = "SELECT * FROM myTable";
        /// var options = new QueryOptions();
        /// do
        /// {
        ///     var result = client.Query(statement, options);
        ///     foreach(var row in result.Rows)
        ///     {
        ///         // row is an instance of <see cref="RecordValue"/>
        ///         Console.WriteLine($"Id: {row["id"]}, Name: {row["name"]}");
        ///     }
        ///     options.ContinuationKey = result.ContinuationKey;
        /// }
        /// while(options.ContinuationKey != null);
        /// </code>
        /// </example>
        /// <param name="statement">Query SQL statement.</param>
        /// <param name="options">(Optional) Options for the Query
        /// operation. If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="QueryOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="QueryResult{TRow}"/> of
        /// <see cref="RecordValue"/>.
        /// </returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="statement"/> is <c>null</c> or invalid or
        /// <paramref name="options"/> contains invalid values.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="QueryResult{TRow}"/>
        /// <seealso cref="PrepareAsync"/>
        /// <seealso cref="GetQueryAsyncEnumerable"/>
        public Task<QueryResult<RecordValue>> QueryAsync(
            string statement,
            QueryOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return QueryAsync<RecordValue>(statement, options,
                cancellationToken);
        }

        /// <summary>
        /// Queries a table based on the prepared query statement.
        /// </summary>
        /// <remarks>
        /// This method is similar to
        /// <see cref="QueryAsync(string, QueryOptions, CancellationToken)"/>
        /// but it executes a query that has been prepared as
        /// <see cref="PreparedStatement"/> via <see cref="PrepareAsync"/>.
        /// </remarks>
        /// <param name="preparedStatement">Prepared query statement.</param>
        /// <param name="options">(Optional) Options for the Query
        /// operation. If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="QueryOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="QueryResult{TRow}"/> of
        /// <see cref="RecordValue"/>.
        /// </returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="preparedStatement"/> is <c>null</c> or invalid or
        /// <paramref name="options"/> contains invalid values.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="QueryAsync(string, QueryOptions, CancellationToken)"/>
        public Task<QueryResult<RecordValue>> QueryAsync(
            PreparedStatement preparedStatement,
            QueryOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return QueryAsync<RecordValue>(preparedStatement, options,
                cancellationToken);
        }

        /// <summary>
        /// Returns <see cref="IAsyncEnumerable{T}"/> to query a table based
        /// on the query statement.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This API facilitates iteration over the query results and is
        /// equivalent to executing <see cref="QueryAsync"/> in a loop with
        /// the continuation key.  The iteration over query results is
        /// necessary because of the limitations on the amount of data read
        /// during each query request as described in
        /// <see cref="QueryAsync"/>.
        /// </para>
        /// <para>
        /// Use this API with <c>await foreach</c> construct to iterate over
        /// the resulting <see cref="IAsyncEnumerable{T}"/>.  Each iteration
        /// of the loop produces <see cref="QueryResult{TRow}"/> which in
        /// turn may contain multiple result records, so a nested loop would
        /// be needed to iterate over each result record as shown in the
        /// example.
        /// </para>
        /// <para>
        /// Note that this method itself may only throw
        /// <see cref="ArgumentException"/>.  Other exceptions listed can only
        /// be thrown during the iteration process as per deferred execution
        /// semantics of enumerables.
        /// </para>
        /// </remarks>
        /// <example>
        /// Asynchronously iterating over
        /// <see cref="GetQueryAsyncEnumerable"/>.
        /// <code>
        /// await foreach(var result in client.GetQueryAsyncEnumerable(
        ///     "SELECT * FROM myTable"))
        /// {
        ///     foreach(var row in result.Rows)
        ///     {
        ///         // row is an instance of <see cref="RecordValue"/>
        ///         Console.WriteLine($"Id: {row["id"]}, Name: {row["name"]}");
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="statement">Query SQL statement.</param>
        /// <param name="options">(Optional) Options for the Query
        /// operation. If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="QueryOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Async enumerable to iterate over
        /// <see cref="QueryResult{TRow}"/> objects.</returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="statement"/> is <c>null</c> or invalid or
        /// <paramref name="options"/> contains invalid values.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="QueryAsync"/>
        /// <seealso href="https://docs.microsoft.com/en-us/archive/msdn-magazine/2019/november/csharp-iterating-with-async-enumerables-in-csharp-8">
        /// Iterating with Async Enumerables in C# 8
        /// </seealso>
        public IAsyncEnumerable<QueryResult<RecordValue>>
            GetQueryAsyncEnumerable(
            string statement,
            QueryOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return GetQueryAsyncEnumerable<RecordValue>(statement, options,
                cancellationToken);
        }

        /// <summary>
        /// Returns <see cref="IAsyncEnumerable{T}"/> to query a table based
        /// on the prepared query statement.
        /// </summary>
        /// <remarks>
        /// This method is similar to
        /// <see cref="GetQueryAsyncEnumerable(string, QueryOptions, CancellationToken)"/>
        /// but it executes a query that has been prepared as
        /// <see cref="PreparedStatement"/> via <see cref="PrepareAsync"/>.
        /// </remarks>
        /// <param name="preparedStatement">Prepared query statement.</param>
        /// <param name="options">(Optional) Options for the Query
        /// operation. If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="QueryOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Async enumerable to iterate over
        /// <see cref="QueryResult{TRow}"/> objects.</returns>
        /// <exception cref="ArgumentException"> If
        /// <paramref name="preparedStatement"/> is <c>null</c> or invalid or
        /// <paramref name="options"/> contains invalid values.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="QueryAsync"/>
        public IAsyncEnumerable<QueryResult<RecordValue>>
            GetQueryAsyncEnumerable(
            PreparedStatement preparedStatement,
            QueryOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return GetQueryAsyncEnumerable<RecordValue>(preparedStatement,
                options, cancellationToken);
        }

    }

}
