/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.IO;


    /// <summary>
    /// Represents information about operation performed by
    /// <see cref="NoSQLClient.GetTableUsageAsync"/> method.
    /// </summary>
    /// <seealso cref="NoSQLClient.GetTableUsageAsync"/>
    /// <seealso cref="Request"/>
    /// <seealso cref="RequestWithTable" />
    public class GetTableUsageRequest : RequestWithTable
    {
        internal const string PagingFeature = "Paging of table usage records";

        internal GetTableUsageRequest(NoSQLClient client, string tableName,
            GetTableUsageOptions options) : base(client, tableName)
        {
            Options = options;
        }

        internal override IOptions BaseOptions => Options;

        internal void CheckPagingSupported() =>
            CheckProtocolVersion(PagingFeature, 4);
        
        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializeGetTableUsage(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializeGetTableUsage(stream, this);
        }

        /// <summary>
        /// Gets the options for <see cref="NoSQLClient.GetTableUsageAsync"/>
        /// operation.
        /// </summary>
        /// <value>
        /// The options or <c>null</c> if options were not provided.
        /// </value>
        public GetTableUsageOptions Options { get; }

        internal override void Validate()
        {
            base.Validate();
            if (Options?.FromIndex.HasValue ?? false)
            {
                CheckProtocolVersion(PagingFeature, 4);
            }
        }
    }

}
