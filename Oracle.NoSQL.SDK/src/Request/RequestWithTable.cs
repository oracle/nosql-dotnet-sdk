/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using static ValidateUtils;

    /// <summary>
    /// Base class for information about all operations that require table
    /// name.
    /// </summary>
    /// <seealso cref="Request" />
    public abstract class RequestWithTable : Request
    {
        internal RequestWithTable(NoSQLClient client, string tableName) :
            base(client)
        {
            TableName = tableName;
        }

        /// <summary>
        /// Gets the name of the table.
        /// </summary>
        /// <value>
        /// Table name.
        /// </value>
        public string TableName { get; internal set; }

        internal override void Validate()
        {
            base.Validate();
            CheckTableName(TableName);
        }
    }

    /// <summary>
    /// Base class for information about all operations that read a row from a
    /// table.
    /// </summary>
    /// <remarks>
    /// Used as based class for <see cref="GetRequest{TRow}"/>.
    /// </remarks>
    /// <seealso cref="RequestWithTable"/>
    /// <seealso cref="GetRequest{TRow}"/>
    public abstract class ReadRequest : RequestWithTable
    {
        internal ReadRequest(NoSQLClient client, string tableName) :
            base(client, tableName)
        {
        }

        internal abstract IReadOptions ReadOptions { get; }

        internal override IOptions BaseOptions => ReadOptions;

        internal Consistency Consistency =>
            ReadOptions?.Consistency ?? Config.Consistency;

    }

    /// <summary>
    /// Base class for information about all operations that write or delete a
    /// row from a table.
    /// </summary>
    /// <remarks>
    /// Used as base class for <see cref="PutRequest{TRow}"/> and
    /// <see cref="DeleteRequest{TRow}"/>.
    /// </remarks>
    /// <seealso cref="RequestWithTable"/>
    /// <seealso cref="PutRequest{TRow}"/>
    /// <seealso cref="DeleteRequest{TRow}"/>
    public abstract class WriteRequest : RequestWithTable
    {
        internal WriteRequest(NoSQLClient client, string tableName) :
            base(client, tableName)
        {
        }

        internal abstract IWriteOptions WriteOptions { get; }

        internal override IOptions BaseOptions => WriteOptions;

        internal bool ReturnExisting => WriteOptions?.ReturnExisting ?? false;

        internal Durability? Durability => WriteOptions?.Durability;
    }

}
