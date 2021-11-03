/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.IO;
    using static ValidateUtils;

    /// <summary>
    /// Represents information about the Prepare operation performed by
    /// <see cref="NoSQLClient.PrepareAsync"/> API.
    /// </summary>
    /// <seealso cref="NoSQLClient.PrepareAsync"/>
    public class PrepareRequest : Request
    {
        internal PrepareRequest(NoSQLClient client, string statement,
            PrepareOptions options) : base(client)
        {
            Statement = statement;
            Options = options;
        }

        internal override IOptions BaseOptions => Options;

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializePrepare(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializePrepare(stream, this);
        }

        internal override void Validate()
        {
            base.Validate();
            CheckNotNullOrEmpty(Statement, nameof(Statement));
        }

        /// <summary>
        /// Gets the query SQL statement.
        /// </summary>
        /// <value>
        /// The query SQL statement.
        /// </value>
        public string Statement { get; internal set; }

        /// <summary>
        /// Gets the options for the Prepare operation.
        /// </summary>
        /// <value>
        /// The options or <c>null</c> if options were not provided.
        /// </value>
        public PrepareOptions Options { get; internal set; }

        internal bool GetQueryPlan => Options != null && Options.GetQueryPlan;
    }
}
