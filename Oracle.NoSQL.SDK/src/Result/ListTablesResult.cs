/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.Collections.Generic;

    /// <summary>
    /// Represents the result of ListTables operation.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class represents the result of
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ListTablesAsync*"/> API
    /// and partial results when iterating with
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetListTablesAsyncEnumerable*"/>.
    /// </para>
    /// <para>
    /// On a successful operation the table names are available as well as the
    /// index of the last returned table.  Table names are sorted
    /// alphabetically.
    /// </para>
    /// </remarks>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.ListTablesAsync*"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetListTablesAsyncEnumerable*"/>
    public class ListTablesResult
    {
        internal ListTablesResult()
        {
        }

        /// <summary>
        /// Gets the list of table names.
        /// </summary>
        /// <remarks>
        /// Note that the list may be empty if either no tables are found
        /// matching the <see cref="ListTablesOptions.Compartment"/> or
        /// <see cref="ListTablesOptions.Namespace"/> or you are paging
        /// table names manually with
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ListTablesAsync*"/>
        /// and no more partial results remain.  See the example in
        /// <see cref="ListTablesOptions"/>.
        /// </remarks>
        /// <value>
        /// List of table names.
        /// </value>
        public IReadOnlyList<string> TableNames { get; internal set; }

        /// <summary>
        /// Gets the next index after the last table name returned.
        /// </summary>
        /// <remarks>
        /// If you are paging table names manually with
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ListTablesAsync*"/>
        /// assign this value to
        /// <see cref="ListTablesOptions.FromIndex"/>. See the example in
        /// <see cref="ListTablesOptions"/>.  This property is not needed if
        /// you are using
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetListTablesAsyncEnumerable*"/>.
        /// </remarks>
        /// <value>
        /// Next table name index.
        /// </value>
        public int NextIndex { get; internal set; }
    }

}
