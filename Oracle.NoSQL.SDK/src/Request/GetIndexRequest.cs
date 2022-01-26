/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.IO;

    /// <summary>
    /// Represents information about operation performed by
    /// <see cref="NoSQLClient.GetIndexesAsync"/> API.
    /// </summary>
    /// <seealso cref="NoSQLClient.GetIndexesAsync"/>
    /// <seealso cref="Request"/>
    /// <seealso cref="RequestWithTable" />
    public class GetIndexesRequest : RequestWithTable
    {
        internal GetIndexesRequest(NoSQLClient client, string tableName,
            GetIndexOptions options) : base(client, tableName)
        {
            Options = options;
        }

        internal override IOptions BaseOptions => Options;

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializeGetIndexes(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializeGetIndexes(stream, this);
        }

        internal virtual string GetIndexName() => null;

        /// <summary>
        /// Gets the options for <see cref="NoSQLClient.GetIndexesAsync"/> and
        /// <see cref="NoSQLClient.GetIndexAsync"/> operations.
        /// </summary>
        /// <value>
        /// The options or <c>null</c> if options were not provided.
        /// </value>
        public GetIndexOptions Options { get; }
    }

    /// <summary>
    /// Represents information about operation performed by
    /// <see cref="NoSQLClient.GetIndexAsync"/> API.
    /// </summary>
    /// <seealso cref="NoSQLClient.GetIndexAsync"/>
    /// <seealso cref="GetIndexesRequest" />
    public class GetIndexRequest : GetIndexesRequest
    {
        internal GetIndexRequest(NoSQLClient client, string tableName,
            string indexName, GetIndexOptions options) :
            base(client, tableName, options)
        {
            IndexName = indexName;
        }

        /// <summary>
        /// Gets the name of the index.
        /// </summary>
        /// <value>
        /// The name of the index.
        /// </value>
        public string IndexName { get; }

        internal override string GetIndexName() => IndexName;

        internal override void Validate()
        {
            base.Validate();
            if (string.IsNullOrEmpty(IndexName))
            {
                throw new ArgumentException("Index name is null or empty");
            }
        }

    }

}
