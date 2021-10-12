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
    /// Represents information about table DDL operation performed by
    /// <see cref="M:Oracle.NoSQL.Driver.NoSQLClient.ExecuteTableDDLAsync*"/>
    /// and
    /// <see cref="M:Oracle.NoSQL.Driver.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>
    /// methods.
    /// </summary>
    /// <seealso cref="NoSQLClient.ExecuteTableDDLAsync"/>
    /// <seealso cref="NoSQLClient.ExecuteTableDDLWithCompletionAsync"/>
    /// <seealso cref="Request"/>
    public class TableDDLRequest : Request
    {
        // Timeout to use for GetTableRequest when called inside
        // WaitForCompletionAsync() without timeout.  Large enough for many
        // retries if necessary.  This timeout is also added to the timeout
        // of TableDDLRequest when called from
        // ExecuteTableDDLWithCompletionAsync.
        internal static readonly TimeSpan DefaultPollRequestTimeout =
            TimeSpan.FromSeconds(600);

        private readonly bool withCompletion;
        internal string tableName;

        internal TableDDLRequest(NoSQLClient client, string statement,
            string tableName, TableDDLOptions options,
            bool withCompletion = false) : base(client)
        {
            Statement = statement;
            this.tableName = tableName;
            Options = options;
            this.withCompletion = withCompletion;
        }

        internal TableDDLRequest(NoSQLClient client, string statement,
            TableDDLOptions options, bool withCompletion = false) :
            this(client, statement, null, options, withCompletion)
        {
        }

        internal override TimeSpan GetDefaultTimeout() => withCompletion ?
            Config.TableDDLTimeout +
            (Config.TablePollTimeout ?? DefaultPollRequestTimeout) :
            Config.TableDDLTimeout;

        internal override IOptions BaseOptions => Options;

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializeTableDDL(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializeTableDDL(stream, this);
        }

        internal virtual TableLimits GetTableLimits() => Options?.TableLimits;

        internal string GetTableName() => tableName;

        /// <summary>
        /// Gets the SQL statement.  Returns <c>null</c> if this is
        /// an instance of <see cref="TableLimitsRequest"/>.
        /// </summary>
        /// <value>
        /// SQL statement.
        /// </value>
        public string Statement { get; }

        /// <summary>
        /// Gets the options for table DDL operation.
        /// </summary>
        /// <value>
        /// The options or <c>null</c> if options were not provided.
        /// </value>
        public TableDDLOptions Options { get; }
    }

    /// <summary>
    /// Represents information about operation performed by
    /// <see cref="NoSQLClient.SetTableLimitsAsync"/> and
    /// <see cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/>
    /// methods.
    /// </summary>
    /// <seealso cref="NoSQLClient.SetTableLimitsAsync"/>
    /// <seealso cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/>
    /// <seealso cref="Request"/>
    public class TableLimitsRequest : TableDDLRequest
    {
        internal TableLimitsRequest(NoSQLClient client, string tableName,
            TableLimits tableLimits, TableDDLOptions options,
            bool withCompletion = false) :
            base(client, null, tableName, options, withCompletion)
        {
            TableLimits = tableLimits;
        }

        /// <summary>
        /// Gets the table name.
        /// </summary>
        /// <value>
        /// Table name.
        /// </value>
        public string TableName => GetTableName();

        /// <summary>
        /// Gets the table limits for the operation.
        /// </summary>
        /// <value>
        /// Table limits.
        /// </value>
        public TableLimits TableLimits { get; }

        internal override TableLimits GetTableLimits() => TableLimits;

        internal override void Validate()
        {
            base.Validate();
            CheckTableName(TableName);

            if (TableLimits == null)
            {
                throw new ArgumentException(
                    "Missing table limits for TableLimitsRequest");
            }

            TableLimits.Validate();
        }
    }

}
