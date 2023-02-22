/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using static ValidateUtils;

    /// <summary>
    /// Represents information about table DDL operation performed by
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>
    /// and
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>
    /// methods.
    /// </summary>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>
    /// <seealso cref="Request"/>
    public class TableDDLRequest : Request
    {
        internal string tableName;

        internal TableDDLRequest(NoSQLClient client, string statement,
            string tableName, TableDDLOptions options) : base(client)
        {
            Statement = statement;
            this.tableName = tableName;
            Options = options;
        }

        internal TableDDLRequest(NoSQLClient client, string statement,
            TableDDLOptions options) : this(client, statement, null, options)
        {
        }

        internal override TimeSpan GetDefaultTimeout() =>
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

        internal virtual IDictionary<string, IDictionary<string, string>>
            GetDefinedTags() => Options?.DefinedTags;

        internal virtual IDictionary<string, string> GetFreeFormTags() =>
            Options?.FreeFormTags;

        internal override string InternalTableName => tableName;

        internal override void ApplyResult(object result)
        {
            base.ApplyResult(result);
            Client.RateLimitingHandler?.ApplyTableResult((TableResult)result);
        }

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
    /// Cloud Service Only.
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
            TableLimits tableLimits, TableDDLOptions options) :
            base(client, null, tableName, options)
        {
            TableLimits = tableLimits;
        }

        /// <summary>
        /// Gets the table name.
        /// </summary>
        /// <value>
        /// Table name.
        /// </value>
        public string TableName => InternalTableName;

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

    /// <summary>
    /// Cloud Service Only.
    /// Represents information about operation performed by
    /// <see cref="NoSQLClient.SetTableTagsAsync"/> and
    /// <see cref="NoSQLClient.SetTableTagsWithCompletionAsync"/>
    /// methods.
    /// </summary>
    /// <seealso cref="NoSQLClient.SetTableTagsAsync"/>
    /// <seealso cref="NoSQLClient.SetTableTagsWithCompletionAsync"/>
    /// <seealso cref="Request"/>
    public class TableTagsRequest : TableDDLRequest
    {
        internal TableTagsRequest(NoSQLClient client, string tableName,
            IDictionary<string, IDictionary<string, string>> definedTags,
            IDictionary<string, string> freeFormTags, TableDDLOptions options) :
            base(client, null, tableName, options)
        {
            DefinedTags = definedTags;
            FreeFormTags = freeFormTags;
        }

        internal override IDictionary<string, IDictionary<string, string>>
            GetDefinedTags() => DefinedTags;

        internal override IDictionary<string, string> GetFreeFormTags() =>
            FreeFormTags;

        /// <summary>
        /// Gets the table name.
        /// </summary>
        /// <value>
        /// Table name.
        /// </value>
        public string TableName => InternalTableName;

        /// <summary>
        /// Gets defined tags for the operation.
        /// </summary>
        /// <value>
        /// Defined tags.
        /// </value>
        public IDictionary<string, IDictionary<string, string>> DefinedTags
        {
            get;
        }

        /// <summary>
        /// Gets free-form tags for the operation.
        /// </summary>
        /// <value>
        /// Free-form tags.
        /// </value>
        public IDictionary<string, string> FreeFormTags { get; }

        internal override void Validate()
        {
            base.Validate();
            CheckTableName(TableName);

            if (DefinedTags == null && FreeFormTags == null)
            {
                throw new ArgumentException(
                    "Must set at least one of DefinedTags and FreeFormTags");
            }
        }
    }

}
