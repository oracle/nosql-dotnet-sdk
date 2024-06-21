/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Query {
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Diagnostics;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal class QueryRuntime
    {
        private FieldValue[] extVariables;
        private long totalMemory;
        private protected List<QueryTraceRecord> queryTraces;
#if DEBUG
        private protected StringBuilder driverTraceBuilder;
#endif

        internal ConsumedCapacity consumedCapacity;
        internal QueryContinuationKey continuationKey;

        internal NoSQLClient Client { get; }

        internal FieldValue[] ResultRegistry { get; }

        internal PreparedStatement PreparedStatement { get; }

        internal QueryRequest Request { get; set; }

        internal TopologyInfo BaseTopology { get; }

        internal long MaxMemory { get; set; }

        // Used only for error reporting
        internal string MaxMemoryStr => MaxMemory % 0x100000 == 0 ?
            $"{MaxMemory / 0x100000} MB" : $"{MaxMemory} bytes";

        internal long TotalMemory
        {
            get => totalMemory;
            set
            {
                Debug.Assert(value >= 0);
                if (value > MaxMemory)
                {
                    throw new InvalidOperationException(
                        "Query: memory exceeded maximum allowed value of " +
                        MaxMemoryStr);
                }
                totalMemory = value;
            }
        }

        internal bool FetchDone { get; set; }

        internal bool NeedContinuation
        {
            get => continuationKey != null;
            set
            {
                if (value)
                {
                    continuationKey ??= new QueryContinuationKey
                    {
                        PreparedStatement = PreparedStatement,
                        Runtime = this
                    };
                }
                else
                {
                    continuationKey = null;
                }
            }
        }

        internal bool IsForTest => Request?.Options?.IsForTest ?? false;

        internal QueryRuntime(NoSQLClient client,
            PreparedStatement preparedStatement)
        {
            Client = client;
            PreparedStatement = preparedStatement;
            ResultRegistry = new FieldValue[preparedStatement.RegisterCount];

            if (!preparedStatement.DriverQueryPlan.IsAsync)
            {
                throw new InvalidOperationException(
                    "Query plan: the top-level step is not async");
            }

            if (preparedStatement.VariableNames != null)
            {
                InitExternalVariables();
            }

            BaseTopology = client.QueryTopology;
        }

        private void InitExternalVariables()
        {
            var variables = PreparedStatement.Variables;
            if (variables.Count != PreparedStatement.VariableNames.Length)
            {
                throw new ArgumentException(
                    "Query: number of bound external variables " +
                    $"{variables.Count} does not match expected " +
                    PreparedStatement.VariableNames.Length);
            }

            extVariables = new FieldValue[variables.Count];
            for (var i = 0; i < variables.Count; i++)
            {
                var name = PreparedStatement.VariableNames[i];
                if (!variables.ContainsKey(name))
                {
                    throw new ArgumentException(
                        $"Query: unbound external variable {name}");
                }

                extVariables[i] = FieldValue.FromObject(variables[name]);
            }
        }

        // Initialize consumed capacity for the current query call.
        private protected void InitConsumedCapacity()
        {
            // PreparedStatement will have consumed capacity set if
            // and only if this is not on-prem.
            if (PreparedStatement.ConsumedCapacity != null)
            {
                consumedCapacity = new ConsumedCapacity();

                // For direct queries that were not prepared by the user
                // (Request.Statement != null), we prepare the query and start
                // its execution in the same API call.  Thus we need to add
                // the preparation cost to the consumed capacity of the result
                // when this is the first execution (continuationKey == null).
                if (Request.Statement != null && continuationKey == null)
                {
                    consumedCapacity.Add(PreparedStatement.ConsumedCapacity);
                }
            }
        }

        [Conditional("DEBUG")]
        private protected void CheckAddDriverQueryTrace()
        {
#if DEBUG
            if (!(Request.Options?.TraceLevel.HasValue ?? false))
            {
                return;
            }

            queryTraces ??= new List<QueryTraceRecord>();
            queryTraces.Add(new QueryTraceRecord
            {
                BatchName = $"{DateTime.UtcNow:u} " +
                    $"DRIVER B-{Request.Options.BatchNumber}",
                BatchTrace = driverTraceBuilder?.ToString() ?? ""
            });
#endif
        }

        internal void TallyConsumedCapacity(ConsumedCapacity other)
        {
            if (other != null)
            {
                Debug.Assert(consumedCapacity != null);
                consumedCapacity.Add(other);
            }
        }

        internal void AddServerQueryTraces(
            IReadOnlyList<QueryTraceRecord> traces)
        {
            // Copying traces may not be currently necessary since there is
            // only one fetch per individual request, but it's done this way
            // in the Java driver (perhaps to accomodate future changes).
            Debug.Assert(traces != null);
            queryTraces ??= new List<QueryTraceRecord>();
            queryTraces.AddRange(traces);
        }

        internal string GetMessageWithLocation(string message,
            ExpressionLocation location)
        {
            return $"Query: {message}.  At: " +
            $"{location.StartLine}:{location.StartColumn}-" +
            $"{location.EndLine}:{location.EndColumn}";
        }

        internal FieldValue GetExtVariable(int pos) => extVariables?[pos];

        [Conditional("DEBUG")]
        internal void Trace(string message, int level = 1)
        {
            // The conditional method will still compile even if DEBUG is not
            // defined, so we need #if DEBUG to avoid compile errors with
            // driverTraceBuilder.
#if DEBUG
            Debug.Assert(Request != null);

            if (!(Request.Options?.TraceLevel.HasValue ?? false) ||
                Request.Options.TraceLevel < level)
            {
                return;
            }

            driverTraceBuilder ??= new StringBuilder();
            driverTraceBuilder.Append($"{DateTime.UtcNow:u} : ")
                .Append(message).Append('\n');
#endif
        }

        [Conditional("DEBUG")]
        internal void TraceExternalVariables()
        {
            if (!(Request.Options?.TraceLevel.HasValue ?? false))
            {
                return;
            }

            Debug.Assert(PreparedStatement.VariableNames.Length ==
                extVariables.Length);

            for (var i = 0; i < extVariables.Length; i++)
            {
                var name = PreparedStatement.VariableNames[i];
                Trace($"Set external variable, index={i}, name={name}, " +
                    $"dbType={extVariables[i].DbType}, " +
                    $"value={extVariables[i]}", 3);
            }
        }
    }

    internal class QueryPlanExecutor<TRow> : QueryRuntime
    {
        private PlanAsyncIterator iterator;
        private List<TRow> rows;

        internal QueryPlanExecutor(NoSQLClient client,
            PreparedStatement preparedStatement) :
            base(client, preparedStatement)
        {
        }

        internal async Task<QueryResult<TRow>> ExecuteAsync(
            QueryRequest<TRow> queryRequest,
            CancellationToken cancellationToken)
        {
            Request = queryRequest;
            Request.Init();
            MaxMemory = queryRequest.MaxMemory;

            // We limit to 1 request to the server per user's call to Query()
            FetchDone = false;
            // Indicates whether user needs to call Query() again
            NeedContinuation = false;

            // Make sure we don't append to query traces from previous
            // request.
            queryTraces = null;
#if DEBUG
            driverTraceBuilder = null;
            TraceExternalVariables();
#endif

            // If the previous call threw retry-able exception, we may still
            // have results (this.rows).  In this case we return them and let
            // user issue query again to get more results.
            if (rows == null)
            {
                InitConsumedCapacity();
                rows = new List<TRow>();
                var limit = Request.Options?.Limit;
                iterator ??= PreparedStatement.DriverQueryPlan
                    .CreateAsyncIterator(this);

                while (await iterator.NextAsync(cancellationToken))
                {
                    var row = iterator.Result;
                    if (!(row is RecordValue))
                    {
                        throw new InvalidOperationException(
                            $"Query result is not a record value: {row}");
                    }

                    rows.Add(((RecordValue)row).ToObject<TRow>());
                    if (limit.HasValue && rows.Count == limit)
                    {
                        NeedContinuation = true;
                        break;
                    }
                }
            }

            var result = new QueryResult<TRow>
            {
                ConsumedCapacity = consumedCapacity,
                Rows = rows,
                ContinuationKey = continuationKey
            };

            CheckAddDriverQueryTrace();

            if (queryTraces != null)
            {
                result.QueryTraces = queryTraces;
            }

            rows = null;
            return result;
        }

    }

}
