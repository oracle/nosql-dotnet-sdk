/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Threading;
    using static ValidateUtils;

    /// <summary>
    /// Base class for <see cref="WriteManyRequest{TRow}"/>.  Only used
    /// internally.
    /// </summary>
    public abstract class WriteManyRequest : Request
    {
        internal const int MaxOpCount = 50;

        internal WriteManyRequest(NoSQLClient client, string tableName,
            WriteOperationCollection operations, IWriteManyOptions options) :
            base(client)
        {
            TableName = tableName;
            Operations = operations;
            Options = options;
        }

        internal override IOptions BaseOptions => Options;

        internal Durability? Durability => Options?.Durability;

        internal override bool SupportsRateLimiting => true;

        internal override bool DoesReads => Operations.DoesReads;

        internal override bool DoesWrites => true;

        internal string TableName { get; }

        internal WriteOperationCollection Operations { get; }

        internal override string InternalTableName
        {
            get
            {
                string tableName;

                if (IsSingleTable)
                {
                    tableName = TableName;
                }
                else
                {
                    // If table name is specified per operation, use the table name of the
                    // first operation for simplicity.  For rate limiter, we will convert
                    // this to top-level (ancestor) table-name which would be the same for
                    // all operations.
                    Debug.Assert(Operations.Count != 0);
                    tableName = Operations[0].TableName;
                }
                Debug.Assert(tableName != null);
                return tableName;
            }
        }

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializeWriteMany(stream, this);
        }

        internal bool IsSingleTable => TableName != null;

        internal override void Validate()
        {
            base.Validate();
            CheckNotNull(Operations, "operations");
            
            if (IsSingleTable)
            {
                CheckTableName(TableName);
            }

            if (Operations.Count == 0)
            {
                throw new ArgumentException(
                    "The list of operations cannot be empty",
                    nameof(Operations));
            }

            var idx = 0;
            foreach (var op in Operations)
            {
                if (IsSingleTable)
                {
                    if (op.TableName != null)
                    {
                        throw new ArgumentException(
                            "Cannot specify table name both as argument to " +
                            $"WriteManyAsync: {TableName} and for " +
                            $"operation at index {idx}");
                    }
                }
                else if (string.IsNullOrEmpty(op.TableName))
                {
                    throw new ArgumentException(
                        "Missing or empty table name for operation at " +
                        $"index {idx}");
                }

                idx++;
            }
        }

        internal bool AbortIfUnsuccessful =>
            Options != null && Options.AbortIfUnsuccessful;

        internal IWriteManyOptions Options { get; }
    }

    /// <summary>
    /// Represents information about WriteMany operations performed by
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/>,
    /// <see cref="NoSQLClient.PutManyAsync"/> and
    /// <see cref="NoSQLClient.DeleteManyAsync"/> APIs.
    /// </summary>
    /// <typeparam name="TRow">The type of value representing the row. Must be
    /// a reference type.  Currently the only supported type is
    /// <see cref="RecordValue"/>.</typeparam>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/>
    /// <seealso cref="NoSQLClient.PutManyAsync"/>
    /// <seealso cref="NoSQLClient.DeleteManyAsync"/>
    /// <seealso cref="Request"/>
    /// <seealso cref="RequestWithTable" />
    public class WriteManyRequest<TRow> : WriteManyRequest
    {
        internal WriteManyRequest(NoSQLClient client, string tableName,
            WriteOperationCollection operations, IWriteManyOptions options) :
            base(client, tableName, operations, options)
        {
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializeWriteMany<TRow>(stream, this);
        }

        /// <summary>
        /// Gets the name, if available.
        /// </summary>
        /// <value>
        /// Table name if available, otherwise <c>null</c>.  This property
        /// will be <c>null</c> if using
        /// <see cref="NoSQLClient.WriteManyAsync(WriteOperationCollection, WriteManyOptions, CancellationToken)"/>.
        /// Table name must be specified for all other WriteMany, PutMany and
        /// DeleteMany APIs.
        /// </value>
        public new string TableName => base.TableName;

        /// <summary>
        /// Gets the collection of operations that are part of this WriteMany
        /// operation.
        /// </summary>
        /// <value>
        /// Instance of <see cref="WriteOperationCollection"/> that represents
        /// a collection of operations that are instances of
        /// <see cref="IWriteOperation"/>.  For
        /// <see cref="NoSQLClient.PutManyAsync"/> and
        /// <see cref="NoSQLClient.DeleteManyAsync"/> the operations in the
        /// collection will be either all <see cref="PutOperation"/> or all
        /// <see cref="DeleteOperation"/> instances respectively.
        /// </value>
        public new WriteOperationCollection Operations => base.Operations;

        /// <summary>
        /// Gets options for
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/> API
        /// if this operation was performed by
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/>.
        /// </summary>
        /// <value>
        /// The options for
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/> API
        /// if this operation was performed by
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/>,
        /// otherwise <c>null</c>.
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
