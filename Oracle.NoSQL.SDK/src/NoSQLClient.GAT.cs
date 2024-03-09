/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using static ValidateUtils;

    public partial class NoSQLClient
    {
        /// <summary>
        /// Cloud Service only.
        /// Adds replica to a table.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This operation adds replica to a Global Active table. If performed
        /// on a regular table (singleton), it will be converted to Global
        /// Active table, provided that the singleton table schema conforms to
        /// certain restrictions. For more information, see
        /// <see href="https://docs.oracle.com/en/cloud/paas/nosql-cloud/gasnd">
        /// Global Active Tables in NDCS
        /// </see>.
        /// </para>
        /// <para>
        /// Note that <see cref="TableLimits"/> for the replica table will
        /// default to the table limits for the existing table, however you
        /// can override the values of <see cref="TableLimits.ReadUnits"/>
        /// and <see cref="TableLimits.WriteUnits"/> for the replica by using
        /// options <see cref="AddReplicaOptions.ReadUnits"/> and
        /// <see cref="AddReplicaOptions.WriteUnits"/>. The storage capacity
        /// of the replica will always be the same as that of the existing
        /// table.
        /// </para>
        /// <para>
        /// As with
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>,
        /// the result returned from this API does not imply operation
        /// completion. Same considerations as described in
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>
        /// about long-running operations apply here, including the need to
        /// use <see cref="TableResult.WaitForCompletionAsync"/> to
        /// asynchronously wait for operation completion.
        /// </para>
        /// <para>
        /// Note that even after this operation is completed (as described
        /// above), the replica table in the receiver region may still be in
        /// the process of being initialized with the data from the sender
        /// region, during which time the data operations on the replica table
        /// will fail with <see cref="TableNotReadyException"/>.
        /// </para>
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="region">Region where to add the replica.</param>
        /// <param name="options">(Optional) Options for the operation. If not
        /// specified or <c>null</c>, appropriate defaults will be used.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="AddReplicaOptions"/>
        /// <seealso cref="TableResult"/>
        /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>
        public Task<TableResult> AddReplicaAsync(
            string tableName,
            Region region,
            AddReplicaOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return AddReplicaAsync(tableName, region?.RegionId, options,
                cancellationToken);
        }

        /// <summary>
        /// Cloud Service only.
        /// Adds replica to a table.
        /// </summary>
        /// <remarks>
        /// This API is equivalent to
        /// <see cref="AddReplicaAsync(string,Region,AddReplicaOptions,CancellationToken)"/>
        /// except that it takes the region id as <paramref name="regionId"/>
        /// parameter instead of <see cref="Region"/> instance. E.g.
        /// "ap-mumbai-1", "us-ashburn-1", etc.
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="regionId">Region id of the region where to add the
        /// replica.</param>
        /// <param name="options">(Optional) Options for the operation. If not
        /// specified or <c>null</c>, appropriate defaults will be used.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="AddReplicaAsync(string,Region,AddReplicaOptions,CancellationToken)"/>
        /// <seealso cref="Region.RegionId"/>
        public async Task<TableResult> AddReplicaAsync(
            string tableName,
            string regionId,
            AddReplicaOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (TableResult)await ExecuteRequestAsync(
                new AddReplicaRequest(this, tableName, regionId, options),
                cancellationToken);
        }

        /// <summary>
        /// Cloud Service only.
        /// Adds replica to a table and waits asynchronously for the operation
        /// completion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is equivalent to calling
        /// <see cref="AddReplicaAsync(string,Region,AddReplicaOptions,CancellationToken)"/>
        /// and then calling
        /// <see cref="TableResult.WaitForCompletionAsync"/> on the returned
        /// <see cref="TableResult"/> object.  If the operation is successful,
        /// the table state in the resulting <see cref="TableResult"/> object
        /// should be <see cref="TableState.Active"/>.
        /// </para>
        /// <para>
        /// For this operation, <see cref="AddReplicaOptions.Timeout"/> covers
        /// the total time interval including waiting for the DDL operation
        /// completion. If not specified, separate default timeouts are used
        /// for issuing the operation and waiting for its completion, with
        /// values of <see cref="NoSQLConfig.TableDDLTimeout"/> and
        /// <see cref="NoSQLConfig.TablePollTimeout"/> correspondingly (the
        /// latter defaults to no timeout if
        /// <see cref="NoSQLConfig.TablePollTimeout"/> is not set).
        /// Note that as with <see cref="TableResult.WaitForCompletionAsync"/>
        /// you may specify poll delay as
        /// <see cref="AddReplicaOptions.PollDelay"/> which otherwise defaults
        /// to <see cref="NoSQLConfig.TablePollDelay"/>.
        /// </para>
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="region">Region where to add the replica.</param>
        /// <param name="options">(Optional) Options for the operation. If not
        /// specified or <c>null</c>, appropriate defaults will be used.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="AddReplicaAsync(string,Region,AddReplicaOptions,CancellationToken)"/>
        /// <seealso cref="TableResult.WaitForCompletionAsync"/>
        public Task<TableResult> AddReplicaWithCompletionAsync(
            string tableName,
            Region region,
            AddReplicaOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return AddReplicaWithCompletionAsync(tableName, region?.RegionId,
                options, cancellationToken);
        }

        /// <summary>
        /// Cloud Service only.
        /// Adds replica to a table and waits asynchronously for the operation
        /// completion.
        /// </summary>
        /// <remarks>
        /// This API is equivalent to
        /// <see cref="AddReplicaWithCompletionAsync(string,Region,AddReplicaOptions,CancellationToken)"/>
        /// except that it takes the region id as <paramref name="regionId"/>
        /// parameter instead of <see cref="Region"/> instance. E.g.
        /// "ap-mumbai-1", "us-ashburn-1", etc.
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="regionId">Region id of the region where to add the
        /// replica.</param>
        /// <param name="options">(Optional) Options for the operation. If not
        /// specified or <c>null</c>, appropriate defaults will be used.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="AddReplicaWithCompletionAsync(string,Region,AddReplicaOptions,CancellationToken)"/>
        /// <seealso cref="Region.RegionId"/>
        public Task<TableResult> AddReplicaWithCompletionAsync(
            string tableName,
            string regionId,
            AddReplicaOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return DoTableOpWithCompletionAsync(
                new AddReplicaRequest(this, tableName, regionId, options),
                cancellationToken);
        }

        /// <summary>
        /// Cloud Service only.
        /// Drops replica from a table.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This operation drops replica from a Global Active table. For more
        /// information, see
        /// <see href="https://docs.oracle.com/en/cloud/paas/nosql-cloud/gasnd">
        /// Global Active Tables in NDCS
        /// </see>.
        /// </para>
        /// <para>
        /// As with
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>,
        /// the result returned from this API does not imply operation
        /// completion. Same considerations as described in
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>
        /// about long-running operations apply here, including the need to
        /// use <see cref="TableResult.WaitForCompletionAsync"/> to
        /// asynchronously wait for operation completion.
        /// </para>
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="region">Region from where to drop the replica.
        /// </param>
        /// <param name="options">(Optional) Options for the operation. If not
        /// specified or <c>null</c>, appropriate defaults will be used.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="DropReplicaOptions"/>
        /// <seealso cref="TableResult"/>
        /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>
        public Task<TableResult> DropReplicaAsync(
            string tableName,
            Region region,
            DropReplicaOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return DropReplicaAsync(tableName, region?.RegionId, options,
                cancellationToken);
        }

        /// <summary>
        /// Cloud Service only.
        /// Drops replica from a table.
        /// </summary>
        /// <remarks>
        /// This API is equivalent to
        /// <see cref="DropReplicaAsync(string,Region,DropReplicaOptions,CancellationToken)"/>
        /// except that it takes the region id as <paramref name="regionId"/>
        /// parameter instead of <see cref="Region"/> instance. E.g.
        /// "ap-mumbai-1", "us-ashburn-1", etc.
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="regionId">Region id of the region from where to drop
        /// the replica.</param>
        /// <param name="options">(Optional) Options for the operation. If not
        /// specified or <c>null</c>, appropriate defaults will be used.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="DropReplicaAsync(string,Region,DropReplicaOptions,CancellationToken)"/>
        /// <seealso cref="Region.RegionId"/>
        public async Task<TableResult> DropReplicaAsync(
            string tableName,
            string regionId,
            DropReplicaOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (TableResult)await ExecuteRequestAsync(
                new DropReplicaRequest(this, tableName, regionId, options),
                cancellationToken);
        }

        /// <summary>
        /// Cloud Service only.
        /// Drops replica from a table and waits asynchronously for the
        /// operation completion.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is equivalent to calling
        /// <see cref="DropReplicaAsync(string,Region,DropReplicaOptions,CancellationToken)"/>
        /// and then calling
        /// <see cref="TableResult.WaitForCompletionAsync"/> on the returned
        /// <see cref="TableResult"/> object.  If the operation is successful,
        /// the table state in the resulting <see cref="TableResult"/> object
        /// should be <see cref="TableState.Active"/>.
        /// </para>
        /// <para>
        /// For this operation, <see cref="DropReplicaOptions.Timeout"/> covers
        /// the total time interval including waiting for the DDL operation
        /// completion. If not specified, separate default timeouts are used
        /// for issuing the operation and waiting for its completion, with
        /// values of <see cref="NoSQLConfig.TableDDLTimeout"/> and
        /// <see cref="NoSQLConfig.TablePollTimeout"/> correspondingly (the
        /// latter defaults to no timeout if
        /// <see cref="NoSQLConfig.TablePollTimeout"/> is not set).
        /// Note that as with <see cref="TableResult.WaitForCompletionAsync"/>
        /// you may specify poll delay as
        /// <see cref="DropReplicaOptions.PollDelay"/> which otherwise defaults
        /// to <see cref="NoSQLConfig.TablePollDelay"/>.
        /// </para>
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="region">Region from where to drop the replica.
        /// </param>
        /// <param name="options">(Optional) Options for the operation. If not
        /// specified or <c>null</c>, appropriate defaults will be used.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="DropReplicaAsync(string,Region,DropReplicaOptions,CancellationToken)"/>
        /// <seealso cref="TableResult.WaitForCompletionAsync"/>
        public Task<TableResult> DropReplicaWithCompletionAsync(
            string tableName,
            Region region,
            DropReplicaOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return DropReplicaWithCompletionAsync(tableName, region?.RegionId,
                options, cancellationToken);
        }

        /// <summary>
        /// Cloud Service only.
        /// Drops replica from a table and waits asynchronously for the
        /// operation completion.
        /// </summary>
        /// <remarks>
        /// This API is equivalent to
        /// <see cref="DropReplicaWithCompletionAsync(string,Region,DropReplicaOptions,CancellationToken)"/>
        /// except that it takes the region id as <paramref name="regionId"/>
        /// parameter instead of <see cref="Region"/> instance. E.g.
        /// "ap-mumbai-1", "us-ashburn-1", etc.
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="regionId">Region id of the region where to add the
        /// replica.</param>
        /// <param name="options">(Optional) Options for the operation. If not
        /// specified or <c>null</c>, appropriate defaults will be used.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="DropReplicaWithCompletionAsync(string,Region,DropReplicaOptions,CancellationToken)"/>
        /// <seealso cref="Region.RegionId"/>
        public Task<TableResult> DropReplicaWithCompletionAsync(
            string tableName,
            string regionId,
            DropReplicaOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return DoTableOpWithCompletionAsync(
                new DropReplicaRequest(this, tableName, regionId, options),
                cancellationToken);
        }

        /// <summary>
        /// Cloud Service only.
        /// Waits asynchronously for local table replica to complete its
        /// initialization.
        /// </summary>
        /// <remarks>
        /// <para>
        /// After table replica is created, it needs to be initialized by
        /// copying the data (if any) from the sender region. During this
        /// initialization process, even though the table state of the replica
        /// table is <see cref="TableState.Active"/>, data operations cannot
        /// be performed on the replica table.
        /// </para>
        /// <para>
        /// This method is used to ensure that the replica table is ready for
        /// data operations by asynchronously waiting for the initialization
        /// process to complete. It works similar to
        /// <see cref="TableResult.WaitForCompletionAsync"/> and
        /// <see cref="WaitForTableStateAsync"/> by polling the table state at
        /// regular intervals until
        /// <see cref="TableResult.IsLocalReplicaInitialized"/> is <c>true</c>.
        /// </para>
        /// <para>
        /// Note that this operation must be performed in the receiver region
        /// where the table replica resides (not in the sender region from
        /// where the replica was created), meaning that this
        /// <see cref="NoSQLClient"/> instance must be configured with the
        /// receiver region (see <see cref="NoSQLConfig.Region"/>).
        /// </para>
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="timeout">
        /// <inheritdoc cref="TableResult.WaitForCompletionAsync" select="param[@name='timeout']"/>
        /// </param>
        /// <param name="pollDelay">
        /// <inheritdoc cref="TableResult.WaitForCompletionAsync" select="param[@name='pollDelay']"/>
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso href="https://docs.oracle.com/en/cloud/paas/nosql-cloud/gasnd">
        /// Global Active Tables in NDCS
        /// </seealso>.
        /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.AddReplicaAsync*"/>
        /// <seealso cref="TableResult.IsLocalReplicaInitialized"/>
        /// <seealso cref="TableResult.WaitForCompletionAsync"/>
        /// <seealso cref="WaitForTableStateAsync"/>
        public Task<TableResult> WaitForLocalReplicaInitAsync(
            string tableName,
            TimeSpan? timeout = null,
            TimeSpan? pollDelay = null,
            CancellationToken cancellationToken = default)
        {
            return WaitForLocalReplicaInitAsync(tableName,
                new TableCompletionOptions
                {
                    Timeout = timeout,
                    PollDelay = pollDelay
                }, cancellationToken);
        }

        /// <summary>
        /// Cloud Service only.
        /// Waits asynchronously for local table replica to complete its
        /// initialization.
        /// </summary>
        /// <remarks>
        /// This method is equivalent to
        /// <see cref="WaitForLocalReplicaInitAsync(string,TimeSpan?,TimeSpan?,CancellationToken)"/>
        /// except that it takes <see cref="TableCompletionOptions"/>
        /// parameter (thus allowing to specify
        /// <see cref="GetTableOptions.Compartment"/> in addition to
        /// <see cref="GetTableOptions.Timeout"/> and
        /// <see cref="TableCompletionOptions.PollDelay"/>).
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="options">Options for the operation.</param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="TableResult"/>.</returns>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="WaitForLocalReplicaInitAsync(string,TimeSpan?,TimeSpan?,CancellationToken)"/>
        /// <seealso cref="TableCompletionOptions"/>
        public async Task<TableResult> WaitForLocalReplicaInitAsync(
            string tableName,
            TableCompletionOptions options,
            CancellationToken cancellationToken = default)
        {
            if (Config.ServiceType != ServiceType.Cloud)
            {
                throw new NotSupportedException(
                    "WaitForLocalReplicaInitAsync is not supported for " +
                    $"service type {Config.ServiceType} " +
                    "(requires Cloud Service)");
            }

            CheckTableName(tableName);
            CheckPollParameters(options?.Timeout, options?.PollDelay,
                nameof(options.Timeout), nameof(options.PollDelay));

            var tableResult = new TableResult
            {
                TableName = tableName
            };

            await WaitForTableStateInternalAsync(tableResult,
                result => result.IsLocalReplicaInitialized &&
                          result.TableState == TableState.Active,
                "local replica initialization", options?.Timeout,
                options?.PollDelay, cancellationToken);

            return tableResult;
        }

        /// <summary>
        /// Cloud Service only.
        /// Gets replica statistics information.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This operation retrieves stats information for the replicas of a
        /// Global Active table.This information includes a time series of
        /// replica stats, as found in <see cref="ReplicaStatsRecord"/>. For
        /// more information on Global Active tables, see
        /// <see href="https://docs.oracle.com/en/cloud/paas/nosql-cloud/gasnd">
        /// Global Active Tables in NDCS
        /// </see>.
        /// </para>
        /// <para>
        /// It is possible to return a range of stats records or, by default,
        /// only the most recent stats records (up to the limit) for each
        /// replica if <see cref="GetReplicaStatsOptions.StartTime"/> is not
        /// specified. Replica stats records are created on a regular basis
        /// and maintained for a period of time. Only records for time periods
        /// that have completed are returned so that a user never sees
        /// changing data for a specific range.
        /// </para>
        /// <para>
        /// This API returns stats for all replicas as a dictionary keyed by
        /// region id of each replica and values being a list of
        /// <see cref="ReplicaStatsRecord"/> instances for that
        /// replica (see <see cref="ReplicaStatsResult.StatsRecords"/>). You
        /// may limit the result to the stats of only one replica by using
        /// an overload that takes a region parameter (see
        /// <see cref="GetReplicaStatsAsync(string,Region,GetReplicaStatsOptions,CancellationToken)"/>
        /// and
        /// <see cref="GetReplicaStatsAsync(string,string,GetReplicaStatsOptions,CancellationToken)"/>).
        /// </para>
        /// <para>
        /// Because the number of replica stats records can be very large,
        /// each call to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetReplicaStatsAsync*"/>
        /// returns a limited number of records (the default limit is 1000).
        /// You can customize this limit via
        /// <see cref="GetReplicaStatsOptions.Limit"/> option. You can retrieve
        /// large number of replica stats records over multiple calls to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetReplicaStatsAsync*"/>
        /// by setting <see cref="GetReplicaStatsOptions.StartTime"/> on each
        /// subsequent call to the value of
        /// <see cref="ReplicaStatsResult.NextStartTime"/> returned by a
        /// previous call.
        /// </para>
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="options">(Optional) Options for the operation.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="ReplicaStatsResult"/>.
        /// </returns>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="GetReplicaStatsOptions"/>
        /// <seealso cref="ReplicaStatsResult"/>
        /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.AddReplicaAsync*"/>
        public Task<ReplicaStatsResult> GetReplicaStatsAsync(
            string tableName,
            GetReplicaStatsOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return GetReplicaStatsAsync(tableName, (string)null, options,
                cancellationToken);
        }

        /// <summary>
        /// Cloud Service only.
        /// Gets replica statistics information.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This API is similar to
        /// <see cref="GetReplicaStatsAsync(string,GetReplicaStatsOptions,CancellationToken)"/>
        /// but allows to pass a <paramref name="region"/> parameter to only
        /// get replica stats for that region.
        /// </para>
        /// <para>
        /// Note that the stats are returned in the same format as for
        /// <see cref="GetReplicaStatsAsync(string,GetReplicaStatsOptions,CancellationToken)"/>,
        /// in this case <see cref="ReplicaStatsResult.StatsRecords"/> being a
        /// dictionary with one key for the specified region. If
        /// <paramref name="region"/> is <c>null</c>, this API will behave
        /// identically to
        /// <see cref="GetReplicaStatsAsync(string,GetReplicaStatsOptions,CancellationToken)"/>
        /// and stats for all regions will be returned.
        /// </para>
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="region">Region from which to query replica
        /// stats information.</param>
        /// <param name="options">(Optional) Options for the operation.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="ReplicaStatsResult"/>.
        /// </returns>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="GetReplicaStatsAsync(string,GetReplicaStatsOptions,CancellationToken)"/>
        public Task<ReplicaStatsResult> GetReplicaStatsAsync(
            string tableName,
            Region region,
            GetReplicaStatsOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return GetReplicaStatsAsync(tableName, region?.RegionId, options,
                cancellationToken);
        }

        /// <summary>
        /// Cloud Service only.
        /// Gets replica statistics information.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This API is similar to
        /// <see cref="GetReplicaStatsAsync(string,GetReplicaStatsOptions,CancellationToken)"/>
        /// but allows to pass a <paramref name="regionId"/> parameter to only
        /// get replica stats for that region.
        /// </para>
        /// <para>
        /// Note that the stats are returned in the same format as for
        /// <see cref="GetReplicaStatsAsync(string,GetReplicaStatsOptions,CancellationToken)"/>,
        /// in this case <see cref="ReplicaStatsResult.StatsRecords"/> being a
        /// dictionary with one key for the specified region. If
        /// <paramref name="regionId"/> is <c>null</c>, this API will behave
        /// identically to
        /// <see cref="GetReplicaStatsAsync(string,GetReplicaStatsOptions,CancellationToken)"/>
        /// and stats for all regions will be returned.
        /// </para>
        /// </remarks>
        /// <param name="tableName">Name of the table.</param>
        /// <param name="regionId">Region id of the region from which to query
        /// replica stats.</param>
        /// <param name="options">(Optional) Options for the operation.
        /// </param>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning <see cref="ReplicaStatsResult"/>.
        /// </returns>
        /// <exception cref="TimeoutException">Operation has timed out.
        /// </exception>
        /// <exception cref="InvalidOperationException">The table or the
        /// service is not in a valid state to perform this operation.
        /// </exception>
        /// <exception cref="NoSQLException"><see cref="NoSQLException"/> or
        /// one of its subclasses is thrown if operation cannot be performed
        /// for any other reason.  See documentation for corresponding
        /// subclass of <see cref="NoSQLException"/>.</exception>
        /// <seealso cref="GetReplicaStatsAsync(string,GetReplicaStatsOptions,CancellationToken)"/>
        public async Task<ReplicaStatsResult> GetReplicaStatsAsync(
            string tableName,
            string regionId,
            GetReplicaStatsOptions options = null,
            CancellationToken cancellationToken = default)
        {
            return (ReplicaStatsResult)await ExecuteRequestAsync(
                new GetReplicaStatsRequest(this, tableName, regionId,
                    options),
                cancellationToken);
        }

    }
}
