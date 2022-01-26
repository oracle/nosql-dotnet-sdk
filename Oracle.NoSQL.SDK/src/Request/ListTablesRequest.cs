/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.IO;

    /// <summary>
    /// Represents information about operation performed by
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ListTablesAsync*"/> and
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetListTablesAsyncEnumerable*"/>
    /// APIs.
    /// </summary>
    /// <seealso cref="NoSQLClient.ListTablesAsync"/>
    /// <seealso cref="NoSQLClient.GetListTablesAsyncEnumerable"/>
    /// <seealso cref="Request"/>
    /// <seealso cref="RequestWithTable" />
    public class ListTablesRequest : Request
    {
        internal ListTablesRequest(NoSQLClient client,
            ListTablesOptions options) : base(client)
        {
            Options = options;
        }

        internal override IOptions BaseOptions => Options;

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializeListTables(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializeListTables(stream, this);
        }

        /// <summary>
        /// Gets the options for <see cref="NoSQLClient.ListTablesAsync"/>
        /// and <see cref="NoSQLClient.GetListTablesAsyncEnumerable"/>
        /// operations.
        /// </summary>
        /// <value>
        /// The options or <c>null</c> if options were not provided.
        /// </value>
        public ListTablesOptions Options { get; }
    }

}
