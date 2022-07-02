/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;
    using static ValidateUtils;

    public partial class NoSQLClient
    {
        /// <summary>
        /// Executes a DDL operation on a table.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The operations allowed are defined by the Data Definition Language
        /// (DDL) portion of the query language related to tables such as
        /// table creation and drop, index add and drop, and the ability to
        /// alter table schema and table limits.
        /// </para>
        /// <para>
        /// Operations using table DDL statements infer the table name
        /// from the statement itself, e.g.
        /// <em>CREATE TABLE MyTable(...)</em>. Table creation requires a
        /// valid <see cref="TableLimits"/> object to define the throughput
        /// and storage desired for the table.  It is an error for TableLimits
        /// to be specified with a statement other than create or alter table.
        /// </para>
        /// <para>
        /// Note that these are potentially long-running operations, so the
        /// result returned by this API does not imply operation completion
        /// and the table may be in an intermediate state (see
        /// <see cref="TableState"/> for more details).  Call
        /// <see cref="TableResult.WaitForCompletionAsync"/> on returned
        /// <see cref="TableResult"/> to asynchronously wait for the operation
        /// completion.  Alternatively, you may check the status of the
        /// running DDL operation by calling <see cref="GetTableAsync"/>.
        /// </para>
        /// <para>
        /// When the DDL operation is completed, the table state should be
        /// either <see cref="TableState.Active"/> or
        /// <see cref="TableState.Dropped"/> (only if the operation was
        /// <em>DROP TABLE</em>).  To get only the final result, instead of
        /// this API, call <see cref="ExecuteTableDDLWithCompletionAsync"/>,
        /// which is equivalent to calling
        /// <see cref="ExecuteTableDDLAsync"/> and then
        /// <see cref="TableResult.WaitForCompletionAsync"/>.
        /// </para>
        /// </remarks>
        /// <param name="statement">SQL statement.</param>
        /// <param name="options">(Optional) Options for table DDL operation.
        /// If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="TableDDLOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="statement"/> is <c>null</c> or invalid
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or
        /// the service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="TableDDLOptions"/>
        /// <seealso cref="TableResult"/>
        public async Task<TableResult> ExecuteTableDDLAsync(
            string statement,
            TableDDLOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (TableResult) await ExecuteRequestAsync(
                new TableDDLRequest(this, statement, options),
                cancellationToken);
        }

        /// <summary>
        /// Executes DDL operation on a table.
        /// </summary>
        /// <remarks>
        /// This API is a shorthand for
        /// <see cref="ExecuteTableDDLAsync(string, TableDDLOptions, CancellationToken)"/>
        /// that takes <see cref="TableLimits"/> instead of
        /// <see cref="TableDDLOptions"/> and can be used for table creation
        /// when you don't need to pass any other options.
        /// </remarks>
        /// <param name="statement">SQL statement.</param>
        /// <param name="tableLimits">Table limits for table creation.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="statement"/> is <c>null</c> or invalid
        /// or <paramref name="tableLimits"/> is <c>null</c> or
        /// contains invalid values.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="ExecuteTableDDLAsync(string, TableDDLOptions, CancellationToken)"/>
        public Task<TableResult> ExecuteTableDDLAsync(
            string statement,
            TableLimits tableLimits,
            CancellationToken cancellationToken = default)
        {
            return ExecuteTableDDLAsync(statement, new TableDDLOptions
            {
                TableLimits = tableLimits
            }, cancellationToken);
        }

        /// <summary>
        /// Executes DDL operation on a table.
        /// </summary>
        /// <remarks>
        /// This API is a shorthand for
        /// <see cref="ExecuteTableDDLAsync(string, TableDDLOptions, CancellationToken)"/>.
        /// Use this overload when you need to provide
        /// <see cref="CancellationToken"/> but you don't need to provide
        /// <see cref="TableDDLOptions"/>.
        /// </remarks>
        /// <param name="statement">SQL statement.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="statement"/> is <c>null</c> or invalid.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="ExecuteTableDDLAsync(string, TableDDLOptions, CancellationToken)"/>
        public Task<TableResult> ExecuteTableDDLAsync(
            string statement,
            CancellationToken cancellationToken)
        {
            return ExecuteTableDDLAsync(statement, (TableDDLOptions)null,
                cancellationToken);
        }

        /// <summary>
        /// Executes DDL operation on a table and asynchronously waits for its
        /// completion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is equivalent to calling
        /// <see cref="ExecuteTableDDLAsync"/> and then calling
        /// <see cref="TableResult.WaitForCompletionAsync"/> on the returned
        /// <see cref="TableResult"/> object.  If the operation is successful,
        /// the table state in the resulting <see cref="TableResult"/> object
        /// should be either <see cref="TableState.Active"/> or
        /// <see cref="TableState.Dropped"/> (only if the operation was
        /// <em>DROP TABLE</em>).
        /// </para>
        /// <para>
        /// For this operation, <see cref="TableDDLOptions.Timeout"/> covers
        /// the total time interval including waiting for the DDL operation
        /// completion.  If not specified, separate default timeouts are used
        /// for issuing the DDL operation and waiting for its completion, with
        /// values of <see cref="NoSQLConfig.TableDDLTimeout"/> and
        /// <see cref="NoSQLConfig.TablePollTimeout"/> correspondingly (the
        /// latter defaults to no timeout if
        /// <see cref="NoSQLConfig.TablePollTimeout"/> is not set).
        /// Note that as with <see cref="TableResult.WaitForCompletionAsync"/>
        /// you may specify poll delay as
        /// <see cref="TableDDLOptions.PollDelay"/> which otherwise defaults
        /// to <see cref="NoSQLConfig.TablePollDelay"/>.
        /// </para>
        /// </remarks>
        /// <param name="statement">SQL statement.</param>
        /// <param name="options">(Optional) Options for table DDL operation.
        /// If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="TableDDLOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="statement"/> is <c>null</c> or invalid
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="ExecuteTableDDLAsync(string, TableDDLOptions, CancellationToken)"/>
        /// <seealso cref="TableResult.WaitForCompletionAsync"/>
        public Task<TableResult> ExecuteTableDDLWithCompletionAsync(
            string statement,
            TableDDLOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return DoTableDDLWithCompletionAsync(
                new TableDDLRequest(this, statement, options),
                cancellationToken);
        }

        /// <summary>
        /// Executes DDL operation on a table and waits asynchronously for its
        /// completion.
        /// </summary>
        /// <remarks>
        /// This method is a shorthand for
        /// <see cref="ExecuteTableDDLWithCompletionAsync(string, TableDDLOptions, CancellationToken)"/>
        /// that takes <see cref="TableLimits"/> instead of
        /// <see cref="TableDDLOptions"/> and can be used for table creation
        /// when you don't need to pass any other options.
        /// </remarks>
        /// <example>
        /// Create a table.  We can ignore the return value since the result
        /// will represent successful completion of the table DDL operation
        /// (<see cref="TableState.Active"/>) or an exception will be thrown.
        /// <code>
        /// try
        /// {
        ///     await client.ExecuteTableDDLWithCompletionAsync(
        ///         "CREATE TABLE foo(id INTEGER, name STRING, PRIMARY KEY(id))",
        ///         new TableLimits(100, 100, 50));
        ///     Console.WriteLine("Table created.");
        /// }
        /// catch(Exception e)
        /// {
        ///     Console.WriteLine($"Got exception: {e.Message}");
        /// }
        /// </code>
        /// </example>
        /// <param name="statement">SQL statement.</param>
        /// <param name="tableLimits">Table limits for table creation.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="statement"/> is <c>null</c> or invalid
        /// or <paramref name="tableLimits"/> is <c>null</c> or
        /// contains invalid values.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="ExecuteTableDDLWithCompletionAsync(string, TableDDLOptions, CancellationToken)"/>
        public Task<TableResult> ExecuteTableDDLWithCompletionAsync(
            string statement,
            TableLimits tableLimits,
            CancellationToken cancellationToken = default)
        {
            return ExecuteTableDDLWithCompletionAsync(statement,
                new TableDDLOptions
                {
                    TableLimits = tableLimits
                }, cancellationToken);
        }

        /// <summary>
        /// Executes DDL operation on a table and waits asynchronously for its
        /// completion.
        /// </summary>
        /// <remarks>
        /// This method is a shorthand for
        /// <see cref="ExecuteTableDDLWithCompletionAsync(string, TableDDLOptions, CancellationToken)"/>.
        /// Use this overload when you need to provide
        /// <see cref="CancellationToken"/> but you don't need to provide
        /// <see cref="TableDDLOptions"/>.
        /// </remarks>
        /// <param name="statement">SQL statement.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="statement"/> is <c>null</c> or invalid.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="ExecuteTableDDLWithCompletionAsync(string, TableDDLOptions, CancellationToken)"/>
        public Task<TableResult> ExecuteTableDDLWithCompletionAsync(
            string statement,
            CancellationToken cancellationToken)
        {
            return ExecuteTableDDLWithCompletionAsync(statement,
                (TableDDLOptions)null, cancellationToken);
        }

        /// <summary>
        /// Cloud Service Only.  Sets new limits of throughput and storage for
        /// existing table.
        /// <remarks>
        /// <para>
        /// Note: this method is supported with the Cloud Service and Cloud
        /// Simulator but is not supported with on-premise NoSQL database (see
        /// <see cref="SDK.ServiceType.KVStore">ServiceType.KVStore</see>),
        /// where it is a no-op.
        /// </para>
        /// <para>
        /// This method is similar to <see cref="ExecuteTableDDLAsync"/>,
        /// so all considerations discussed  apply here, including long
        /// running DDL operations and the need to use
        /// <see cref="TableResult.WaitForCompletionAsync"/> to asynchronously
        /// wait for operation completion.
        /// </para>
        /// </remarks>
        /// </summary>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="tableLimits">The new table limits.</param>
        /// <param name="options">(Optional) Options for the operation.
        /// If not specified or <c>null</c>, appropriate defaults
        /// will be used.  This parameter should not be used to specify table
        /// limits, use <paramref name="tableLimits"/> parameter instead.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="tableName"/> is <c>null</c> or invalid
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="ExecuteTableDDLAsync(string, TableDDLOptions, CancellationToken)"/>
        /// <seealso cref="TableDDLOptions"/>
        /// <seealso cref="TableResult"/>
        public async Task<TableResult> SetTableLimitsAsync(
            string tableName,
            TableLimits tableLimits,
            TableDDLOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (TableResult) await ExecuteRequestAsync(
                new TableLimitsRequest(this, tableName, tableLimits, options),
                cancellationToken);
        }

        /// <summary>
        /// Cloud Service Only.  Sets table limits on existing table and
        /// asynchronously waits for the operation completion completion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is equivalent to
        /// calling <see cref="SetTableLimitsAsync"/> and then calling
        /// <see cref="TableResult.WaitForCompletionAsync"/> on the returned
        /// <see cref="TableResult"/> object.  If the operation is successful,
        /// the table state in the resulting <see cref="TableResult"/> object
        /// should be <see cref="TableState.Active"/>.
        /// </para>
        /// <para>
        /// Note: this method is supported with the Cloud Service and Cloud
        /// Simulator but is not supported with on-premise NoSQL database (see
        /// <see cref="SDK.ServiceType.KVStore">ServiceType.KVStore</see>),
        /// where it is a no-op.
        /// </para>
        /// <para>
        /// For this operation, <see cref="TableDDLOptions.Timeout"/> covers
        /// the total time interval including waiting for the DDL operation
        /// completion.  If not specified, separate default timeouts are used
        /// for issuing the DDL operation and waiting for its completion, with
        /// values of <see cref="NoSQLConfig.TableDDLTimeout"/> and
        /// <see cref="NoSQLConfig.TablePollTimeout"/> correspondingly (the
        /// latter defaults to no timeout if
        /// <see cref="NoSQLConfig.TablePollTimeout"/> is not set).
        /// Note that as with <see cref="TableResult.WaitForCompletionAsync"/>
        /// you may specify poll delay as
        /// <see cref="TableDDLOptions.PollDelay"/> which otherwise defaults
        /// to <see cref="NoSQLConfig.TablePollDelay"/>.
        /// </para>
        /// </remarks>
        /// <example>
        /// Updating limits on a table.  We can ignore the return value since
        /// the result would represent the final state of successful operation
        /// (or an exception will be thrown).
        /// <code>
        /// try
        /// {
        ///     await client.SetTableLimitsWithCompletionAsync(
        ///         "myTable",
        ///         new TableLimits(100, 100, 50));
        ///     Console.WriteLine("Table limits updated.");
        /// }
        /// catch(Exception e)
        /// {
        ///     Console.WriteLine($"Got exception: {e.Message}");
        /// }
        /// </code>
        /// </example>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="tableLimits">The new table limits.</param>
        /// <param name="options">(Optional) Options for the operation.
        /// If not specified or <c>null</c>, appropriate defaults
        /// will be used.  This parameter should not be used to specify table
        /// limits, use <paramref name="tableLimits"/> parameter instead.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="tableName"/> is <c>null</c> or invalid
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="SetTableLimitsAsync"/>
        /// <seealso cref="TableResult.WaitForCompletionAsync"/>
        public Task<TableResult> SetTableLimitsWithCompletionAsync(
            string tableName,
            TableLimits tableLimits,
            TableDDLOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return DoTableDDLWithCompletionAsync(
                new TableLimitsRequest(this, tableName, tableLimits, options),
                cancellationToken);
        }

        /// <summary>
        /// Retrieves static information about a table in the form of
        /// <see cref="TableResult"/>.
        /// </summary>
        /// <remarks>
        /// This information includes the table state, provisioned throughput,
        /// capacity and schema. Dynamic information such as usage is obtained
        /// using <see cref="GetTableUsageAsync"/>.
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="options">(Optional) Options for GetTable operation.
        /// If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="GetTableOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="tableName"/> is <c>null</c> or invalid
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The service is not in
        /// a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="GetTableOptions"/>
        /// <seealso cref="TableResult"/>
        public async Task<TableResult> GetTableAsync(
            string tableName,
            GetTableOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (TableResult) await ExecuteRequestAsync(
                new GetTableRequest(this, tableName, options),
                cancellationToken);
        }

        /// <summary>
        /// Retrieves static information about a table in the form of
        /// <see cref="TableResult"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This information includes the table state, provisioned throughput,
        /// capacity and schema.  This method is similar to
        /// <see cref="GetTableAsync(string, GetTableOptions, CancellationToken)"/>
        /// but instead of table name it takes <see cref="TableResult"/>
        /// object returned by <see cref="ExecuteTableDDLAsync"/>.  This
        /// allows, in addition to table information, to retrieve error
        /// information for the DDL operation.  If DDL operation represented
        /// by <paramref name="tableResult"/> failed, this follow-on call will
        /// throw exception containing the error information.
        /// </para>
        /// <para>
        /// Note that as with
        /// <see cref="GetTableAsync(string, GetTableOptions, CancellationToken)"/>
        /// this operation will only retrieve information at one point in
        /// time.  If you wish to asynchronously wait for DDL operation
        /// completion, use <see cref="TableResult.WaitForCompletionAsync"/>
        /// instead.
        /// </para>
        /// </remarks>
        /// <param name="tableResult">Table result representing ongoing table
        /// DDL operation.</param>
        /// <param name="options">(Optional) Options for GetTable operation.
        /// If not specified or <c>null</c>, appropriate defaults
        /// will be used.  See <see cref="GetTableOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="tableResult"/> is <c>null</c> or
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The service is not in
        /// a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="GetTableOptions"/>
        /// <seealso cref="TableResult"/>
        public async Task<TableResult> GetTableAsync(
            TableResult tableResult,
            GetTableOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (TableResult) await ExecuteRequestAsync(
                new GetTableRequest(this, tableResult, options),
                cancellationToken);
        }

        /// <summary>
        /// Waits asynchronously for a table to reach a desired state.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This is achieved by polling the table at specified interval.  You
        /// can use this API to ensure that the table is ready for data
        /// operations after it has been created or altered.
        /// </para>
        /// <para>
        /// Note: the preferred way of ensuring that the table is ready for
        /// data operations after table DDL has been performed is to call
        /// <see cref="TableResult.WaitForCompletionAsync"/> on a
        /// <see cref="TableResult"/> object returned by
        /// <see cref="ExecuteTableDDLAsync"/> representing ongoing table
        /// DDL operation.  Use this API only in a rare use case where DDL
        /// operation is performed outside your control and you don't have its
        /// <see cref="TableResult"/>.
        /// </para>
        /// <para>
        /// This API waits until the table has transitioned from an
        /// intermediate state like <see cref="TableState.Creating"/> or
        /// <see cref="TableState.Updating"/> to a stable state like
        /// <see cref="TableState.Active"/>, at which point it can be used.
        /// The result of this operation, if successful, is a
        /// <see cref="TableResult"/> that shows the table state from the
        /// last poll.  If <paramref name="tableState"/> is
        /// <see cref="TableState.Dropped"/> this method will return
        /// successfully once the table no longer exists and the resulting
        /// <see cref="TableResult"/> will contain only the table name and
        /// state (dropped), with other properties being
        /// <c>null</c>.
        /// </para>
        /// <para>
        /// Note that unlike other methods that return
        /// <see cref="TableResult"/> this method will not fail if the table
        /// doesn't exist and even if the target <paramref name="tableState"/>
        /// is <see cref="TableState.Active"/>.  Instead, it will keep polling
        /// until the table is created or the timeout is reached, which allows
        /// you to asynchronously wait for table creation done outside your
        /// control.  Caution need to be exercised using this method to avoid
        /// excessive poll times.
        /// </para>
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="tableState">Desired table state, usually
        /// <see cref="TableState.Active"/> or
        /// <see cref="TableState.Dropped"/></param>
        /// <param name="options">(Optional) Options for this operation.
        /// If not specified or <c>null</c>, defaults will be used.
        /// See <see cref="TableCompletionOptions"/>.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="tableName"/> is <c>null</c> or invalid
        /// or <paramref name="tableState"/> is invalid or
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The service is not in
        /// a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="TableCompletionOptions"/>
        /// <seealso cref="TableResult"/>
        public async Task<TableResult> WaitForTableStateAsync(
            string tableName,
            TableState tableState,
            TableCompletionOptions options = null,
            CancellationToken cancellationToken = default)
        {
            CheckTableName(tableName);
            CheckEnumValue(tableState);
            CheckPollParameters(options?.Timeout, options?.PollDelay,
                nameof(options.Timeout), nameof(options.PollDelay));

            var result = new TableResult
            {
                TableName = tableName
            };

            await WaitForTableStateInternalAsync(result, tableState,
                options?.Timeout, options?.PollDelay, cancellationToken);
            return result;
        }

        /// <summary>
        /// Cloud Service Only.  Retrieves dynamic information associated with
        /// a table in the form of <see cref="TableUsageResult"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This information includes a time series of usage snapshots, each
        /// indicating data such as read and write throughput, throttling
        /// events, etc, as found in <see cref="TableUsageRecord"/>.
        /// </para>
        /// <para>
        /// Note: this method is supported with the Cloud Service and Cloud
        /// Simulator but is not supported with on-premise NoSQL database (see
        /// <see cref="SDK.ServiceType.KVStore">ServiceType.KVStore</see>),
        /// where it will result in exception.
        /// </para>
        /// <para>
        /// Usage information is collected in time slices and returned in
        /// individual usage records.  It is possible to return a range of
        /// usage records within a given time period.  Unless the time period
        /// is specified, only one most recent usage record is returned.
        /// Usage records are created on a regular basis and maintained for a
        /// period of time.  Only records for time periods that have completed
        /// are returned so that a user never sees changing data for a
        /// specific range.
        /// </para>
        /// <para>
        /// The usage record time slices are short (1 minute) and
        /// <see cref="TableUsageResult"/> will contain one
        /// <see cref="TableUsageRecord"/> per each time slice in a specified
        /// time range, regardless of whether the table was used at that time
        /// or was idle.  Thus, care needs to be taken when specifying the
        /// time range (see <see cref="GetTableUsageOptions.StartTime"/>  and
        /// <see cref="GetTableUsageOptions.EndTime"/>) to avoid returning
        /// excessive number of table usage records.  Another way is to
        /// specify <see cref="GetTableUsageOptions.Limit"/> option to limit
        /// the number of usage records returned. See
        /// <see cref="GetTableUsageOptions"/>.
        /// </para>
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="options">(Optional) Options for this operation, which
        /// allow specify a time range for usage records, limit on the number
        /// usage records returned and other parameters.  If not specified
        /// or <c>null</c>, appropriate defaults will be used.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableUsageResult"/>.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="tableName"/> is <c>null</c> or invalid
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or service
        /// is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NotSupportedException">If this operation is
        /// invoked on on-premise NoSQL database (see
        /// <see cref="SDK.ServiceType.KVStore">ServiceType.KVStore</see>).
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="GetTableUsageOptions"/>
        /// <seealso cref="TableUsageResult"/>
        public async Task<TableUsageResult> GetTableUsageAsync(
            string tableName,
            GetTableUsageOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (TableUsageResult) await ExecuteRequestAsync(
                new GetTableUsageRequest(this, tableName, options),
                cancellationToken);
        }

        /// <summary>
        /// Retrieves the information about indexes of the table.
        /// </summary>
        /// <remarks>
        /// This information is retrieved as a list of
        /// <see cref="IndexResult"/> objects.
        /// </remarks>
        /// <example>
        /// Displaying table indexes.
        /// <code>
        /// var results = await client.GetIndexesAsync("MyTable");
        /// foreach(var result in results)
        /// {
        ///     var fields = string.Join(", ", result.Fields);
        ///     Console.WriteLine($"{result.IndexName}({fields})");
        /// }
        /// </code>
        /// </example>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="options">(Optional) Options for this operation.  If
        /// not specified or <c>null</c>, appropriate defaults will
        /// be used.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning a list of <see cref="IndexResult"/>
        /// objects for each index of the table.  If there are no indexes,
        /// empty list is returned.
        /// </returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="tableName"/> is <c>null</c> or invalid
        /// or <paramref name="options"/> contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or service
        /// is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="GetIndexOptions"/>
        /// <seealso cref="IndexResult"/>
        public async Task<IReadOnlyList<IndexResult>> GetIndexesAsync(
            string tableName,
            GetIndexOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (IReadOnlyList<IndexResult>) await ExecuteRequestAsync(
                new GetIndexesRequest(this, tableName, options),
                cancellationToken);
        }

        /// <summary>
        /// Retrieves the information about specific index of the table.
        /// </summary>
        /// <remarks>
        /// This information is retrieved as <see cref="IndexResult"/>
        /// object.  If provided index name is not found,
        /// <see cref="IndexNotFoundException"/> is thrown.
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="indexName">Name of the index.</param>
        /// <param name="options">(Optional) Options for this operation.  If
        /// not specified or <c>null</c>, appropriate defaults will
        /// be used.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="IndexResult"/> object.
        /// </returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="tableName"/> is <c>null</c> or invalid
        /// or <paramref name="indexName"/> is <c>null</c> or
        /// invalid or <paramref name="options"/> contains invalid values.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or service
        /// is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="GetIndexOptions"/>
        /// <seealso cref="IndexResult"/>
        public async Task<IndexResult> GetIndexAsync(
            string tableName,
            string indexName,
            GetIndexOptions options = null,
            CancellationToken cancellationToken = default)
        {
            var request = new GetIndexRequest(this, tableName, indexName,
                options);
            var result = (IReadOnlyList<IndexResult>)
                await ExecuteRequestAsync(request, cancellationToken);
            Debug.Assert(result != null);

            if (result.Count != 1)
            {
                throw new BadProtocolException(
                    $"Unexpected number of index results: {result.Count}");
            }

            return result[0];
        }

        /// <summary>
        /// Lists tables, returning table names.
        /// </summary>
        /// <remarks>
        /// <para>
        /// If further information about a specific table is desired,
        /// <see cref="GetTableAsync(string, GetTableOptions, CancellationToken)"/>
        /// API may be used.
        /// </para>
        /// <para>
        /// If a given identity has access to a large number of tables the
        /// list may be paged by either using
        /// <see cref="ListTablesOptions.FromIndex"/> and
        /// <see cref="ListTablesOptions.Limit"/> or by calling
        /// <see cref="GetListTablesAsyncEnumerable(ListTablesOptions, CancellationToken)"/>
        /// instead of this API.  The table names are returned as string list
        /// in <see cref="ListTablesResult"/> in alphabetical order to
        /// facilitate paging.
        /// </para>
        /// </remarks>
        /// <example>
        /// Listing all tables in a default compartment/tenancy.
        /// <code>
        ///     var result = await client.ListTablesAsync();
        ///     foreach(var tableName in result.TableNames)
        ///     {
        ///         Console.WriteLine(tableName);
        ///     }
        /// </code>
        /// </example>
        /// <param name="options">(Optional) Options for this operation, which
        /// allow specifying paging as well as other parameters.  If not
        /// specified or <c>null</c>, appropriate defaults will be
        /// used.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="ListTablesResult"/> object
        /// containing table names.</returns>
        /// <exception cref="ArgumentException">If <paramref name="options"/>
        /// contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The service is not in
        /// a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="ListTablesOptions"/>
        /// <seealso cref="ListTablesResult"/>
        public async Task<ListTablesResult> ListTablesAsync(
            ListTablesOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (ListTablesResult) await ExecuteRequestAsync(
                new ListTablesRequest(this, options),
                cancellationToken);
        }

        /// <summary>
        /// Lists tables, returning table names.
        /// </summary>
        /// <remarks>
        /// This API is a shorthand for
        /// <see cref="ListTablesAsync(ListTablesOptions, CancellationToken)"/>
        /// that takes <paramref name="compartmentOrNamespace"/> as the only
        /// option to specify either compartment name or path (Cloud Service
        /// only) or namespace (on-premise database only).
        /// </remarks>
        /// <example>
        /// Listing all tables in a given compartment.
        /// <code>
        ///     var result = await client.ListTablesAsync("my_compartment");
        ///     foreach(var tableName in result.TableNames)
        ///     {
        ///         Console.WriteLine(tableName);
        ///     }
        /// </code>
        /// </example>
        /// <param name="compartmentOrNamespace">
        /// For Cloud Service, pass either compartment id or compartment path
        /// (see remarks section for <see cref="NoSQLClient"/>).  For
        /// on-premise database, pass namespace name.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="ListTablesResult"/> object
        /// containing table names.</returns>
        /// <exception cref="ArgumentException">If
        /// <paramref name="compartmentOrNamespace"/> is missing or invalid.
        /// </exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The service is not in
        /// a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="ListTablesAsync(ListTablesOptions, CancellationToken)"/>
        public Task<ListTablesResult> ListTablesAsync(
            string compartmentOrNamespace,
            CancellationToken cancellationToken = default)
        {
            return ListTablesAsync(new ListTablesOptions
            {
                Namespace = compartmentOrNamespace
            }, cancellationToken);
        }

        /// <summary>
        /// Lists tables, returning table names.
        /// </summary>
        /// <remarks>
        /// This API is a shorthand for
        /// <see cref="ListTablesAsync(ListTablesOptions, CancellationToken)"/>.
        /// Use this overload when you need to provide
        /// <see cref="CancellationToken"/> but you don't need to provide
        /// <see cref="ListTablesOptions"/>.
        /// </remarks>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns>Task returning <see cref="ListTablesResult"/> object
        /// containing table names.</returns>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The service is not in
        /// a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="ListTablesAsync(ListTablesOptions, CancellationToken)"/>
        public Task<ListTablesResult> ListTablesAsync(
            CancellationToken cancellationToken)
        {
            return ListTablesAsync((ListTablesOptions)null,
                cancellationToken);
        }

        /// <summary>
        /// Returns <see cref="IAsyncEnumerable{T}"/> to list table names.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Use this API when a given identity has access to a large number of
        /// tables and you wish to page the results rather than returning the
        /// whole list at once.
        /// </para>
        /// <para>
        /// This API is similar to
        /// <see cref="ListTablesAsync(ListTablesOptions, CancellationToken)"/>
        /// but creates <see cref="IAsyncEnumerable{T}"/> that allows you to
        /// iterate over the results using <c>await foreach</c>
        /// construct.  Each of the results is <see cref="ListTablesResult"/>
        /// containing partial list of table names.  Note that you must
        /// specify <see cref="ListTablesOptions.Limit"/> which will set
        /// a limit on the number table names in each partial result
        /// (otherwise the first partial result will contain the whole list).
        /// The table names are returned in alphabetical order.
        /// </para>
        /// <para>
        /// Note that this method may only throw
        /// <see cref="ArgumentException"/>.  Other exceptions listed can only
        /// be thrown during the iteration process as per deferred execution
        /// semantics of enumerables.
        /// </para>
        /// </remarks>
        /// <example>
        /// Asynchronously paging and printing table names.
        /// <code>
        /// var options = new ListTablesOptions
        /// {
        ///     Compartment = "my_compartment",
        ///     Limit = 100
        /// };
        ///
        /// await foreach(var result in client.GetListTablesAsyncEnumerable(options))
        /// {
        ///     foreach(var tableName in result.TableNames)
        ///     {
        ///         Console.WriteLine(tableName);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="options">Options for this operation.  Specify
        /// <see cref="ListTablesOptions.Limit"/> parameter to enable paging
        /// as well as other options as needed.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// You may also use
        /// <see cref="TaskAsyncEnumerableExtensions.WithCancellation{T}"/>
        /// extension to pass the cancellation token to the resulting
        /// <see cref="IAsyncEnumerable{ListTablesResult}"/> instead of
        /// <paramref name="cancellationToken"/> parameter.</param>
        /// <returns>Async enumerable to iterate over
        /// <see cref="ListTablesResult"/> objects.</returns>
        /// <exception cref="ArgumentException">If <paramref name="options"/>
        /// contains invalid values.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The service is not in
        /// a valid state to perform this operation.</exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="ListTablesAsync(ListTablesOptions, CancellationToken)"/>
        /// <seealso cref="ListTablesOptions"/>
        /// <seealso cref="ListTablesResult"/>
        /// <seealso href="https://docs.microsoft.com/en-us/archive/msdn-magazine/2019/november/csharp-iterating-with-async-enumerables-in-csharp-8">
        /// Iterating with Async Enumerables in C#
        /// </seealso>
        public IAsyncEnumerable<ListTablesResult>
            GetListTablesAsyncEnumerable(
            ListTablesOptions options,
            CancellationToken cancellationToken = default)
        {
            return GetListTablesAsyncEnumerableWithOptions(
                options != null ? options.Clone() : new ListTablesOptions(),
                cancellationToken);
        }

        /// <summary>
        /// Returns <see cref="IAsyncEnumerable{T}"/> to list table names.
        /// </summary>
        /// <remarks>
        /// This API is a shorthand for
        /// <see cref="GetListTablesAsyncEnumerable(ListTablesOptions, CancellationToken)"/>
        /// that takes <paramref name="limit"/> and optional
        /// <paramref name="compartmentOrNamespace"/> to specify either
        /// compartment name or path (Cloud Service only) or namespace
        /// (on-premise NoSQL database only) and no other options are
        /// required.
        /// </remarks>
        /// <example>
        /// Asynchronously paging and printing table names.
        /// <code>
        /// await foreach(var result in client.GetListTablesAsyncEnumerable(100))
        /// {
        ///     foreach(var tableName in result.TableNames)
        ///     {
        ///         Console.WriteLine(tableName);
        ///     }
        /// }
        /// </code>
        /// </example>
        /// <param name="limit">Limit on number of tables in each partial
        /// result.  Equivalent to <see cref="ListTablesOptions.Limit"/>.
        /// Must be a positive value.
        /// </param>
        /// <param name="compartmentOrNamespace">(Optional) For Cloud Service,
        /// pass either compartment id or compartment path (see remarks
        /// section for <see cref="NoSQLClient"/>).  For on-premise NoSQL
        /// database, pass namespace name.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// You may also use
        /// <see cref="TaskAsyncEnumerableExtensions.WithCancellation{T}"/>
        /// extension to pass the cancellation token to the resulting
        /// <see cref="IAsyncEnumerable{ListTablesResult}"/> instead of
        /// <paramref name="cancellationToken"/> parameter.</param>
        /// <returns>Async enumerable to iterate over
        /// <see cref="ListTablesResult"/> objects.</returns>
        /// <exception cref="ArgumentException">If <paramref name="limit"/> or
        /// <paramref name="compartmentOrNamespace"/> are invalid.</exception>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The service is not in
        /// a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="GetListTablesAsyncEnumerable(ListTablesOptions, CancellationToken)"/>
        public IAsyncEnumerable<ListTablesResult>
            GetListTablesAsyncEnumerable(
                int limit,
                string compartmentOrNamespace = null,
                CancellationToken cancellationToken = default)
        {
            return GetListTablesAsyncEnumerableWithOptions(
                new ListTablesOptions
                {
                    Compartment = compartmentOrNamespace,
                    Limit = limit
                },
                cancellationToken);
        }

    }

}
