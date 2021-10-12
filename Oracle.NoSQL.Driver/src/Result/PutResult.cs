/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{
    /// <summary>
    /// Represents the result of the Put operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is the result of <see cref="NoSQLClient.PutAsync"/>,
    /// <see cref="NoSQLClient.PutIfAbsentAsync"/>,
    /// <see cref="NoSQLClient.PutIfPresentAsync"/> and
    /// <see cref="NoSQLClient.PutIfVersionAsync"/> APIs.
    /// </para>
    /// <para>
    /// <see cref="PutResult{TRow}.Success"/> determines whether the
    /// conditional Put operation was successful.  For unconditional Put
    /// operations this property will always be <c>true</c> because other
    /// failures throw exceptions. If successful,
    /// <see cref="PutResult{TRow}.Version"/> will be set.  If conditional
    /// Put operation fails, <see cref="PutResult{TRow}.ExistingRow"/> and
    /// its <see cref="PutResult{TRow}.ExistingVersion"/> may be available if
    /// <see cref="PutOptions.ReturnExisting"/> was set to <c>true</c>.
    /// </para>
    /// </remarks>
    /// <typeparam name="TRow">The type of value representing the row
    /// optionally returned by <see cref="PutResult{TRow}.ExistingRow"/>.
    /// Must be a reference type.  Currently the only supported type is
    /// <see cref="RecordValue"/>.</typeparam>
    /// <seealso cref="NoSQLClient.PutAsync"/>
    /// <seealso cref="ConsumedCapacity"/>
    /// <seealso cref="RowVersion"/>
    public class PutResult<TRow> : IWriteResultWithId<TRow>, IDataResult
    {
        // Implement interface properties explicitly to avoid having public
        // setters in the result class.

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

        FieldValue IWriteResultWithId<TRow>.GeneratedValue
        {
            get => GeneratedValue;
            set => GeneratedValue = value;
        }

        /// <inheritdoc cref="GetResult{TRow}.ConsumedCapacity"/>
        public ConsumedCapacity ConsumedCapacity { get; internal set; }

        /// <summary>
        /// Gets a value indicating whether the Put operation was successful.
        /// </summary>
        /// <remarks>
        /// This value determines success of conditional Put operation (as
        /// indicated by <see cref="PutOptions.IfAbsent"/>,
        /// <see cref="PutOptions.IfPresent"/> or
        /// <see cref="PutOptions.MatchVersion"/> options).  For unconditional
        /// Put operation this value is always <c>true</c>.
        /// </remarks>
        /// <value>
        /// <c>true</c> if the Put operation was successful, otherwise
        /// <c>false</c>.
        /// </value>
        /// <seealso cref="PutOptions.IfAbsent"/>
        /// <seealso cref="PutOptions.IfPresent"/>
        /// <seealso cref="PutOptions.MatchVersion"/>
        public bool Success { get; internal set; }

        /// <summary>
        /// Gets the <see cref="RowVersion"/> of the new row if the Put
        /// operation was successful.
        /// </summary>
        /// <value>
        /// Version of the new row if the Put operation was successful,
        /// otherwise <c>null</c>.
        /// </value>
        public RowVersion Version { get; internal set; }

        /// <summary>
        /// Gets the value of existing row if the conditional Put operation
        /// has failed.
        /// </summary>
        /// <remarks>
        /// This value is available if the conditional Put operation (as
        /// indicated by <see cref="PutOptions.IfAbsent"/>,
        /// <see cref="PutOptions.IfPresent"/> or
        /// <see cref="PutOptions.MatchVersion"/> options) has failed and
        /// <see cref="PutOptions.ReturnExisting"/> was set to <c>true</c>.
        /// </remarks>
        /// <value>
        /// Value of existing row if available, otherwise <c>null</c>.
        /// </value>
        /// <seealso cref="PutOptions.IfAbsent"/>
        /// <seealso cref="PutOptions.IfPresent"/>
        /// <seealso cref="PutOptions.MatchVersion"/>
        /// <seealso cref="PutOptions.ReturnExisting"/>
        public TRow ExistingRow { get; internal set; }

        /// <summary>
        /// Gets the value of <see cref="RowVersion"/> of existing row if the
        /// conditional Put operation has failed.
        /// </summary>
        /// <remarks>
        /// This value is available if the conditional Put operation (as
        /// indicated by <see cref="PutOptions.IfAbsent"/>,
        /// <see cref="PutOptions.IfPresent"/> or
        /// <see cref="PutOptions.MatchVersion"/> options) has failed and
        /// <see cref="PutOptions.ReturnExisting"/> was set to <c>true</c>.
        /// </remarks>
        /// <value>
        /// Version of <see cref="RowVersion"/> of existing row if
        /// available, otherwise <c>null</c>.
        /// </value>
        /// <seealso cref="PutOptions.IfAbsent"/>
        /// <seealso cref="PutOptions.IfPresent"/>
        /// <seealso cref="PutOptions.MatchVersion"/>
        /// <seealso cref="PutOptions.ReturnExisting"/>
        public RowVersion ExistingVersion { get; internal set; }

        /// <summary>
        /// Gets the value generated by the Put operation for an identity or
        /// generated UUID column.
        /// </summary>
        /// <remarks>
        /// This property returns the value generated if the Put operation
        /// created a new value for an identity column or a string column
        /// declared as generated UUID. It is available only if the table has
        /// such column and a value was generated for that column by the Put
        /// operation.
        /// </remarks>
        /// <value>Generated value if available, otherwise <c>null</c>.
        /// </value>
        public FieldValue GeneratedValue { get; set; }
    }

}
