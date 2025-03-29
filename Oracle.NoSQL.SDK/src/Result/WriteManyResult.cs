/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents the result of WriteMany, PutMany or DeleteMany operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is the result of
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/>,
    /// <see cref="NoSQLClient.PutManyAsync"/> and
    /// <see cref="NoSQLClient.DeleteManyAsync"/> APIs.
    /// </para>
    /// <para>
    /// If the operation succeeds, <see cref="WriteManyResult{TRow}.Success"/>
    /// is <c>true</c> and the execution result of each Put or Delete sub
    /// operation is available as part of
    /// <see cref="WriteManyResult{TRow}.Results"/> property as an instance of
    /// <see cref="WriteOperationResult{TRow}"/>.
    /// </para>
    /// <para>
    /// If the operation is aborted because of the failure of a Put or Delete
    /// sub operation that has
    /// <see cref="IWriteOperation.AbortIfUnsuccessful"/> set to <c>true</c>
    /// or because of failure of any sub operation if
    /// <see cref="WriteManyOptions.AbortIfUnsuccessful"/>,
    /// <see cref="PutManyOptions.AbortIfUnsuccessful"/> or
    /// <see cref="DeleteManyOptions.AbortIfUnsuccessful"/> is set to
    /// <c>true</c>, then <see cref="WriteManyResult{TRow}.Success"/> is
    /// <c>false</c> and <see cref="WriteManyResult{TRow}.Results"/> is set to
    /// <c>null</c>.  The index of failed Put or Delete operation is
    /// available as <see cref="WriteManyResult{TRow}.FailedOperationIndex"/>
    /// and its execution result is available as
    /// <see cref="WriteManyResult{TRow}.FailedOperationResult"/>.
    /// </para>
    /// </remarks>
    /// <typeparam name="TRow">The type of value representing the row
    /// optionally returned by
    /// <see cref="WriteOperationResult{TRow}.ExistingRow"/> of Put or Delete
    /// sub operation.  Must be a reference type.  Currently the only
    /// supported type is <see cref="RecordValue"/>.</typeparam>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/>
    /// <seealso cref="NoSQLClient.PutManyAsync"/>
    /// <seealso cref="NoSQLClient.DeleteManyAsync"/>
    /// <seealso cref="WriteOperationResult{TRow}"/>
    public class WriteManyResult<TRow> : IDataResult
    {
        internal WriteManyResult()
        {
        }

        ConsumedCapacity IDataResult.ConsumedCapacity
        {
            get => ConsumedCapacity;
            set => ConsumedCapacity = value;
        }

        /// <inheritdoc cref="GetResult{TRow}.ConsumedCapacity"/>
        public ConsumedCapacity ConsumedCapacity { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the operation was successful.
        /// </summary>
        /// <value>
        /// <c>true</c> if WriteMany, PutMany or DeleteMany operation was
        /// successful, <c>false</c> if the operation was aborted because
        /// of the failure of sub operation and the setting of
        /// <em>AbortIfUnsuccessful</em> option.
        /// </value>
        public bool Success => !FailedOperationIndex.HasValue;

        /// <summary>
        /// Gets the list of results of sub operations.
        /// </summary>
        /// <value>
        /// The list of results of Put/Delete sub operations if the operation
        /// was successful, otherwise <c>null</c>.
        /// </value>
        /// <seealso cref="WriteOperationResult{TRow}"/>
        public IReadOnlyList<WriteOperationResult<TRow>> Results
        {
            get;
            internal set;
        }

        /// <summary>
        /// Gets the index of the failed Put or Delete sub operation that
        /// resulted in the entire operation aborting.
        /// </summary>
        /// <value>
        /// Index of the failed Put or Delete sub operation in the
        /// <see cref="WriteManyResult{TRow}.Results"/> list if the entire
        /// operation was aborted, otherwise <c>null</c>.
        /// </value>
        public int? FailedOperationIndex { get; internal set; }

        /// <summary>
        /// Gets the result of the failed Put or Delete sub operation that
        /// resulted in the entire operation aborting.
        /// </summary>
        /// <value>
        /// Result of the failed Put or Delete sub operation if the entire
        /// operation was aborted, otherwise <c>null</c>.
        /// </value>
        public WriteOperationResult<TRow> FailedOperationResult
        {
            get;
            internal set;
        }
    }

}
