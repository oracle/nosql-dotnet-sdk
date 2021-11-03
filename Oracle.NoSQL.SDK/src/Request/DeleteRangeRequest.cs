/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.IO;
    using System.Threading;
    using static ValidateUtils;

    /// <summary>
    /// Represents information about DeleteRange operation performed by
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.DeleteRangeAsync*"/> and
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetDeleteRangeAsyncEnumerable*"/>
    /// APIs.
    /// </summary>
    /// <seealso cref="NoSQLClient.DeleteRangeAsync"/>
    /// <seealso cref="NoSQLClient.GetDeleteRangeAsyncEnumerable"/>
    /// <seealso cref="Request"/>
    /// <seealso cref="RequestWithTable" />
    public class DeleteRangeRequest : RequestWithTable
    {
        internal DeleteRangeRequest(NoSQLClient client, string tableName,
            object partialPrimaryKey, DeleteRangeOptions options) :
            base(client, tableName)
        {
            PartialPrimaryKey = partialPrimaryKey;
            Options = options;
            FieldRange = Options?.FieldRange;
        }

        internal DeleteRangeRequest(NoSQLClient client, string tableName,
            object partialPrimaryKey, FieldRange fieldRange = null) :
            base(client, tableName)
        {
            PartialPrimaryKey = partialPrimaryKey;
            FieldRange = fieldRange;
        }

        internal override IOptions BaseOptions => Options;

        internal override void Validate()
        {
            base.Validate();
            CheckNotNull(PartialPrimaryKey, nameof(PartialPrimaryKey));
            FieldRange?.Validate();
        }

        internal override void Serialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            serializer.SerializeDeleteRange(stream, this);
        }

        internal override object Deserialize(IRequestSerializer serializer,
            MemoryStream stream)
        {
            return serializer.DeserializeDeleteRange(stream, this);
        }

        /// <summary>
        /// Gets the value of partial primary key for DeleteRange operation.
        /// </summary>
        /// <value>
        /// Partial primary key as <c>object</c>.  Currently, its runtime type
        /// would only be <see cref="MapValue"/> or its subclasses.
        /// </value>
        public object PartialPrimaryKey { get; internal set; }

        /// <summary>
        /// Gets the options for the DeleteRange operation.
        /// </summary>
        /// <value>
        /// The options or <c>null</c> if options were not provided.
        /// </value>
        public DeleteRangeOptions Options { get; set; }

        /// <summary>
        /// Gets the field range for the DeleteRange operation.
        /// </summary>
        /// <value>
        /// Value of the field range if provided either as
        /// <see cref="DeleteRangeOptions.FieldRange"/> or as a parameter to
        /// <see cref="NoSQLClient.DeleteRangeAsync(string, object, FieldRange, CancellationToken)"/>,
        /// otherwise <c>null</c>.
        /// </value>
        public FieldRange FieldRange { get; }
    }
}
