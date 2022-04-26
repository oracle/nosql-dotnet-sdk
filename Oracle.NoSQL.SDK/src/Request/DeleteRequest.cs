/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.IO;
    using static ValidateUtils;

    // This interface is used to unite serialization implementation between
    // Delete and WriteMany APIs
    internal interface IDeleteOp
    {
        object PrimaryKey { get; }

        DeleteOptions Options { get; }

        RowVersion MatchVersion { get; }

        bool ReturnExisting { get; }
    }

    /// <summary>
    /// Represents information about Delete operation performed by
    /// <see cref="NoSQLClient.DeleteAsync"/> API.
    /// </summary>
    /// <typeparam name="TRow">The type of value representing the row
    /// optionally returned by <see cref="DeleteResult{TRow}.ExistingRow"/>.
    /// Must be a reference type.  Currently the only supported type is
    /// <see cref="RecordValue"/>.</typeparam>
    /// <seealso cref="NoSQLClient.DeleteAsync"/>
    /// <seealso cref="Request"/>
    /// <seealso cref="WriteRequest" />
    public class DeleteRequest<TRow> : WriteRequest, IDeleteOp
    {
        internal DeleteRequest(NoSQLClient client, string tableName,
            object primaryKey, DeleteOptions options) :
            base(client, tableName)
        {
            PrimaryKey = primaryKey;
            Options = options;
        }

        internal override IWriteOptions WriteOptions => Options;

        RowVersion IDeleteOp.MatchVersion => Options?.MatchVersion;

        bool IDeleteOp.ReturnExisting => ReturnExisting;

        internal override bool DoesReads => true;

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializeDelete(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializeDelete(stream, this);
        }

        internal override void Validate()
        {
            base.Validate();
            CheckNotNull(PrimaryKey, nameof(PrimaryKey));
        }

        /// <summary>
        /// Gets the value of the primary key for the Delete operation.
        /// </summary>
        /// <value>
        /// The primary key as <c>object</c>.  Currently, its runtime type
        /// would only be <see cref="MapValue"/> or its subclasses.
        /// </value>
        public object PrimaryKey { get; internal set; }

        /// <summary>
        /// Gets the options for the Delete operation.
        /// </summary>
        /// <value>
        /// The options or <c>null</c> if options were not provided.
        /// </value>
        public DeleteOptions Options { get; set; }
    }

    /// <summary>
    /// Represents information about Put operation performed by
    /// <see cref="NoSQLClient.DeleteIfVersionAsync"/> API.
    /// </summary>
    /// <typeparam name="TRow">The type of value representing the row
    /// optionally returned by <see cref="DeleteResult{TRow}.ExistingRow"/>.
    /// Must be a reference type.  Currently the only supported type is
    /// <see cref="RecordValue"/>.</typeparam>
    /// <seealso cref="DeleteRequest{TRow}" />
    /// <seealso cref="NoSQLClient.DeleteIfVersionAsync"/>
    public class DeleteIfVersionRequest<TRow> : DeleteRequest<TRow>, IDeleteOp
    {
        internal DeleteIfVersionRequest(NoSQLClient client, string tableName,
            object primaryKey, RowVersion matchVersion,
            DeleteOptions options) :
            base(client, tableName, primaryKey, options)
        {
            MatchVersion = matchVersion;
        }

        /// <summary>
        /// Gets the <see cref="RowVersion"/> of the row to match for
        /// <see cref="NoSQLClient.DeleteIfVersionAsync"/> operation.
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
                    "Must specify version for DeleteIfVersionAsync");
            }
        }

    }

}
