/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Threading;
    using static ValidateUtils;

    /// <summary>
    /// Represents options for the DeleteRange operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These options are passed to APIs
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.DeleteRangeAsync*"/>
    /// and
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetDeleteRangeAsyncEnumerable*"/>.
    /// For properties not specified or <c>null</c>, appropriate defaults will
    /// be used.
    /// </para>
    /// <para>
    /// Note that if you only need to specify <see cref="FieldRange"/>, you
    /// may use overloads
    /// <see cref="NoSQLClient.DeleteRangeAsync(string, object, FieldRange, CancellationToken)"/>
    /// and
    /// <see cref="NoSQLClient.GetDeleteRangeAsyncEnumerable(string, object, FieldRange, CancellationToken)"/>
    /// instead that take <see cref="FieldRange"/> instead of these options.
    /// </para>
    /// </remarks>
    /// <example>
    /// Executing DeleteRange operation with provided
    /// <see cref="DeleteRangeOptions"/>.
    /// <code>
    /// var result = await client.DeleteRange(
    ///     "myTable",
    ///     new MapValue // partial primary key
    ///     {
    ///         ["deptId"] = 50
    ///     },
    ///     new DeleteRangeOptions
    ///     {
    ///         Timeout = TimeSpan.FromSeconds(20),
    ///         FieldRange = new FieldRange("itemId")
    ///         {
    ///             StartsAfter = 1010,
    ///             EndsWith = 2000
    ///         });
    ///     });
    /// </code>
    /// </example>
    /// <seealso cref="NoSQLClient.DeleteRangeAsync"/>
    /// <seealso cref="NoSQLClient.GetDeleteRangeAsyncEnumerable"/>
    /// <seealso cref="FieldRange"/>
    /// <seealso cref="DeleteRangeContinuationKey"/>
    public class DeleteRangeOptions : IOptions
    {
        internal const int MaxWriteKBLimit = 2048;

        /// <inheritdoc cref="GetOptions.Compartment"/>
        public string Compartment { get; set; }

        /// <inheritdoc cref="GetOptions.Timeout"/>
        public TimeSpan? Timeout { get; set; }

        /// <inheritdoc cref="PutOptions.Durability"/>
        public Durability? Durability { get; set; }

        /// <summary>
        /// Gets or sets the field range.
        /// </summary>
        /// <remarks>
        /// The field range is based on portion of the primary key that was not
        /// provided in the partial primary key.  If not set, the operation
        /// will delete all rows matching the partial primary key.
        /// </remarks>
        /// <value>
        /// The field range.
        /// </value>
        /// <seealso cref="FieldRange"/>
        public FieldRange FieldRange { get; set; }

        /// <summary>
        /// Gets or sets the limit on the total KB of data written during the
        /// operation.
        /// </summary>
        /// <remarks>
        /// This value can only reduce the system defined limit.  Either limit
        /// may cause the need for continuation of this operation with the
        /// continuation key returned by
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.DeleteRangeAsync*"/> or
        /// via iterating over
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetDeleteRangeAsyncEnumerable*"/>.
        /// </remarks>
        /// <value>
        /// Limit on the total KB write for the operation.  If set, must be
        /// positive value.  If not set, the system defined limit will be
        /// used.
        /// </value>
        public int? MaxWriteKB { get; set; }

        /// <summary>
        /// Gets or sets the continuation key for the DeleteRange operation.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Only for use with <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.DeleteRangeAsync*"/>.
        /// This property is not needed if using
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetDeleteRangeAsyncEnumerable*"/>.
        /// </para>
        /// <para>
        /// The continuation key is returned as
        /// <see cref="DeleteRangeResult.ContinuationKey"/> from the previous
        /// call to <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.DeleteRangeAsync*"/>
        /// and can be used to continue this operation.  Operations with a
        /// continuation key still require the primary key.
        /// </para>
        /// </remarks>
        /// <value>
        /// The continuation key on subsequent calls to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.DeleteRangeAsync*"/>.
        /// </value>
        /// <seealso cref="DeleteRangeResult.ContinuationKey"/>
        public DeleteRangeContinuationKey ContinuationKey { get; set; }

        internal DeleteRangeOptions Clone() =>
            (DeleteRangeOptions)MemberwiseClone();

        void IOptions.Validate()
        {
            CheckTimeout(Timeout);
            Durability?.Validate();
            CheckPositiveInt32(MaxWriteKB, nameof(MaxWriteKB));

            // Currently the proxy returns BadProtocolException on this so we
            // check it here so that ArgumentException can be thrown.
            CheckNotAboveLimit(MaxWriteKB, MaxWriteKBLimit, nameof(MaxWriteKB));

            // FieldRange is validated in DeleteRangeRequest
        }

    }
}
