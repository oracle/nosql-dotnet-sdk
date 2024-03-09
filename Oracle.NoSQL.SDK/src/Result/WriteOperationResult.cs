/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

    /// <summary>
    /// Represents the result of each Put or Delete sub operation in
    /// <see cref="WriteManyResult{TRow}"/>.
    /// </summary>
    /// <remarks>
    /// This class contains all relevant properties from
    /// <see cref="PutResult{TRow}"/> and <see cref="DeleteResult{TRow}"/>
    /// (note that <em>ConsumedCapacity</em> is not defined on the sub
    /// operation basis).
    /// </remarks>
    /// <inheritdoc cref="WriteManyResult{TRow}" path="typeparam"/>
    /// <seealso cref="WriteManyResult{TRow}"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/>
    /// <seealso cref="NoSQLClient.PutManyAsync"/>
    /// <seealso cref="NoSQLClient.DeleteManyAsync"/>
    public class WriteOperationResult<TRow> : IWriteResultWithId<TRow>
    {
        internal WriteOperationResult()
        {
        }

        // Implement interface properties explicitly to avoid having public
        // setters in the result class.

        bool IWriteResult<TRow>.Success
        {
            get => Success;
            set => Success = value;
        }

        TRow IWriteResult<TRow>.ExistingRow
        {
            get => ExistingRow;
            set => ExistingRow = value;
        }

        RowVersion IWriteResult<TRow>.ExistingVersion
        {
            get => ExistingVersion;
            set => ExistingVersion = value;
        }

        DateTime? IWriteResult<TRow>.ExistingModificationTime
        {
            get => ExistingModificationTime;
            set => ExistingModificationTime = value;
        }

        FieldValue IWriteResultWithId<TRow>.GeneratedValue
        {
            get => GeneratedValue;
            set => GeneratedValue = value;
        }

        /// <summary>
        /// Gets a value indicating whether the Put or Delete operation was
        /// successful.
        /// </summary>
        /// <remarks>
        /// The success is defined in the same way as in
        /// <see cref="PutResult{TRow}.Success"/> and
        /// <see cref="DeleteResult{TRow}.Success"/> respectively.
        /// </remarks>
        /// <value>
        /// <c>true</c> if the Put or Delete operation was successful,
        /// otherwise <c>false</c>.
        /// </value>
        /// <seealso cref="PutResult{TRow}.Success"/>
        /// <seealso cref="DeleteResult{TRow}.Success"/>
        public bool Success { get; internal set; }

        /// <summary>
        /// For Put operations only.  Gets the <see cref="RowVersion"/> of the
        /// new row if the Put operation was successful.
        /// </summary>
        /// <value>
        /// Version of the new row if this result is a result of a Put
        /// operation and this operation was successful, otherwise
        /// <c>null</c>.
        /// </value>
        /// <seealso cref="PutResult{TRow}.Version"/>
        public RowVersion Version { get; internal set; }

        /// <summary>
        /// Gets the value of existing row if the conditional Put or Delete
        /// operation has failed.
        /// </summary>
        /// <remarks>
        /// This value is equivalent to
        /// <see cref="PutResult{TRow}.ExistingRow"/> or
        /// <see cref="DeleteResult{TRow}.ExistingRow"/> for Put and Delete
        /// operations respectively.
        /// </remarks>
        /// <inheritdoc cref="PutResult{TRow}.ExistingRow" path="value"/>
        /// <seealso cref="PutResult{TRow}.ExistingRow"/>
        /// <seealso cref="DeleteResult{TRow}.ExistingRow"/>
        public TRow ExistingRow { get; internal set; }

        /// <summary>
        /// Gets the value of <see cref="RowVersion"/> of existing row if the
        /// conditional Put or Delete operation has failed.
        /// </summary>
        /// <remarks>
        /// This value is equivalent to
        /// <see cref="PutResult{TRow}.ExistingVersion"/> or
        /// <see cref="DeleteResult{TRow}.ExistingVersion"/> for Put and
        /// Delete operations respectively.
        /// </remarks>
        /// <inheritdoc cref="PutResult{TRow}.ExistingVersion" path="value"/>
        /// <seealso cref="PutResult{TRow}.ExistingVersion"/>
        /// <seealso cref="DeleteResult{TRow}.ExistingVersion"/>
        public RowVersion ExistingVersion { get; internal set; }

        /// <summary>
        /// Gets the modification time of existing row if the conditional Put
        /// or Delete operation has failed.
        /// </summary>
        /// <remarks>
        /// This value is equivalent to
        /// <see cref="PutResult{TRow}.ExistingModificationTime"/> or
        /// <see cref="DeleteResult{TRow}.ExistingModificationTime"/> for Put and
        /// Delete operations respectively.
        /// </remarks>
        /// <inheritdoc cref="PutResult{TRow}.ExistingModificationTime" path="value"/>
        /// <seealso cref="PutResult{TRow}.ExistingModificationTime"/>
        /// <seealso cref="DeleteResult{TRow}.ExistingModificationTime"/>
        public DateTime? ExistingModificationTime { get; internal set; }

        /// <summary>
        /// For Put operations only.  Gets the value generated by the Put
        /// operation for an identity or generated UUID column.
        /// </summary>
        /// <inheritdoc cref="PutResult{TRow}.GeneratedValue" path="remarks"/>
        /// <inheritdoc cref="PutResult{TRow}.GeneratedValue" path="value"/>
        /// <seealso cref="PutResult{TRow}.GeneratedValue"/>
        public FieldValue GeneratedValue { get; set; }
    }

}
