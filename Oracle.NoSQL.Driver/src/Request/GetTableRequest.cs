/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{
    using System.IO;

    /// <summary>
    /// Represents information about operation performed by
    /// <see cref="NoSQLClient.GetTableAsync"/> API.
    /// </summary>
    /// <seealso cref="NoSQLClient.GetTableAsync"/>
    /// <seealso cref="Request"/>
    /// <seealso cref="RequestWithTable" />
    public class GetTableRequest : RequestWithTable
    {
        internal string operationId;

        internal GetTableRequest(NoSQLClient client, string tableName,
            string operationId, GetTableOptions options = null) :
            base(client, tableName)
        {
            this.operationId = operationId;
            Options = options;
        }

        internal GetTableRequest(NoSQLClient client, string tableName,
            GetTableOptions options) : this(client, tableName, null, options)
        {
        }

        internal GetTableRequest(NoSQLClient client, TableResult tableResult,
            GetTableOptions options) : this(client, tableResult.TableName,
            tableResult.OperationId, options)
        {
        }

        internal override IOptions BaseOptions => Options;

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializeGetTable(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializeGetTable(stream, this);
        }

        /// <summary>
        /// Gets the options for <see cref="NoSQLClient.GetTableAsync"/>
        /// operation.
        /// </summary>
        /// <value>
        /// The options or <c>null</c> if options were not provided.
        /// </value>
        public GetTableOptions Options { get; }
    }

}
