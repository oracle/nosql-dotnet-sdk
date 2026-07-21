/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Threading;
    using System.Threading.Tasks;
    using Query;

    public partial class NoSQLClient
    {
        internal void ObserveOrDeferLogicalQuery(QueryRequest request)
        {
            if (request.IsInternal)
            {
                return;
            }

            request.RestoreStatsFeatureValidationFromContinuation();
            if (request.LastWriteMetadata != null &&
                !request.StatsFeatureValidationSucceeded)
            {
                // Java checks server feature support before observing a query.
                // The request is observed after that preflight succeeds.
                request.DeferStatsLogicalQuery();
                return;
            }

            StatsControl?.ObserveQuery(request);
        }

        private async Task<QueryResult<TRow>>
            ExecutePreparedQueryRequestAsync<TRow>(
            QueryRequest<TRow> request,
            CancellationToken cancellationToken)
        {
            Debug.Assert(request.PreparedStatement != null);
            if (request.PreparedStatement.DriverQueryPlan == null)
            {
                // Simple query
                return (QueryResult<TRow>)await ExecuteValidatedRequestAsync(
                    request, cancellationToken);
            }

            // Advanced query
            var continuationKey = request.ContinuationKey;
            QueryPlanExecutor<TRow> executor;

            if (continuationKey?.Runtime != null)
            {
                executor = (QueryPlanExecutor<TRow>)continuationKey.Runtime;
            }
            else
            {
                executor = new QueryPlanExecutor<TRow>(this,
                    request.PreparedStatement);
            }

            return await executor.ExecuteAsync(request, cancellationToken);
        }

        private async Task<QueryResult<TRow>> ExecuteQueryRequestAsync<TRow>(
            QueryRequest<TRow> request,
            CancellationToken cancellationToken,
            bool observeLogicalQuery = true)
        {
            request.PreparedStatement ??=
                request.ContinuationKey?.PreparedStatement;

            if (observeLogicalQuery)
            {
                // Count this user-visible query API execution. Internal query
                // fetches are still counted only as HTTP Query requests by
                // ExecuteValidatedRequestAsync.
                ObserveOrDeferLogicalQuery(request);
            }

            // If the query is not already prepared the first request will
            // prepare it.
            if (request.PreparedStatement == null)
            {
                var result = (QueryResult<TRow>)
                    await ExecuteValidatedRequestAsync(
                        request, cancellationToken);

                // We always read the prepared statement if the request
                // does not have it.
                Debug.Assert(result.PreparedStatement != null);
                // Should be set by QueryRequest<TRow>.ApplyResult().
                Debug.Assert(request.PreparedStatement != null);

                if (request.PreparedStatement.DriverQueryPlan == null)
                {
                    // Simple query may already have results so we just return
                    // them.
                    return result;
                }

                // Advanced query will have no results in this case, only
                // the prepared statement.  To make it more intuitive for
                // the user, we execute first prepared query call.
                request.Options ??= new QueryOptions();

                request.Options.ContinuationKey = result.ContinuationKey;
            }

            return await ExecutePreparedQueryRequestAsync(request,
                cancellationToken);
        }

        private static QueryOptions GetQueryOptions(QueryOptions options) =>
            options != null ? options.Clone() : new QueryOptions();

        private async IAsyncEnumerable<QueryResult<TRow>>
            GetQueryAsyncEnumerable<TRow>(
            QueryRequest<TRow> request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            QueryResult<TRow> result;
            do
            {
                // Each continuation batch is equivalent to one QueryAsync
                // call. The first call is unprepared; later calls reuse the
                // prepared statement carried by the continuation key.
                result = await ExecuteQueryRequestAsync(request,
                    cancellationToken);

                if (result.ContinuationKey != null)
                {
                    request.Options.ContinuationKey = result.ContinuationKey;
                }

                yield return result;
            } while (result.ContinuationKey != null);
        }

        internal Task<QueryResult<TRow>> QueryAsync<TRow>(

            string statement,
            QueryOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var request = new QueryRequest<TRow>(this, statement, options);
            request.Validate();

            return ExecuteQueryRequestAsync(request, cancellationToken);
        }

        internal Task<QueryResult<TRow>> QueryAsync<TRow>(

            PreparedStatement preparedStatement,
            QueryOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var request = new QueryRequest<TRow>(this, preparedStatement,
                options);
            request.Validate();

            // Direct prepared-query execution bypasses ExecuteQueryRequestAsync,
            // so count the user-visible logical query here.
            ObserveOrDeferLogicalQuery(request);
            return ExecutePreparedQueryRequestAsync(request, cancellationToken);
        }

        // In the following two methods we make sure that the request
        // validation is not deferred.

        internal IAsyncEnumerable<QueryResult<TRow>>
            GetQueryAsyncEnumerable<TRow>(
            string statement,
            QueryOptions options = null,
            CancellationToken cancellationToken =
                default)
        {
            var request = new QueryRequest<TRow>(this, statement,
                GetQueryOptions(options));
            request.Validate();

            return GetQueryAsyncEnumerable(request, cancellationToken);
        }

        internal IAsyncEnumerable<QueryResult<TRow>>
            GetQueryAsyncEnumerable<TRow>(
            PreparedStatement preparedStatement,
            QueryOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var request = new QueryRequest<TRow>(this, preparedStatement,
                GetQueryOptions(options));
            request.Validate();

            return GetQueryAsyncEnumerable(request, cancellationToken);
        }

    }

}
