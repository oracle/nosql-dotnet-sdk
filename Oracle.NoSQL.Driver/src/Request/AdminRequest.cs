/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{
    using System;
    using System.IO;
    using static ValidateUtils;

    /// <summary>
    /// On-premise only.  Represents information about admin DDL operation
    /// performed by
    /// <see cref="M:Oracle.NoSQL.Driver.NoSQLClient.ExecuteAdminAsync*"/> and
    /// <see cref="M:Oracle.NoSQL.Driver.NoSQLClient.ExecuteAdminWithCompletionAsync*"/>
    /// methods.
    /// </summary>
    /// <seealso cref="NoSQLClient.ExecuteAdminAsync"/>
    /// <seealso cref="NoSQLClient.ExecuteAdminWithCompletionAsync"/>
    /// <seealso cref="Request"/>
    public class AdminRequest : Request
    {
        // Timeout to use for AdminStatusRequest when called inside
        // WaitForCompletionAsync() without timeout.  Large enough for many
        // retries if necessary.  This timeout is also added to the timeout
        // of AdminRequest when called from
        // ExecuteAdminWithCompletionAsync.
        internal static readonly TimeSpan DefaultPollRequestTimeout =
            TimeSpan.FromSeconds(600);

        private readonly bool withCompletion;

        internal AdminRequest(NoSQLClient client, char [] statement,
            AdminOptions options, bool withCompletion = false) :
            base(client)
        {
            Statement = statement;
            Options = options;
            this.withCompletion = withCompletion;
        }

        internal override IOptions BaseOptions => Options;

        internal override TimeSpan GetDefaultTimeout() => withCompletion ?
            Config.AdminTimeout +
            (Config.AdminPollTimeout ?? DefaultPollRequestTimeout) :
            Config.AdminTimeout;

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializeAdmin(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializeAdmin(stream, this);
        }

        internal override void Validate()
        {
            base.Validate();
            CheckNotNull(Statement, nameof(Statement));
            if (Statement.Length == 0)
            {
                throw new ArgumentException("Statement cannot be empty",
                    nameof(Statement));
            }
        }

        /// <summary>
        /// Gets the statement for the admin DDL operation.
        /// </summary>
        /// <value>
        /// The statement for the admin DDL operation.
        /// </value>
        public char [] Statement { get; }

        /// <summary>
        /// Gets the options for the admin DDL operation.
        /// </summary>
        /// <value>
        /// The options or <c>null</c> if options were not provided.
        /// </value>
        public AdminOptions Options { get; }
    }

}
