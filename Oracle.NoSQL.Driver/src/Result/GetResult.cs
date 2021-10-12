/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{
    using System;

    /// <summary>
    /// Represents the result of <see cref="NoSQLClient.GetAsync"/> operation.
    /// </summary>
    /// <remarks>
    /// If matching row is found, it is available as
    /// <see cref="GetResult{TRow}.Row"/> as well as its version and
    /// (optional) expiration time.  If matching row is not found,
    /// <see cref="GetResult{TRow}.Row"/> as well as other properties, except
    /// <see cref="GetResult{TRow}.ConsumedCapacity"/>, are set to
    /// <c>null</c>.
    /// </remarks>
    /// <typeparam name="TRow">The type of value representing the returned
    /// row.  Must be a reference type.  Currently the only supported type is
    /// <see cref="RecordValue"/>.</typeparam>
    /// <seealso cref="NoSQLClient.GetAsync"/>
    /// <seealso cref="ConsumedCapacity"/>
    /// <seealso cref="RowVersion"/>
    public class GetResult<TRow> : IDataResult
    {
        // We have to implement the interface explicitly (via the class
        // property) because the implicit implementation does not have public
        // setter.
        ConsumedCapacity IDataResult.ConsumedCapacity
        {
            get => ConsumedCapacity;
            set => ConsumedCapacity = value;
        }

        /// <summary>
        /// Cloud Service/Cloud Simulator only.  Gets capacity consumed by
        /// this operation.
        /// </summary>
        /// <value>
        /// Consumed capacity.  For on-premise NoSQL Database, this value is
        /// always <c>null</c>.
        /// </value>
        /// <seealso cref="ConsumedCapacity"/>
        public ConsumedCapacity ConsumedCapacity { get; internal set; }

        /// <summary>
        /// Gets the value of the returned row.
        /// </summary>
        /// <value>
        /// The value of the returned row or <c>null</c> if the row doesn't
        /// exist.
        /// </value>
        public TRow Row { get; internal set; }

        /// <summary>
        /// Gets the expiration time of the row.
        /// </summary>
        /// <value>
        /// Expiration time of the row.  <c>null</c> if the row exists but
        /// does not expire or the row does not exist.
        /// </value>
        public DateTime? ExpirationTime { get; internal set; }

        /// <summary>
        /// Gets the version of the returned row.
        /// </summary>
        /// <value>
        /// Version of the returned row or <c>null</c> if the row does not
        /// exist.
        /// </value>
        /// <see cref="RowVersion"/>
        public RowVersion Version { get; internal set; }
    }

}
