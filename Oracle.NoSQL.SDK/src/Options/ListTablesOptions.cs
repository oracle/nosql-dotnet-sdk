/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using static ValidateUtils;
    using System.Threading;

    /// <summary>
    /// Represents options passed to
    /// <see cref="NoSQLClient.ListTablesAsync(ListTablesOptions, CancellationToken)"/>
    /// and
    /// <see cref="NoSQLClient.GetListTablesAsyncEnumerable(ListTablesOptions, CancellationToken)"/>
    /// APIs.
    /// </summary>
    /// <remarks>
    /// For properties not specified or <c>null</c>,
    /// appropriate defaults will be used as indicated below.
    /// <para>
    /// If you expect the number of tables to be very large, you may page the
    /// returned list of table names over multiple calls to
    /// <see cref="NoSQLClient.ListTablesAsync(ListTablesOptions, CancellationToken)"/>
    /// using <see cref="ListTablesOptions.FromIndex"/> and
    /// <see cref="ListTablesOptions.Limit"/> properties as shown in the
    /// example below.  However, the recommended way is to call
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetListTablesAsyncEnumerable*"/>
    /// and iterate over its result, in which case you only need to use the
    /// <see cref="ListTablesOptions.Limit"/> property.
    /// </para>
    /// </remarks>
    /// <example>
    /// Asynchronously paging and printing table names using
    /// <see cref="NoSQLClient.ListTablesAsync(ListTablesOptions, CancellationToken)"/>
    /// and <see cref="ListTablesOptions.FromIndex"/> and
    /// <see cref="ListTablesOptions.Limit"/> properties.  We iterate until
    /// the number of returned table names becomes less than the limit (and
    /// possibly 0), which means that the last partial result has been
    /// received.
    /// <code>
    /// var options = new ListTablesOptions
    /// {
    ///     Timeout = TimeSpan.FromSeconds(10)
    ///     Limit = 100
    /// };
    /// do
    /// {
    ///     var result = await client.ListTablesAsync(options);
    ///     foreach(var tableName in result.TableNames)
    ///     {
    ///         Console.WriteLine(tableName);
    ///     }
    ///     options.FromIndex = result.NextIndex;
    /// } while(result.TableNames.Count == options.Limit);
    /// </code>
    /// </example>
    /// <seealso cref="NoSQLClient.ListTablesAsync(ListTablesOptions, CancellationToken)"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetListTablesAsyncEnumerable*"/>
    public class ListTablesOptions : IOptions
    {
        /// <inheritdoc cref="GetOptions.Compartment"/>
        public string Compartment { get; set; }

        /// <summary>
        /// On-premise NoSQL database only. Gets or sets the namespace to use
        /// for the operation.
        /// </summary>
        /// <remarks>
        /// If the value is set, only tables from the given namespace are
        /// listed, otherwise all tables for the user are listed.
        /// </remarks>
        /// <value>
        /// The namespace name.
        /// </value>
        public string Namespace
        {
            get => Compartment;
            set => Compartment = value;
        }

        /// <inheritdoc cref="GetOptions.Timeout"/>
        public TimeSpan? Timeout { get; set; }

        /// <summary>
        /// Gets or sets the index to use to start returning table names.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property can be used to page table names over multiple calls
        /// to
        /// <see cref="NoSQLClient.ListTablesAsync(ListTablesOptions, CancellationToken)"/>
        /// in order to avoid returning a very large list in a single result.
        /// To page table names, set this value to 0 on the first call
        /// to
        /// <see cref="NoSQLClient.ListTablesAsync(ListTablesOptions, CancellationToken)"/>
        /// and on subsequent calls to <see cref="ListTablesResult.NextIndex"/>
        /// returned from the previous call.  These operations are best done
        /// in a loop.
        /// </para>
        /// <para>
        /// However, the recommended and more simple way to page table names
        /// is to use
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetListTablesAsyncEnumerable*"/>
        /// in which case you need not use this property.
        /// </para>
        /// </remarks>
        /// <value>
        /// Starting index to return table names.  Cannot be a negative value.
        /// The default is 0 (start from the beginning of the list).
        /// </value>
        /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetListTablesAsyncEnumerable*"/>
        public int? FromIndex { get; set; }

        /// <summary>
        /// Gets or sets the maximum number of tables to return.
        /// </summary>
        /// <remarks>
        /// Represents maximum number of tables to return in one call to
        /// <see cref="NoSQLClient.ListTablesAsync(ListTablesOptions, CancellationToken)"/>
        /// or when iterating with
        /// <see cref="NoSQLClient.GetListTablesAsyncEnumerable(ListTablesOptions, CancellationToken)"/>.
        /// </remarks>
        /// <value>
        /// The limit.  If set, must be a positive value.  If not set, there is no limit.
        /// </value>
        public int? Limit { get; set; }

        void IOptions.Validate()
        {
            CheckTimeout(Timeout);
            CheckNonNegativeInt32(FromIndex, nameof(FromIndex));
            CheckPositiveInt32(Limit, nameof(Limit));
        }

        internal ListTablesOptions Clone() =>
            (ListTablesOptions)MemberwiseClone();
    }

}
