/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using static ValidateUtils;

    /// <summary>
    /// For Cloud Service/Cloud Simulator only.
    /// Table limits are used during table creation to specify the throughput
    /// and capacity to be consumed by the table as well as in an operation to
    /// change the limits of an existing table.
    /// </summary>
    /// <remarks>
    /// <para>
    /// These operations are performed by
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>,
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>,
    /// <see cref="NoSQLClient.SetTableLimitsAsync"/> and
    /// <see cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/> methods.
    /// The values provided are enforced by the system and are used for
    /// billing purposes.
    /// </para>
    /// <para>
    /// Throughput limits are defined in terms of read units and write units.
    /// A read unit represents 1 eventually consistent read per second for
    /// data up to 1 KB in size.  A read that is absolutely consistent is
    /// double that, consuming 2 read units for a read of up to 1 KB in size.
    /// This means that if an application is to use
    /// <see cref="Consistency.Absolute"/> it may need to specify additional
    /// read units when creating a table.  A write unit represents 1 write per
    /// second of data up to 1 KB in size.
    /// </para>
    /// <para>
    /// In addition to throughput, table capacity must be specified to
    /// indicate the maximum amount of storage, in gigabytes, allowed for the
    /// table.
    /// </para>
    /// <para>
    /// All 3 values must be used whenever using this object. There are no
    /// defaults and no mechanism to indicate "no change."
    /// </para>
    /// <example>
    /// Changing table limits for a table.
    /// <code>
    /// var result = await client.SetTableLimitsWithCompletionAsync("myTable",
    ///     new TableLimits(100, 200, 100));
    /// </code>
    /// </example>
    /// </remarks>
    /// <seealso cref="TableDDLOptions"/>
    /// <seealso cref="NoSQLClient.SetTableLimitsAsync"/>
    /// <seealso cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/>
    public class TableLimits
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TableLimits"/> class.
        /// </summary>
        /// <param name="readUnits">Read units. Must be a positive value.
        /// </param>
        /// <param name="writeUnits">Write units. Must be a positive value.
        /// </param>
        /// <param name="storageGB">Maximum storage in gigabytes.  Must be a
        /// positive value.</param>
        public TableLimits(int readUnits, int writeUnits, int storageGB)
        {
            ReadUnits = readUnits;
            WriteUnits = writeUnits;
            StorageGB = storageGB;
        }

        /// <summary>
        /// Gets read units.
        /// </summary>
        /// <value>
        /// Read units.
        /// </value>
        public int ReadUnits { get; }

        /// <summary>
        /// Gets write units.
        /// </summary>
        /// <value>
        /// Write units.
        /// </value>
        public int WriteUnits { get; }

        /// <summary>
        /// Gets maximum storage capacity in GB.
        /// </summary>
        /// <value>
        /// Maximum storage capacity in GB.
        /// </value>
        public int StorageGB { get; }

        internal void Validate()
        {
            CheckPositiveInt32(ReadUnits, nameof(ReadUnits));
            CheckPositiveInt32(WriteUnits, nameof(WriteUnits));
            CheckPositiveInt32(StorageGB, nameof(StorageGB));
        }
    }

}
