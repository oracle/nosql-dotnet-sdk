/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using static ValidateUtils;

    /// <summary>
    /// Represents a Put or Delete operation that is part of
    /// <see cref="WriteOperationCollection"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The operations represented by this interface are part of
    /// <see cref="WriteOperationCollection"/> which is used as input to
    /// <see cref="NoSQLClient.WriteManyAsync"/> method.
    /// </para>
    /// <para>
    /// The operations are added to the collection using methods of
    /// <see cref="WriteOperationCollection"/>, so you don't need to be
    /// familiar with classes implementing this interface in order to use
    /// <see cref="NoSQLClient.WriteManyAsync"/> API.  These interface and
    /// classes implementing it are for informational purpose only if you
    /// choose to iterate through <see cref="WriteOperationCollection"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="WriteOperationCollection"/>
    /// <seealso cref="NoSQLClient.WriteManyAsync"/>
    public interface IWriteOperation
    {
        /// <summary>
        /// Gets the value that determines whether to abort the transaction
        /// started by call to <see cref="NoSQLClient.WriteManyAsync"/> if
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
    /// <seealso cref="WriteOperationCollection.AddPut"/>
    public class PutOperation : IWriteOperation, IPutOp
    {
        internal PutOperation(object row, PutOptions options,
            bool abortIfUnsuccessful)
        {
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

        /// <inheritdoc cref="PutRequest{TRow}.Row"/>
        public object Row { get; }

        /// <inheritdoc cref="PutRequest{TRow}.Options"/>
        public PutOptions Options { get; }

        internal virtual void Validate()
        {
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
    /// <seealso cref="WriteOperationCollection.AddPutIfAbsent"/>
    public class PutIfAbsentOperation : PutOperation, IPutOp
    {
        internal PutIfAbsentOperation(object row, PutOptions options,
            bool abortIfUnsuccessful) : base(row, options, abortIfUnsuccessful)
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
    /// <seealso cref="WriteOperationCollection.AddPutIfPresent"/>
    public class PutIfPresentOperation : PutOperation, IPutOp
    {
        internal PutIfPresentOperation(object row, PutOptions options,
            bool abortIfUnsuccessful) : base(row, options, abortIfUnsuccessful)
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
    /// <seealso cref="WriteOperationCollection.AddPutIfVersion"/>
    public class PutIfVersionOperation : PutOperation, IPutOp
    {
        internal PutIfVersionOperation(object row, RowVersion matchVersion,
            PutOptions options, bool abortIfUnsuccessful) :
            base(row, options, abortIfUnsuccessful)
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
    /// <seealso cref="WriteOperationCollection.AddDelete"/>
    public class DeleteOperation : IWriteOperation, IDeleteOp
    {
        internal DeleteOperation(object primaryKey, DeleteOptions options,
            bool abortIfUnsuccessful)
        {
            PrimaryKey = primaryKey;
            Options = options;
            AbortIfUnsuccessful = abortIfUnsuccessful;
        }

        bool IDeleteOp.ReturnExisting => Options?.ReturnExisting ?? false;

        RowVersion IDeleteOp.MatchVersion => Options?.MatchVersion;

        /// <inheritdoc cref="IWriteOperation.AbortIfUnsuccessful"/>
        public bool AbortIfUnsuccessful { get; }

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
    /// <seealso cref="WriteOperationCollection.AddDeleteIfVersion"/>
    public class DeleteIfVersionOperation : DeleteOperation, IDeleteOp
    {
        internal DeleteIfVersionOperation(object primaryKey,
            RowVersion matchVersion, DeleteOptions options,
            bool abortIfUnsuccessful) :
            base(primaryKey, options, abortIfUnsuccessful)
        {
            MatchVersion = matchVersion;
        }

        /// <inheritdoc cref="DeleteIfVersionRequest{TRow}.MatchVersion"/>
        public RowVersion MatchVersion { get; }

        internal override void Validate()
        {
            base.Validate();
            CheckNotNull(MatchVersion, "matchVersion");
        }
    }

}
