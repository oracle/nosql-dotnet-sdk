/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System.Diagnostics;
    using static ValidateUtils;

    /// <summary>
    /// For Cloud Service/Cloud Simulator only.
    /// CapacityMode specifies the type of capacity that will be set on a
    /// table. It is used in table creation and table capacity updates.
    /// </summary>
    /// <remarks>
    /// <para>
    /// In <see cref="Provisioned"/> mode, the application defines the
    /// specified maximum read and write throughput for a table, as well as
    /// the maximum storage size.
    /// </para>
    /// <para>
    /// <see cref="OnDemand"/> mode allows for flexible throughput usage.
    /// In this mode, only the maximum storage size is specified.
    /// </para>
    /// </remarks>
    /// <seealso cref="TableLimits"/>
    public enum CapacityMode
    {
        /// <summary>
        /// Provisioned mode.  This is the default.
        /// </summary>
        Provisioned = 1,

        /// <summary>
        /// On Demand mode.
        /// </summary>
        OnDemand = 2
    }

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
    /// The way to initialize <see cref="TableLimits"/> instance depends on
    /// the chosen <see cref="CapacityMode"/> of the table:
    /// </para>
    /// <para>In <see cref="SDK.CapacityMode.Provisioned"/> mode, all 3 values
    /// for throughput and storage limits must be specified.  There are no
    /// defaults and no mechanism to indicate "no change".
    /// </para>
    /// <para>
    /// In <see cref="SDK.CapacityMode.OnDemand"/> mode, only storage limit
    /// must be specified.
    /// </para>
    /// <para>
    /// You may also pass <see cref="TableLimits"/> to
    /// <seealso cref="NoSQLClient.SetTableLimitsAsync"/> or
    /// <seealso cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/> APIs
    /// to change <see cref="CapacityMode"/> of the existing table, that is
    /// to switch the table from Provisioned to On Demand or vice versa.
    /// </para>
    /// <para>
    /// <see cref="TableLimits"/> are also returned as part of
    /// <see cref="TableResult"/> from operations listed above as well as
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetTableAsync*"/> and
    /// <see cref="TableResult.WaitForCompletionAsync"/>.  For returned
    /// <see cref="TableLimits"/>, when using with
    /// <see cref="ServiceType.Cloud"/> Service, <see cref="ReadUnits"/> and
    /// <see cref="WriteUnits"/> will be available for both
    /// <see cref="SDK.CapacityMode.Provisioned"/> and
    /// <see cref="SDK.CapacityMode.OnDemand"/> tables.  For
    /// <see cref="SDK.CapacityMode.OnDemand"/> tables, these values will
    /// indicate the maximum limits set by the service for on-demand tables.
    /// Note that these values are not available for on-demand tables when
    /// using Cloud Simulator (see <see cref="ServiceType.CloudSim"/>).
    /// </para>
    /// <example>
    /// Specifying table limits when creating a provisioned table.
    /// <code>
    /// var result = await client.ExecuteTableDDLWithCompletionAsync(
    ///     "CREATE TABLE table1(id INTEGER, name STRING, PRIMARY KEY(id))",
    ///     new TableLimits(100, 200, 100));
    /// </code>
    /// </example>
    /// <example>
    /// Specifying table limits when creating an on demand table.
    /// <code>
    /// var result = await client.ExecuteTableDDLWithCompletionAsync(
    ///     "CREATE TABLE table1(id INTEGER, name STRING, PRIMARY KEY(id))",
    ///     new TableLimits(100));
    /// </code>
    /// </example>
    /// </remarks>
    /// <seealso cref="CapacityMode"/>
    /// <seealso cref="TableDDLOptions"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLAsync*"/>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.ExecuteTableDDLWithCompletionAsync*"/>
    /// <seealso cref="NoSQLClient.SetTableLimitsAsync"/>
    /// <seealso cref="NoSQLClient.SetTableLimitsWithCompletionAsync"/>
    public class TableLimits
    {
        internal TableLimits(int readUnits, int writeUnits, int storageGB,
            CapacityMode capacityMode)
        {
            ReadUnits = readUnits > 0 ? readUnits : 0;
            WriteUnits = writeUnits > 0 ? writeUnits : 0;
            StorageGB = storageGB;
            CapacityMode = capacityMode;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TableLimits"/> class
        /// with <see cref="SDK.CapacityMode.Provisioned"/> capacity mode.
        /// </summary>
        /// <param name="readUnits">Read units. Must be a positive value.
        /// </param>
        /// <param name="writeUnits">Write units. Must be a positive value.
        /// </param>
        /// <param name="storageGB">Maximum storage in gigabytes.  Must be a
        /// positive value.</param>
        public TableLimits(int readUnits, int writeUnits, int storageGB) :
            this(readUnits, writeUnits, storageGB, CapacityMode.Provisioned)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TableLimits"/> class
        /// with <see cref="SDK.CapacityMode.OnDemand"/> capacity mode.
        /// </summary>
        /// <param name="storageGB">Maximum storage in gigabytes.  Must be a
        /// positive value.</param>
        public TableLimits(int storageGB) :
            this(0, 0, storageGB, CapacityMode.OnDemand)
        {
        }

        /// <summary>
        /// Gets table capacity mode.
        /// </summary>
        /// <value>
        /// Table capacity mode, provisioned or on demand.
        /// </value>
        public CapacityMode CapacityMode { get; }

        /// <summary>
        /// Gets read units.
        /// </summary>
        /// <value>
        /// Read units if available, otherwise 0.
        /// </value>
        public int ReadUnits { get; }

        /// <summary>
        /// Gets write units.
        /// </summary>
        /// <value>
        /// Write units if available, otherwise 0.
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
            CheckEnumValue(CapacityMode);
            if (CapacityMode == CapacityMode.Provisioned)
            {
                CheckPositiveInt32(ReadUnits, nameof(ReadUnits));
                CheckPositiveInt32(WriteUnits, nameof(WriteUnits));
            }
            else
            {
                Debug.Assert(ReadUnits == 0);
                Debug.Assert(WriteUnits == 0);
            }

            CheckPositiveInt32(StorageGB, nameof(StorageGB));
        }
    }

}
