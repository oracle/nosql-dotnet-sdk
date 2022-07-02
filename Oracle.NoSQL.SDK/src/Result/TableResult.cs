/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Text.RegularExpressions;
    using System.Threading;
    using System.Threading.Tasks;
    using static ValidateUtils;

    /// <summary>
    /// Represents the static information about a table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is the result of APIs
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>,
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>,
    /// <see cref="NoSQLClient.SetTableLimitsAsync"/>,
    /// <see cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/>,
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetTableAsync*"/>,
    /// as well as <see cref="TableResult.WaitForCompletionAsync"/> and
    /// <see cref="NoSQLClient.WaitForTableStateAsync"/>.
    /// It encapsulates the state of the table that is the target of the
    /// operation.
    /// </para>
    /// <para>
    /// Table DDL operations performed by
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>
    /// and <see cref="NoSQLClient.SetTableLimitsAsync"/> such as table
    /// creation, modification, and drop are potentially long running and not
    /// necessarily completed when these methods return result and the table
    /// may still be in one of its intermediate states. You may call
    /// <see cref="TableResult.WaitForCompletionAsync"/> to be notified when
    /// the operation completes and the table reaches desired state (which,
    /// depending on the operation, would be either
    /// <see cref="SDK.TableState.Active"/> or
    /// <see cref="SDK.TableState.Dropped"/>.
    /// </para>
    /// <para>
    /// Alternatively you may use
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>
    /// or <see cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/> which
    /// will return the result only when the DDL operation is fully completed.
    /// You may also call
    /// <see cref="NoSQLClient.GetTableAsync(string, GetTableOptions, CancellationToken)"/>
    /// to receive static information about the table as well as its current
    /// state.
    /// </para>
    /// </remarks>
    /// <seealso cref="NoSQLClient.ExecuteTableDDLAsync"/>
    /// <seealso cref="NoSQLClient.ExecuteTableDDLWithCompletionAsync"/>
    /// <seealso cref="NoSQLClient.SetTableLimitsAsync"/>
    /// <seealso cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/>
    /// <seealso cref="NoSQLClient.GetTableAsync"/>
    /// <seealso cref="TableResult.WaitForCompletionAsync"/>
    /// <seealso cref="NoSQLClient.WaitForTableStateAsync"/>
    public class TableResult
    {
        internal const TableState UnknownTableState = (TableState)(-1);

        private readonly TableDDLRequest request;

        internal string OperationId { get; set; }

        internal TableResult()
        {
        }

        internal TableResult(TableDDLRequest request = null)
        {
            this.request = request;
        }

        /// <summary>
        /// Cloud Service only.  Gets the compartment id of the table.
        /// </summary>
        /// <value>
        /// Compartment id.
        /// </value>
        public string CompartmentId { get; internal set; }

        // Tests should check that TableState value is set to correct enum
        // value upon being returned.

        /// <summary>
        /// Gets the current table state.
        /// </summary>
        /// <value>
        /// Current table state.
        /// </value>
        public TableState TableState { get; internal set; } =
            UnknownTableState;

        /// <summary>
        /// Gets the name of the table.
        /// </summary>
        /// <value>
        /// Table name.
        /// </value>
        public string TableName { get; internal set; }

        /// <summary>
        /// Gets the table schema.
        /// </summary>
        /// <value>
        /// Table schema as JSON string.
        /// </value>
        public string TableSchema { get; internal set; }

        /// <summary>
        /// Gets the throughput and capacity limits for the table.
        /// </summary>
        /// <remarks>
        /// Valid for Cloud Service/Cloud Simulator only.  For on-premise
        /// NoSQL database this property returns <c>null</c>.
        /// </remarks>
        /// <value>
        /// Table limits.
        /// </value>
        public TableLimits TableLimits { get; internal set; }

        /// <summary>
        /// Asynchronously waits for completion of table DDL operations.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Table DDL operations are issued by methods
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>
        /// and <see cref="NoSQLClient.SetTableLimitsAsync"/>.
        /// </para>
        /// <para>
        /// The wait is accomplished by polling the operation state at
        /// specified interval.  When the operation completes, the table
        /// state should be <see cref="SDK.TableState.Active"/> or
        /// <see cref="SDK.TableState.Dropped"/> (only for
        /// <em>DROP TABLE</em> operation).
        /// </para>
        /// <para>
        /// The result of this method is a <see cref="TableResult"/> that
        /// represents the final state of the operation (which is the result
        /// of the last poll).  If the operation fails, this method will
        /// throw exception with the information about the operation failure.
        /// </para>
        /// <para>
        /// Note that in addition to the result returned, this calling
        /// instance is modified with any change in table state or metadata
        /// and will reflect the operation completion upon return.
        /// </para>
        /// <para>
        /// This method need only be called on instances of
        /// <see cref="TableResult"/> returned from
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>
        /// and <see cref="NoSQLClient.SetTableLimitsAsync"/>.  If you are
        /// using
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>
        /// or <see cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/> you
        /// need not call this method on the returned result, as it will
        /// already reflect operation completion.
        /// </para>
        /// </remarks>
        /// <param name="timeout">(Optional) Timeout reflecting how long to
        /// keep polling for the operation completion.  Must be positive
        /// value. Defaults to <see cref="NoSQLConfig.TablePollTimeout"/>
        /// if the latter is set or to no timeout if the latter is not set.
        /// </param>
        /// <param name="pollDelay">(Optional) Delay between successive polls,
        /// determines how often the polls are performed.  Must be positive
        /// value and not greater then the timeout.  Defaults to
        /// <see cref="NoSQLConfig.TablePollDelay"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="timeout"/> or <paramref name="pollDelay"/> are
        /// invalid or <paramref name="pollDelay"/> is greater than the
        /// timeout.</exception>
        /// <exception cref="TimeoutException">Table DDL operation has not
        /// completed within the timeout period.  May also be thrown if
        /// the service becomes unreachable due to network connectivity.
        /// </exception>
        /// <exception cref="InvalidOperationException">If this method is
        /// called on an instance of <see cref="TableResult"/> other than
        /// those returned by
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>
        /// or <see cref="NoSQLClient.SetTableLimitsAsync"/> or if the
        /// service is not in a valid state to perform the operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses may reflect the failure of the operation
        /// being polled for. See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        public async Task<TableResult> WaitForCompletionAsync(
            TimeSpan? timeout = null,
            TimeSpan? pollDelay = null,
            CancellationToken cancellationToken = default)
        {
            if (request == null)
            {
                throw new InvalidOperationException(
                    "Cannot call WaitForCompletionAsync because this " +
                    "TableResult is not a result of ExecuteTableDDL");
            }

            var tableState = request.Statement != null &&
                Regex.IsMatch(request.Statement, @"^\s*DROP\s+TABLE\s+",
                    RegexOptions.IgnoreCase) ?
                    TableState.Dropped : TableState.Active;

            CheckPollParameters(timeout, pollDelay, nameof(timeout),
                nameof(pollDelay));
            await request.Client.WaitForTableStateInternalAsync(this,
                tableState, timeout, pollDelay, cancellationToken);

            return this;
        }

    }

}
