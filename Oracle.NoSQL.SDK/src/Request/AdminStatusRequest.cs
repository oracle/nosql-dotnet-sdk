/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.IO;
    using static ValidateUtils;

    /// <summary>
    /// On-premise only.  Represents information about an operation performed
    /// by <see cref="NoSQLClient.GetAdminStatusAsync"/> API.
    /// </summary>
    /// <seealso cref="NoSQLClient.GetAdminStatusAsync"/>
    /// <seealso cref="Request"/>
    public class AdminStatusRequest : Request
    {
        internal AdminStatusRequest(NoSQLClient client,
            AdminResult adminResult, GetAdminStatusOptions options = null) :
            base(client)
        {
            AdminResult = adminResult;
            Options = options;
        }

        internal override IOptions BaseOptions => Options;

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializeGetAdminStatus(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializeGetAdminStatus(stream, this);
        }

        internal override void Validate()
        {
            base.Validate();
            CheckNotNull(AdminResult, nameof(AdminResult));
        }

        /// <summary>
        /// Gets the <see cref="AdminResult"/> used as an input to this
        /// operation.
        /// </summary>
        /// <value>
        /// The result returned by
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteAdminAsync*"/>
        /// used as an input to this operation.
        /// </value>
        public AdminResult AdminResult { get; }

        /// <summary>
        /// Gets the options for this operation.
        /// </summary>
        /// <value>
        /// The options or <c>null</c> if options were not provided.
        /// </value>
        public GetAdminStatusOptions Options { get; }
    }

}
