/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{
    using System.IO;
    using static ValidateUtils;

    /// <summary>
    /// Represents information about operation performed by
    /// <see cref="NoSQLClient.GetAsync"/> API.
    /// </summary>
    /// <typeparam name="TRow">The type of value representing the row as the
    /// result of Get operation.  Must be a reference type.  Currently the
    /// only supported type is <see cref="RecordValue"/>.</typeparam>
    /// <seealso cref="NoSQLClient.GetAsync"/>
    /// <seealso cref="Request"/>
    /// <seealso cref="ReadRequest" />
    public class GetRequest<TRow> : ReadRequest
    {
        internal GetRequest(NoSQLClient client, string tableName,
            object primaryKey, GetOptions options) :
            base(client, tableName)
        {
            PrimaryKey = primaryKey;
            Options = options;
        }

        internal override IReadOptions ReadOptions => Options;

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializeGet(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializeGet(stream, this);
        }

        internal override void Validate()
        {
            base.Validate();
            CheckNotNull(PrimaryKey, nameof(PrimaryKey));
        }

        /// <summary>
        /// Gets the value of the primary key for
        /// <see cref="NoSQLClient.GetAsync"/> operation.
        /// </summary>
        /// <value>
        /// The primary key as <c>object</c>.  Currently, its runtime type
        /// would only be <see cref="MapValue"/> or its subclasses.
        /// </value>
        public object PrimaryKey { get; }

        /// <summary>
        /// Gets the options for <see cref="NoSQLClient.GetAsync"/> operation.
        /// </summary>
        /// <value>
        /// The options or <c>null</c> if options were not provided.
        /// </value>
        public GetOptions Options { get; }
    }

}
