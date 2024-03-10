/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.IO;
    using static ValidateUtils;

    /// <summary>
    /// Represents information about operation performed by
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.DropReplicaAsync*"/> and
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.DropReplicaWithCompletionAsync*"/>
    /// APIs.
    /// </summary>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.DropReplicaAsync*"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.DropReplicaWithCompletionAsync*"/>
    /// <seealso cref="Request"/>
    /// <seealso cref="TableOperationRequest" />
    public class DropReplicaRequest : TableOperationRequest
    {
        internal string operationId;

        internal DropReplicaRequest(NoSQLClient client, string tableName,
            string regionId, DropReplicaOptions options = null) :
            base(client, tableName)
        {
            RegionId = regionId;
            Options = options;
        }

        internal override IOptions BaseOptions => Options;

        internal override ITableCompletionOptions CompletionOptions =>
            Options;

        internal override bool NeedsContentSigned => true;

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializeDropReplica(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializeDropReplica(stream, this);
        }

        internal override void Validate()
        {
            base.Validate();
            CheckNotNullOrEmpty(RegionId, "region id");
        }

        /// <summary>
        /// Gets the region id for the operation if it was provided to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.DropReplicaAsync*"/> or
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.DropReplicaWithCompletionAsync*"/>.
        /// </summary>
        /// <value>
        /// The region id if it was provided, otherwise <c> null</c>.
        /// </value>
        public string RegionId { get; }

        /// <summary>
        /// Gets the options for
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.DropReplicaAsync*"/> or
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.DropReplicaWithCompletionAsync*"/>
        /// operation.
        /// </summary>
        /// <value>
        /// The options, or <c>null</c> if options were not provided.
        /// </value>
        public DropReplicaOptions Options { get; }
    }

}
