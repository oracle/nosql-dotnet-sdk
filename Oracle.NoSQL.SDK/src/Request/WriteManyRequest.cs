/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using static ValidateUtils;

    /// <summary>
    /// Represents information about WriteMany operations performed by
    /// <see cref="NoSQLClient.WriteManyAsync"/>,
    /// <see cref="NoSQLClient.PutManyAsync"/> and
    /// <see cref="NoSQLClient.DeleteManyAsync"/> APIs.
    /// </summary>
    /// <typeparam name="TRow">The type of value representing the row. Must be
    /// a reference type.  Currently the only supported type is
    /// <see cref="RecordValue"/>.</typeparam>
    /// <seealso cref="NoSQLClient.WriteManyAsync"/>
    /// <seealso cref="NoSQLClient.PutManyAsync"/>
    /// <seealso cref="NoSQLClient.DeleteManyAsync"/>
    /// <seealso cref="Request"/>
    /// <seealso cref="RequestWithTable" />
    public class WriteManyRequest<TRow> : RequestWithTable
    {
        internal const int MaxOpCount = 50;

        internal WriteManyRequest(NoSQLClient client, string tableName,
            IReadOnlyCollection<IWriteOperation> operations,
            IWriteManyOptions options) : base(client, tableName)
        {
            Operations = operations;
            Options = options;
        }

        internal override IOptions BaseOptions => Options;

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializeWriteMany(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializeWriteMany(stream, this);
        }

        internal override void Validate()
        {
            base.Validate();
            CheckNotNull(Operations, "operations");

            if (Operations.Count == 0)
            {
                throw new ArgumentException(
                    "The list of operations cannot be empty",
                    nameof(Operations));
            }
        }

        internal bool AbortIfUnsuccessful =>
            Options != null && Options.AbortIfUnsuccessful;

        /// <summary>
        /// Gets the collection of operations that are part of this WriteMany
        /// operation.
        /// </summary>
        /// <value>
        /// The collection of operations as instances of
        /// <see cref="IWriteOperation"/>.  For
        /// <see cref="NoSQLClient.PutManyAsync"/> and
        /// <see cref="NoSQLClient.DeleteManyAsync"/> the operations in the
        /// collection will be either all <see cref="PutOperation"/> or all
        /// <see cref="DeleteOperation"/> instances respectively.
        /// </value>
        public IReadOnlyCollection<IWriteOperation> Operations { get; }

        internal IWriteManyOptions Options { get; }

        /// <summary>
        /// Gets options for <see cref="NoSQLClient.WriteManyAsync"/> API if
        /// this operation was performed by
        /// <see cref="NoSQLClient.WriteManyAsync"/>.
        /// </summary>
        /// <value>
        /// The options for <see cref="NoSQLClient.WriteManyAsync"/> API if
        /// this operation was performed by
        /// <see cref="NoSQLClient.WriteManyAsync"/>, otherwise <c>null</c>.
        /// </value>
        public WriteManyOptions WriteManyOptions =>
            Options is WriteManyOptions wmo ? wmo : null;

        /// <summary>
        /// Gets options for <see cref="NoSQLClient.PutManyAsync"/> API if
        /// this operation was performed by
        /// <see cref="NoSQLClient.PutManyAsync"/>.
        /// </summary>
        /// <value>
        /// The options for <see cref="NoSQLClient.PutManyAsync"/> API if
        /// this operation was performed by
        /// <see cref="NoSQLClient.PutManyAsync"/>, otherwise <c>null</c>.
        /// </value>
        public PutManyOptions PutManyOptions =>
            Options is PutManyOptions pmo ? pmo : null;

        /// <summary>
        /// Gets options for <see cref="NoSQLClient.DeleteManyAsync"/> API if
        /// this operation was performed by
        /// <see cref="NoSQLClient.DeleteManyAsync"/>.
        /// </summary>
        /// <value>
        /// The options for <see cref="NoSQLClient.DeleteManyAsync"/> API if
        /// this operation was performed by
        /// <see cref="NoSQLClient.DeleteManyAsync"/>, otherwise <c>null</c>.
        /// </value>
        public DeleteManyOptions DeleteManyOptions =>
            Options is DeleteManyOptions dmo ? dmo : null;
    }

}
