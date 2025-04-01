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
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetReplicaStatsAsync*"/>
    /// API.
    /// </summary>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.DropReplicaAsync*"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.DropReplicaWithCompletionAsync*"/>
    /// <seealso cref="Request"/>
    /// <seealso cref="TableOperationRequest" />
    public class GetReplicaStatsRequest : RequestWithTable
    {
        internal string operationId;

        internal GetReplicaStatsRequest(NoSQLClient client, string tableName,
            string regionId, GetReplicaStatsOptions options = null) :
            base(client, tableName)
        {
            RegionId = regionId;
            Options = options;
        }

        internal override IOptions BaseOptions => Options;

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializeGetReplicaStats(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializeGetReplicaStats(stream, this);
        }

        /// <summary>
        /// Gets the region id for the operation if it was provided to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetReplicaStatsAsync*"/>.
        /// </summary>
        /// <value>
        /// The region id if it was provided, otherwise <c> null</c>.
        /// </value>
        public string RegionId { get; }

        /// <summary>
        /// Gets the options for
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetReplicaStatsAsync*"/>
        /// operation.
        /// </summary>
        /// <value>
        /// The options, or <c>null</c> if options were not provided.
        /// </value>
        public GetReplicaStatsOptions Options { get; }
    }

}
