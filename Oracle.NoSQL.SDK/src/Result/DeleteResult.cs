/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{

    /// <summary>
    /// Represents the result of the Delete operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is the result of <see cref="NoSQLClient.DeleteAsync"/> and
    /// <see cref="NoSQLClient.DeleteIfVersionAsync"/> APIs.
    /// </para>
    /// <para>
    /// <see cref="DeleteResult{TRow}.Success"/> determines whether the
    /// Delete operation was successful.  Unconditional Delete will be
    /// successful if the row exists.  Conditional Delete will be successful
    /// if the row exists and its version matches the one provided.
    /// </para>
    /// <para>
    /// If conditional Delete operation fails,
    /// <see cref="DeleteResult{TRow}.ExistingRow"/> and
    /// its <see cref="DeleteResult{TRow}.ExistingVersion"/> may be available
    /// if <see cref="DeleteOptions.ReturnExisting"/> was set to <c>true</c>.
    /// </para>
    /// </remarks>
    /// <typeparam name="TRow">The type of value representing the row
    /// optionally returned by <see cref="DeleteResult{TRow}.ExistingRow"/>.
    /// Must be a reference type.  Currently the only supported type is
    /// <see cref="RecordValue"/>.</typeparam>
    /// <seealso cref="NoSQLClient.DeleteAsync"/>
    /// <seealso cref="NoSQLClient.DeleteIfVersionAsync"/>
    /// <seealso cref="ConsumedCapacity"/>
    /// <seealso cref="RowVersion"/>
    public class DeleteResult<TRow> : IWriteResult<TRow>, IDataResult
    {
        ConsumedCapacity IDataResult.ConsumedCapacity
        {
            get => ConsumedCapacity;
            set => ConsumedCapacity = value;
        }

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

        /// <inheritdoc cref="GetResult{TRow}.ConsumedCapacity"/>
        public ConsumedCapacity ConsumedCapacity { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the Delete operation was
        /// successful.
        /// </summary>
        /// <value>
        /// <c>true</c> if the Delete operation was successful, otherwise
        /// <c>false</c>.
        /// </value>
        public bool Success { get; internal set; }

        /// <summary>
        /// Gets the value of existing row if the conditional Delete operation
        /// has failed.
        /// </summary>
        /// <remarks>
        /// This value is available if the conditional Delete operation (as
        /// indicated by <see cref="DeleteOptions.MatchVersion"/>) has failed
        /// and <see cref="DeleteOptions.ReturnExisting"/> was set to
        /// <c>true</c>.
        /// </remarks>
        /// <value>
        /// Value of existing row if available, otherwise <c>null</c>.
        /// </value>
        /// <seealso cref="DeleteOptions.MatchVersion"/>
        /// <seealso cref="NoSQLClient.DeleteIfVersionAsync"/>
        public TRow ExistingRow { get; internal set; }

        /// <summary>
        /// Gets the value of <see cref="RowVersion"/> of existing row if the
        /// conditional Delete operation has failed.
        /// </summary>
        /// <remarks>
        /// This value is available if the conditional Delete operation (as
        /// indicated by <see cref="DeleteOptions.MatchVersion"/>) has failed
        /// and <see cref="DeleteOptions.ReturnExisting"/> was set to
        /// <c>true</c>.
        /// </remarks>
        /// <value>
        /// Version of existing row if available, otherwise <c>null</c>.
        /// </value>
        /// <seealso cref="DeleteOptions.MatchVersion"/>
        /// <seealso cref="NoSQLClient.DeleteIfVersionAsync"/>
        public RowVersion ExistingVersion { get; internal set; }
    }

}
