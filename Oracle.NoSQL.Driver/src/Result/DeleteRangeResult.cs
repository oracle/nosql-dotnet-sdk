/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{
    using System.Diagnostics;

    /// <summary>
    /// Represents the result of the DeleteRange operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class represents the result of
    /// <see cref="M:Oracle.NoSQL.Driver.NoSQLClient.DeleteRangeAsync*"/> API
    /// and partial results when iterating with
    /// <see cref="M:Oracle.NoSQL.Driver.NoSQLClient.GetDeleteRangeAsyncEnumerable*"/>.
    /// </para>
    /// <para>
    /// On a successful operation the number of rows deleted is available
    /// using <see cref="DeleteRangeResult.DeletedCount"/>.  There is a limit
    /// to the amount of data consumed by a single request to the server.
    /// If there are still more  rows to delete, the continuation key will be
    /// available as <see cref="DeleteRangeResult.ContinuationKey"/>.
    /// </para>
    /// </remarks>
    public class DeleteRangeResult : IDataResult
    {
        ConsumedCapacity IDataResult.ConsumedCapacity
        {
            get => ConsumedCapacity;
            set => ConsumedCapacity = value;
        }

        /// <inheritdoc cref="GetResult{TRow}.ConsumedCapacity"/>
        public ConsumedCapacity ConsumedCapacity { get; internal set; }

        /// <summary>
        /// Gets the number of rows deleted.
        /// </summary>
        /// <value>
        /// The number of rows deleted.
        /// </value>
        public int DeletedCount { get; internal set; }

        /// <summary>
        /// Gets the continuation key.
        /// </summary>
        /// <remarks>
        /// You only need to use this property if using
        /// <see cref="M:Oracle.NoSQL.Driver.NoSQLClient.DeleteRangeAsync*"/>.
        /// It is not needed if using
        /// <see cref="M:Oracle.NoSQL.Driver.NoSQLClient.GetDeleteRangeAsyncEnumerable*"/>.
        /// </remarks>
        /// <value>
        /// The continuation key indicating where the next call to
        /// <see cref="M:Oracle.NoSQL.Driver.NoSQLClient.DeleteRangeAsync*"/> can
        /// resume or <c>null</c> if there are no more rows to delete.
        /// </value>
        /// <seealso cref="DeleteRangeContinuationKey"/>
        public DeleteRangeContinuationKey ContinuationKey { get; internal set; }
    }

    /// <summary>
    /// Represents the continuation key for the DeleteRange operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <see cref="DeleteRangeContinuationKey"/> is an opaque type returned
    /// from <see cref="M:Oracle.NoSQL.Driver.NoSQLClient.DeleteRangeAsync*"/>
    /// as <see cref="DeleteRangeResult.ContinuationKey"/> when the operation
    /// has exceeded the maximum amount of data to be modified and it needs to
    /// be continued with subsequent calls to
    /// <see cref="M:Oracle.NoSQL.Driver.NoSQLClient.DeleteRangeAsync*"/>.
    /// For each subsequent
    /// call, set <see cref="DeleteRangeOptions.ContinuationKey"/> to the
    /// value of <see cref="DeleteRangeResult.ContinuationKey"/> returned by
    /// the previous call.
    /// </para>
    /// <para>
    /// You do not need to use this type if iterating over
    /// <see cref="M:Oracle.NoSQL.Driver.NoSQLClient.GetDeleteRangeAsyncEnumerable*"/>.
    /// </para>
    /// </remarks>
    /// <seealso cref="NoSQLClient.DeleteRangeAsync"/>
    /// <seealso cref="DeleteRangeResult"/>
    /// <seealso cref="DeleteRangeOptions"/>
    public class DeleteRangeContinuationKey
    {
        internal byte[] Bytes { get; }

        internal DeleteRangeContinuationKey(byte[] bytes)
        {
            Debug.Assert(bytes != null);
            Bytes = bytes;
        }
    }

}
