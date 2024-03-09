/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.Threading;
    using static ValidateUtils;

    /// <summary>
    /// Represents a Put or Delete operation that is part of
    /// <see cref="WriteOperationCollection"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The operations represented by this interface are part of
    /// <see cref="WriteOperationCollection"/> which is used as input to
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/> method.
    /// </para>
    /// <para>
    /// The operations are added to the collection using methods of
    /// <see cref="WriteOperationCollection"/>, so you don't need to be
    /// familiar with classes implementing this interface in order to use
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/> APIs.
    /// These interface and classes implementing it are for informational
    /// purpose only if you choose to iterate through
    /// <see cref="WriteOperationCollection"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="WriteOperationCollection"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/>
    public interface IWriteOperation
    {
        /// <summary>
        /// Gets the table name, if available.
        /// </summary>
        /// <remarks>
        /// Table name is required if this operation is used by
        /// <see cref="NoSQLClient.WriteManyAsync(WriteOperationCollection, WriteManyOptions, CancellationToken)"/>
        /// method that requires you to provide table name for each operation.
        /// If using
        /// <see cref="NoSQLClient.WriteManyAsync(string, WriteOperationCollection, WriteManyOptions, CancellationToken)"/>,
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.PutManyAsync*"/> or
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.DeleteManyAsync*"/>,
        /// table name should be provided as a parameter to these methods
        /// rather than per-operation and thus this property should be
        /// <c>null</c>.
        /// </remarks>
        /// <value>
        /// Table name if available, otherwise null.
        /// </value>
        string TableName { get; }

        /// <summary>
        /// Gets the value that determines whether to abort the transaction
        /// started by call to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/> if
        /// this operation fails.
        /// </summary>
        /// <value><c>true</c> to abort the transaction if this operation
        /// fails, otherwise <c>false</c>.
        /// </value>
        bool AbortIfUnsuccessful { get; }
    }

    /// <summary>
    /// Represents a Put operation that is part of
    /// <see cref="WriteOperationCollection"/>.
    /// </summary>
    /// <seealso cref="IWriteOperation"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.WriteOperationCollection.AddPut*"/>
    public class PutOperation : IWriteOperation, IPutOp
    {
        internal PutOperation(string tableName, object row,
            PutOptions options, bool abortIfUnsuccessful)
        {
            TableName = tableName;
            Row = row;
            Options = options;
            AbortIfUnsuccessful = abortIfUnsuccessful;
        }

        PutOpKind IPutOp.PutOpKind =>
            Options?.putOpKind ?? PutOpKind.Always;

        internal bool DoesReads =>
            ((IPutOp)this).PutOpKind != PutOpKind.Always;

        RowVersion IPutOp.MatchVersion => Options?.MatchVersion;

        bool IPutOp.ReturnExisting => Options?.ReturnExisting ?? false;

        /// <inheritdoc cref="IWriteOperation.AbortIfUnsuccessful"/>
        public bool AbortIfUnsuccessful { get; }

        /// <inheritdoc cref="IWriteOperation.TableName"/>
        public virtual string TableName { get; }

        /// <inheritdoc cref="PutRequest{TRow}.Row"/>
        public object Row { get; }

        /// <inheritdoc cref="PutRequest{TRow}.Options"/>
        public PutOptions Options { get; }

        internal virtual void Validate()
        {
            // TableName is validated in WriteManyRequest.
            CheckNotNull(Row, "row");
            ((IOptions)Options)?.Validate();
        }
    }

    /// <summary>
    /// Represents a PutIfAbsent operation that is part of
    /// <see cref="WriteOperationCollection"/>.
    /// </summary>
    /// <remarks>
    /// This operation puts a row into a table if there is no existing row
    /// that matches the primary key.
    /// </remarks>
    /// <seealso cref="IWriteOperation"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.WriteOperationCollection.AddPutIfAbsent*"/>
    public class PutIfAbsentOperation : PutOperation, IPutOp
    {
        internal PutIfAbsentOperation(string tableName, object row,
            PutOptions options, bool abortIfUnsuccessful) :
            base(tableName, row, options, abortIfUnsuccessful)
        {
        }

        PutOpKind IPutOp.PutOpKind => PutOpKind.IfAbsent;
    }
    
    /// <summary>
    /// Represents a PutIfPresent operation that is part of
    /// <see cref="WriteOperationCollection"/>.
    /// </summary>
    /// <remarks>
    /// This operation puts a row into a table if there is an existing row
    /// that matches the primary key.
    /// </remarks>
    /// <seealso cref="IWriteOperation"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.WriteOperationCollection.AddPutIfPresent*"/>
    public class PutIfPresentOperation : PutOperation, IPutOp
    {
        internal PutIfPresentOperation(string tableName, object row,
            PutOptions options, bool abortIfUnsuccessful) :
            base(tableName, row, options, abortIfUnsuccessful)
        {
        }

        PutOpKind IPutOp.PutOpKind => PutOpKind.IfPresent;
    }

    /// <summary>
    /// Represents a PutIfVersion operation that is part of
    /// <see cref="WriteOperationCollection"/>.
    /// </summary>
    /// <remarks>
    /// This operation puts a row into a table if there is an existing row
    /// that matches the primary key and its <see cref="RowVersion"/> matches
    /// the provided value.
    /// </remarks>
    /// <seealso cref="IWriteOperation"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.WriteOperationCollection.AddPutIfVersion*"/>
    public class PutIfVersionOperation : PutOperation, IPutOp
    {
        internal PutIfVersionOperation(string tableName, object row,
            RowVersion matchVersion, PutOptions options,
            bool abortIfUnsuccessful) :
            base(tableName, row, options, abortIfUnsuccessful)
        {
            MatchVersion = matchVersion;
        }

        PutOpKind IPutOp.PutOpKind => PutOpKind.IfVersion;

        /// <inheritdoc cref="PutIfVersionRequest{TRow}.MatchVersion"/>
        public RowVersion MatchVersion { get; }

        internal override void Validate()
        {
            base.Validate();
            CheckNotNull(MatchVersion, "matchVersion");
        }
    }

    /// <summary>
    /// Represents a Delete operation that is part of
    /// <see cref="WriteOperationCollection"/>.
    /// </summary>
    /// <seealso cref="IWriteOperation"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.WriteOperationCollection.AddDelete*"/>
    public class DeleteOperation : IWriteOperation, IDeleteOp
    {
        internal DeleteOperation(string tableName, object primaryKey,
            DeleteOptions options, bool abortIfUnsuccessful)
        {
            TableName = tableName;
            PrimaryKey = primaryKey;
            Options = options;
            AbortIfUnsuccessful = abortIfUnsuccessful;
        }

        bool IDeleteOp.ReturnExisting => Options?.ReturnExisting ?? false;

        RowVersion IDeleteOp.MatchVersion => Options?.MatchVersion;

        /// <inheritdoc cref="IWriteOperation.AbortIfUnsuccessful"/>
        public bool AbortIfUnsuccessful { get; }

        /// <inheritdoc cref="IWriteOperation.TableName"/>
        public virtual string TableName { get; }

        /// <inheritdoc cref="DeleteRequest{TRow}.PrimaryKey"/>
        public object PrimaryKey { get; }

        /// <inheritdoc cref="DeleteRequest{TRow}.Options"/>
        public DeleteOptions Options { get; }

        internal virtual void Validate()
        {
            CheckNotNull(PrimaryKey, "primaryKey");
            ((IOptions)Options)?.Validate();
        }
    }

    /// <summary>
    /// Represents a DeleteIfVersion operation that is part of
    /// <see cref="WriteOperationCollection"/>.
    /// </summary>
    /// <remarks>
    /// This operation deletes a row from a table if there is an existing row
    /// that matches the primary key and its <see cref="RowVersion"/> matches
    /// the provided value.
    /// </remarks>
    /// <seealso cref="IWriteOperation"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.WriteOperationCollection.AddDeleteIfVersion*"/>
    public class DeleteIfVersionOperation : DeleteOperation, IDeleteOp
    {
        internal DeleteIfVersionOperation(string tableName, object primaryKey,
            RowVersion matchVersion, DeleteOptions options,
            bool abortIfUnsuccessful) :
            base(tableName, primaryKey, options, abortIfUnsuccessful)
        {
            MatchVersion = matchVersion;
        }

        /// <inheritdoc cref="DeleteIfVersionRequest{TRow}.MatchVersion"/>
        public RowVersion MatchVersion { get; }

        internal override void Validate()
        {
            // TableName is validated in WriteManyRequest.
            base.Validate();
            CheckNotNull(MatchVersion, "matchVersion");
        }
    }

}
