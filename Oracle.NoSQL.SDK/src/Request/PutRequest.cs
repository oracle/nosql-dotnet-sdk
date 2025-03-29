/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.IO;
    using static ValidateUtils;

    // This interface is used to unify serialization implementation between
    // Put and WriteMany APIs
    internal interface IPutOp
    {
        PutOpKind PutOpKind { get; }

        object Row { get; }

        PutOptions Options { get; }

        RowVersion MatchVersion { get; }

        bool ReturnExisting { get; }
    }

    /// <summary>
    /// Represents information about Put operation performed by
    /// <see cref="NoSQLClient.PutAsync"/> API.
    /// </summary>
    /// <typeparam name="TRow">The type of value representing the row to put.
    /// Must be a reference type.  Currently the only supported type is
    /// <see cref="RecordValue"/>.</typeparam>
    /// <seealso cref="NoSQLClient.PutAsync"/>
    /// <seealso cref="Request"/>
    /// <seealso cref="ReadRequest" />
    public class PutRequest<TRow> : WriteRequest, IPutOp
    {
        internal PutRequest(NoSQLClient client, string tableName, object row,
            PutOptions options) : base(client, tableName)
        {
            Row = row;
            Options = options;
        }

        PutOpKind IPutOp.PutOpKind =>
            Options?.putOpKind ?? PutOpKind.Always;

        RowVersion IPutOp.MatchVersion => Options?.MatchVersion;

        internal override IWriteOptions WriteOptions => Options;

        bool IPutOp.ReturnExisting => ReturnExisting;

        internal override bool DoesReads =>
            ((IPutOp)this).PutOpKind != PutOpKind.Always;

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializePut(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializePut(stream, this);
        }

        internal override void Validate()
        {
            base.Validate();
            CheckNotNull(Row, nameof(Row));
        }

        /// <summary>
        /// Gets the value of the row for the Put operation.
        /// </summary>
        /// <value>
        /// The value of the row as <c>object</c>. Currently, its runtime type
        /// would only be <see cref="MapValue"/> or its subclasses.
        /// </value>
        public object Row { get; }

        /// <summary>
        /// Gets the options for the Put operation.
        /// </summary>
        /// <value>
        /// The options or <c>null</c> if options were not provided.
        /// </value>
        public PutOptions Options { get; }
    }

    /// <summary>
    /// Represents information about Put operation performed by
    /// <see cref="NoSQLClient.PutIfAbsentAsync"/> API.
    /// </summary>
    /// <typeparam name="TRow">The type of value representing the row
    /// optionally returned by <see cref="PutResult{TRow}.ExistingRow"/>.
    /// Must be a reference type.  Currently the only supported type is
    /// <see cref="RecordValue"/>.</typeparam>
    /// <seealso cref="PutRequest{TRow}" />
    /// <seealso cref="NoSQLClient.PutIfAbsentAsync"/>
    public class PutIfAbsentRequest<TRow> : PutRequest<TRow>, IPutOp
    {
        internal PutIfAbsentRequest(NoSQLClient client, string tableName,
            object row, PutOptions options) :
            base(client, tableName, row, options)
        {
        }

        PutOpKind IPutOp.PutOpKind => PutOpKind.IfAbsent;
    }

    /// <summary>
    /// Represents information about Put operation performed by
    /// <see cref="NoSQLClient.PutIfPresentAsync"/> API.
    /// </summary>
    /// <typeparam name="TRow">The type of value representing the row
    /// optionally returned by <see cref="PutResult{TRow}.ExistingRow"/>.
    /// Must be a reference type.  Currently the only supported type is
    /// <see cref="RecordValue"/>.</typeparam>
    /// <seealso cref="PutRequest{TRow}" />
    /// <seealso cref="NoSQLClient.PutIfPresentAsync"/>
    public class PutIfPresentRequest<TRow> : PutRequest<TRow>, IPutOp
    {
        internal PutIfPresentRequest(NoSQLClient client, string tableName,
            object row, PutOptions options) :
            base(client, tableName, row, options)
        {
        }

        PutOpKind IPutOp.PutOpKind => PutOpKind.IfPresent;
    }

    /// <summary>
    /// Represents information about Put operation performed by
    /// <see cref="NoSQLClient.PutIfVersionAsync"/> API.
    /// </summary>
    /// <typeparam name="TRow">The type of value representing the row
    /// optionally returned by <see cref="PutResult{TRow}.ExistingRow"/>.
    /// Must be a reference type.  Currently the only supported type is
    /// <see cref="RecordValue"/>.</typeparam>
    /// <seealso cref="PutRequest{TRow}" />
    /// <seealso cref="NoSQLClient.PutIfVersionAsync"/>
    public class PutIfVersionRequest<TRow> : PutRequest<TRow>, IPutOp
    {
        internal PutIfVersionRequest(NoSQLClient client, string tableName,
            object row, RowVersion matchVersion, PutOptions options) :
            base(client, tableName, row, options)
        {
            MatchVersion = matchVersion;
        }

        PutOpKind IPutOp.PutOpKind => PutOpKind.IfVersion;

        /// <summary>
        /// Gets the <see cref="RowVersion"/> of the row to match for
        /// the PutIfVersion operation.
        /// </summary>
        /// <value>
        /// The value of the version to match.
        /// </value>
        public RowVersion MatchVersion { get; }

        internal override void Validate()
        {
            base.Validate();
            if (MatchVersion == null)
            {
                throw new ArgumentNullException(nameof(MatchVersion),
                    "Must specify version for PutIfVersionAsync");
            }
        }

    }

}
