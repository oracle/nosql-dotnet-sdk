/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Runtime.CompilerServices;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using static ValidateUtils;

    public partial class NoSQLClient
    {
        private Http.Client client;
        private bool disposed;

        internal NoSQLConfig Config { get; private set; }

        internal ProtocolHandler ProtocolHandler { get; private set; }

        internal RateLimitingHandler RateLimitingHandler { get; private set; }

        internal static TimeoutException GetTimeoutException(TimeSpan timeout,
            int retryCount, Exception inner) => new TimeoutException(
            $"Operation timed out after {timeout.TotalMilliseconds} ms " +
            $"and {retryCount} retries", inner);

        internal bool IsRetryableException(Exception ex)
        {
            return ex is NoSQLException noSqlEx && noSqlEx.IsRetryable ||
                client.IsRetryableNetworkException(ex);
        }

        internal bool IsRetryableNetworkException(Exception ex)
        {
            return client.IsRetryableNetworkException(ex);
        }

        internal Task<object> ExecuteRequestAsync(Request request,
            CancellationToken cancellationToken)
        {
            request.Validate();
            return ExecuteValidatedRequestAsync(request, cancellationToken);
        }

        internal async Task<object> ExecuteValidatedRequestAsync(
            Request request, CancellationToken cancellationToken)
        {
            request.Init();

            var rlReq =
                RateLimitingHandler != null && request.SupportsRateLimiting
                    ? new RateLimitingRequest(RateLimitingHandler, request)
                    : null;

            var timeout = request.Timeout; // original request timeout
            var startTime = DateTime.UtcNow;

            while (true)
            {
                var serialVersion = ProtocolHandler.SerialVersion;
                try
                {
                    if (rlReq != null)
                    {
                        await rlReq.Start(cancellationToken);
                    }

                    var result = await client.ExecuteRequestAsync(request,
                        cancellationToken);
                    request.ApplyResult(result);

                    if (rlReq != null)
                    {
                        await rlReq.Finish(result, cancellationToken);
                    }
                    return result;
                }
                catch (Exception ex)
                {
                    request.AddException(ex);
                    rlReq?.HandleException(ex);

                    if (ex is SecurityInfoNotReadyException &&
                        timeout < Config.SecurityInfoNotReadyTimeout)
                    {
                        timeout = Config.SecurityInfoNotReadyTimeout;
                    }

                    var endTime = startTime + timeout;
                    var now = DateTime.UtcNow;

                    if (ex is UnsupportedProtocolException &&
                        !Config.DisableProtocolFallback && now < endTime &&
                        // We should always retry if some other concurrent
                        // request has already decremented serial version.
                        ProtocolHandler.DecrementSerialVersion(serialVersion))
                    {
                        if (request.MinProtocolVersion >
                            ProtocolHandler.SerialVersion)
                        {
                            throw new NotSupportedException(
                                "This operation requires minimum protocol " +
                                $"version {request.MinProtocolVersion} and " +
                                "cannot be performed by the service " +
                                "running protocol version " +
                                ProtocolHandler.SerialVersion);
                        }
                        continue;
                    }

                    if (ex is TimeoutException)
                    {
                        throw GetTimeoutException(now - startTime,
                            request.RetryCount, ex);
                    }

                    if (!IsRetryableException(ex) ||
                        !Config.RetryHandler.ShouldRetry(request))
                    {
                        throw;
                    }

                    var delay = Config.RetryHandler.GetRetryDelay(request);
                    endTime -= delay;

                    if (now >= endTime)
                    {
                        throw GetTimeoutException(now - startTime,
                            request.RetryCount, ex);
                    }

                    // This will adjust http request timeout for the time
                    // already elapsed.
                    request.Timeout = endTime - now;
                    await Task.Delay(delay, cancellationToken);
                }
            }
        }

        // These should be slightly more efficient than using Linq
        // Select with lambda function.

        private static WriteOperationCollection CreatePutManyOps<TRow>(
            IReadOnlyCollection<TRow> rows,
            PutManyOptions options)
        {
            CheckNotNull(rows, nameof(rows));
            ((IOptions)options)?.Validate();

            var woc = new WriteOperationCollection(rows.Count);

            foreach (var row in rows)
            {
                CheckNotNull(row, nameof(row));
                woc.AddValidatedPutOp(new PutOperation(null, row, options,
                    options?.AbortIfUnsuccessful ?? false));
            }

            return woc;
        }

        private static WriteOperationCollection CreateDeleteManyOps(
            IReadOnlyCollection<object> primaryKeys,
            DeleteManyOptions options)
        {
            CheckNotNull(primaryKeys, nameof(primaryKeys));
            ((IOptions)options)?.Validate();

            var woc = new WriteOperationCollection(primaryKeys.Count);

            foreach (var primaryKey in primaryKeys)
            {
                CheckNotNull(primaryKey, nameof(primaryKey));
                woc.AddValidatedDeleteOp(new DeleteOperation(null, primaryKey,
                    options, options?.AbortIfUnsuccessful ?? false));
            }

            return woc;
        }

        private async Task<WriteManyResult<TRow>> WriteManyInternalAsync<TRow>(
            string tableName, WriteOperationCollection operations,
            IWriteManyOptions options, CancellationToken cancellationToken)
        {
            return (WriteManyResult<TRow>) await ExecuteRequestAsync(
                new WriteManyRequest<TRow>(this, tableName, operations,
                    options), cancellationToken);
        }

        private async Task<TableResult> DoTableOpWithCompletionAsync(
            TableOperationRequest request, CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var result = (TableResult) await ExecuteRequestAsync(request,
                cancellationToken);
            Debug.Assert(result != null);
            var timeout = request.CompletionOptions?.Timeout -
                          (DateTime.UtcNow - startTime);

            if (timeout <= TimeSpan.Zero)
            {
                // Make sure timeout is positive.
                timeout = TimeSpan.FromMilliseconds(1);
            }

            return await result.WaitForCompletionAsync(timeout,
                request.CompletionOptions?.PollDelay, cancellationToken);
        }

        internal async Task WaitForTableStateInternalAsync(
            TableResult tableResult, Func<TableResult,bool> predicate,
            string description, TimeSpan? timeout, TimeSpan? pollDelay,
            CancellationToken cancellationToken)
        {
            if (predicate(tableResult))
            {
                return;
            }

            // If called from NoSQLClient.WaitForTableStateAsync() or
            // NoSQLClient.WaitForLocalReplicaInitAsync(), in which case the
            // table may not even exist at the moment, in which case we keep
            // polling until the table is created and reaches desired state.
            var isInitialStateUnknown =
                tableResult.TableState == TableResult.UnknownTableState;

            var tablePollTimeout = timeout ?? Config.TablePollTimeout;
            var tablePollDelay = pollDelay ?? Config.TablePollDelay;

            var options = new GetTableOptions
            {
                Compartment = tableResult.CompartmentId,
                Timeout = tablePollTimeout < Config.Timeout
                    ? tablePollTimeout
                    : Config.Timeout
            };

            var request = new GetTableRequest(this, tableResult.TableName,
                tableResult.OperationId, options);
            var startTime = DateTime.UtcNow;

            while(true)
            {
                TableResult result = null;

                try
                {
                    result = (TableResult)await ExecuteValidatedRequestAsync(
                        request, cancellationToken);
                    Debug.Assert(result != null);
                }
                catch (TableNotFoundException)
                {
                    tableResult.TableState = TableState.Dropped;
                }

                if (result != null)
                {
                    // Modify passed tableResult in place. Doing it on every
                    // poll instead of just when we return allows the user to
                    // see updated result even if exception is thrown.
                    tableResult.CopyFrom(result);
                }

                if (predicate(tableResult))
                {
                    return;
                }

                // If called from TableResult.WaitForCompletionAsync() and we
                // are not waiting for table to be dropped, but get the table
                // state dropped, we throw exception to notify the user
                // something unexpected has happened.
                if (!isInitialStateUnknown &&
                    tableResult.TableState == TableState.Dropped)
                {
                    throw new TableNotFoundException(
                        $"Table {tableResult.TableName} not found while " +
                        $"waiting for {description}");
                }

                if (tablePollTimeout.HasValue)
                {
                    var remaining = startTime + tablePollTimeout -
                        (DateTime.UtcNow + tablePollDelay);

                    if (remaining <= TimeSpan.Zero)
                    {
                        throw new TimeoutException(
                            "Reached timeout while waiting for " +
                            description);
                    }

                    if (options.Timeout > remaining)
                    {
                        options.Timeout = remaining;
                    }
                }

                await Task.Delay(tablePollDelay, cancellationToken);
            }
        }

        internal Task WaitForTableStateInternalAsync(
            TableResult tableResult, TableState tableState, TimeSpan? timeout,
            TimeSpan? pollDelay, CancellationToken cancellationToken) =>
            WaitForTableStateInternalAsync(tableResult,
                result => result.TableState == tableState,
                $"table state {tableState}", timeout, pollDelay,
                cancellationToken);

        internal async Task WaitForAdminCompletionAsync(
            AdminResult adminResult, TimeSpan? timeout, TimeSpan? pollDelay,
            CancellationToken cancellationToken)
        {
            if (adminResult.State == AdminState.Complete)
            {
                return;
            }

            var adminPollTimeout = timeout ?? Config.AdminPollTimeout;
            var adminPollDelay = pollDelay ?? Config.AdminPollDelay;

            var options = new GetAdminStatusOptions
            {
                Timeout = adminPollTimeout < Config.Timeout
                    ? adminPollTimeout
                    : Config.Timeout
            };

            var request = new AdminStatusRequest(this, adminResult, options);
            var startTime = DateTime.UtcNow;

            while (true)
            {
                var result = (AdminResult) await ExecuteValidatedRequestAsync(
                    request, cancellationToken);
                Debug.Assert(result != null);

                adminResult.State = result.State;
                adminResult.Output = result.Output;

                if (result.State == AdminState.Complete)
                {
                    return;
                }

                if (adminPollTimeout.HasValue)
                {
                    var remaining = startTime + adminPollTimeout -
                        (DateTime.UtcNow + adminPollDelay);

                    if (remaining <= TimeSpan.Zero)
                    {
                        throw new TimeoutException(
                            "Reached timeout while waiting for " +
                            "admin operation completion");
                    }

                    if (options.Timeout > remaining)
                    {
                        options.Timeout = remaining;
                    }
                }

                await Task.Delay(adminPollDelay, cancellationToken);
            }
        }

        private async Task<JsonDocument> ExecuteAdminListOpAsync(
            string statement,
            AdminOptions options,
            CancellationToken cancellationToken)
        {
            var result = await ExecuteAdminWithCompletionAsync(statement,
                options, cancellationToken);
            Debug.Assert(result != null);

            if (string.IsNullOrEmpty(result.Output))
            {
                return null;
            }

            try
            {
                return JsonDocument.Parse(result.Output);
            }
            catch (JsonException ex)
            {
                throw new BadProtocolException(
                    $"Error parsing output of command \"{statement}\"", ex);
            }
        }

        private async IAsyncEnumerable<ListTablesResult>
            GetListTablesAsyncEnumerableInternal(
                ListTablesRequest request,
                [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var options = request.Options;
            Debug.Assert(options != null);

            ListTablesResult result;

            do
            {
                result = (ListTablesResult)await ExecuteValidatedRequestAsync(
                    request, cancellationToken);
                if (result.TableNames.Count == 0)
                {
                    yield break;
                }

                options.FromIndex = result.NextIndex;
                yield return result;
            } while (options.Limit.HasValue &&
                     result.TableNames.Count == options.Limit.Value);
        }

        // We split into 2 methods to ensure that the request validation is
        // not deferred.
        private IAsyncEnumerable<ListTablesResult>
            GetListTablesAsyncEnumerableWithOptions(
                ListTablesOptions options,
                CancellationToken cancellationToken)
        {
            var request = new ListTablesRequest(this, options);
            request.Validate();

            return GetListTablesAsyncEnumerableInternal(request,
                cancellationToken);
        }

        private async IAsyncEnumerable<TableUsageResult>
            GetTableUsageAsyncEnumerableInternal(
                GetTableUsageRequest request,
                [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            var options = request.Options;
            Debug.Assert(options != null);

            var limit = options.Limit ?? GetTableUsageOptions.DefaultLimit;
            TableUsageResult result;

            do
            {
                result = (TableUsageResult)await ExecuteValidatedRequestAsync(
                    request, cancellationToken);
                if (result.UsageRecords.Count == 0)
                {
                    yield break;
                }

                options.FromIndex = result.NextIndex;
                yield return result;
            } while (result.UsageRecords.Count == limit);
        }

        // We split into 2 methods to ensure that the request validation is
        // not deferred.
        private IAsyncEnumerable<TableUsageResult>
            GetTableUsageAsyncEnumerableWithOptions(
                string tableName,
                GetTableUsageOptions options,
                CancellationToken cancellationToken)
        {
            var request = new GetTableUsageRequest(this, tableName,
                options, 4);
            request.Validate();

            return GetTableUsageAsyncEnumerableInternal(request,
                cancellationToken);
        }

        private async IAsyncEnumerable<DeleteRangeResult>
            GetDeleteRangeAsyncEnumerableInternal(
                DeleteRangeRequest request,
                [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            Debug.Assert(request.Options != null);
            DeleteRangeResult result;

            do
            {
                result = (DeleteRangeResult)
                    await ExecuteValidatedRequestAsync(
                        request, cancellationToken);

                if (result.ContinuationKey != null)
                {
                    request.Options.ContinuationKey = result.ContinuationKey;
                }

                yield return result;
            } while (result.ContinuationKey != null);
        }

        // We split into 2 methods to ensure that the request validation is
        // not deferred.
        private IAsyncEnumerable<DeleteRangeResult>
            GetDeleteRangeAsyncEnumerableWithOptions(
                string tableName,
                object partialPrimaryKey,
                DeleteRangeOptions options,
                CancellationToken cancellationToken)
        {
            var request = new DeleteRangeRequest(this, tableName,
                partialPrimaryKey, options);
            request.Validate();

            return GetDeleteRangeAsyncEnumerableInternal(request,
                cancellationToken);
        }

    }

}
