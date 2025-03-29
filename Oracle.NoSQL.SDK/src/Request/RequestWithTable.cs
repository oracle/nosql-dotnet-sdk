/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using static ValidateUtils;

    /// <summary>
    /// Base class for information about all operations on a specific table.
    /// </summary>
    /// <seealso cref="Request" />
    public abstract class RequestWithTable : Request
    {
        internal RequestWithTable(NoSQLClient client, string tableName) :
            base(client)
        {
            TableName = tableName;
        }
        
        private protected virtual bool RequiresTableName => true;
        
        /// <summary>
        /// Gets the name of the table, if available for the request.
        /// </summary>
        /// <remarks>
        /// This value is <c>null</c> for instances of
        /// <see cref="TableDDLRequest"/> (but not its subclasses) that
        /// represent DDL operation as SQL statement (see
        /// <see cref="TableDDLRequest.Statement"/>).
        /// </remarks>
        /// <value>
        /// Table name if available, otherwise <c>null</c>.
        /// </value>
        public string TableName { get; }

        internal override string InternalTableName => TableName;

        internal override void Validate()
        {
            base.Validate();
            if (RequiresTableName || TableName != null)
            {
                CheckTableName(TableName);
            }
        }
    }

    /// <summary>
    /// Base class for information about all operations that read a row from a
    /// table.
    /// </summary>
    /// <remarks>
    /// Used as based class for <see cref="GetRequest{TRow}"/>.
    /// </remarks>
    /// <seealso cref="RequestWithTable"/>
    /// <seealso cref="GetRequest{TRow}"/>
    public abstract class ReadRequest : RequestWithTable
    {
        internal ReadRequest(NoSQLClient client, string tableName) :
            base(client, tableName)
        {
        }

        internal abstract IReadOptions ReadOptions { get; }

        internal override IOptions BaseOptions => ReadOptions;

        internal Consistency Consistency =>
            ReadOptions?.Consistency ?? Config.Consistency;

        internal override bool SupportsRateLimiting => true;

        internal override bool DoesReads => true;
    }

    /// <summary>
    /// Base class for information about all operations that write or delete a
    /// row from a table.
    /// </summary>
    /// <remarks>
    /// Used as base class for <see cref="PutRequest{TRow}"/> and
    /// <see cref="DeleteRequest{TRow}"/>.
    /// </remarks>
    /// <seealso cref="RequestWithTable"/>
    /// <seealso cref="PutRequest{TRow}"/>
    /// <seealso cref="DeleteRequest{TRow}"/>
    public abstract class WriteRequest : RequestWithTable
    {
        internal WriteRequest(NoSQLClient client, string tableName) :
            base(client, tableName)
        {
        }

        internal abstract IWriteOptions WriteOptions { get; }

        internal override IOptions BaseOptions => WriteOptions;

        internal bool ReturnExisting => WriteOptions?.ReturnExisting ?? false;

        internal Durability? Durability => WriteOptions?.Durability;

        internal override bool SupportsRateLimiting => true;

        internal override bool DoesWrites => true;
    }

    /// <summary>
    /// Base class for table-level operations such as table DDL,
    /// setting table limits, adding and dropping replicas, etc.
    /// </summary>
    /// <remarks>
    /// These operations return result of type <see cref="TableResult"/>. In
    /// addition, these are potentially-long running operations and may
    /// require to call <see cref="TableResult.WaitForCompletionAsync"/> to
    /// wait for operation completion. For more information, see API
    /// documentation of corresponding methods of <see cref="NoSQLClient"/>.
    /// </remarks>
    public abstract class TableOperationRequest : RequestWithTable
    {
        internal TableOperationRequest(NoSQLClient client, string tableName) :
            base(client, tableName)
        {
        }

        internal abstract ITableCompletionOptions CompletionOptions { get; }

        internal override TimeSpan GetDefaultTimeout() =>
            Config.TableDDLTimeout;

        internal override void ApplyResult(object result)
        {
            base.ApplyResult(result);
            Client.RateLimitingHandler?.ApplyTableResult((TableResult)result);
        }

    }
}
