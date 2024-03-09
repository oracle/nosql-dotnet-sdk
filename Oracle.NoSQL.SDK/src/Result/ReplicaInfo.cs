/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    /// <summary>
    /// Cloud Service only.
    /// Represents information about a single remote replica of a Global
    /// Active table.
    /// </summary>
    /// <remarks>
    /// For more information, see
    /// <see href="https://docs.oracle.com/en/cloud/paas/nosql-cloud/gasnd">
    /// Global Active Tables in NDCS
    /// </see>. You can retrieve information about table replicas from any
    /// method that returns <see cref="TableResult"/> (such as
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetTableAsync*"/>,
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.AddReplicaAsync*"/>, etc.)
    /// via <see cref="TableResult.Replicas"/> property.
    /// </remarks>
    /// <seealso cref="TableResult.Replicas"/>
    public class ReplicaInfo
    {
        internal ReplicaInfo()
        {
        }

        /// <summary>
        /// Gets the name of the replica.
        /// </summary>
        /// <value>
        /// Name of the replica. This is the same as a region id (see
        /// <see cref="Region.RegionId"/>) of the replica's region.
        /// </value>
        public string ReplicaName { get; internal set; }

        /// <summary>
        /// Gets the region of the replica, if given <see cref="Region"/> is
        /// known in the SDK.
        /// </summary>
        /// <value>
        /// Region of the replica, or <c>null</c> if given
        /// <see cref="Region"/> value is not defined.
        /// </value>
        public Region Region => Region.FromRegionId(ReplicaName);

        /// <summary>
        /// Gets the OCID of the replica table.
        /// </summary>
        /// <value>
        /// OCID of the replica table.
        /// </value>
        public string ReplicaOCID { get; internal set; }

        /// <summary>
        /// Gets the capacity mode of the replica table.
        /// </summary>
        /// <remarks>
        /// Capacity mode may be set separately for each replica.
        /// </remarks>
        /// <value>
        /// Capacity mode of the replica table.
        /// </value>
        /// <seealso cref="CapacityMode"/>
        public CapacityMode CapacityMode { get; internal set; }

        /// <summary>
        /// Gets the write units of the replica table.
        /// </summary>
        /// <remarks>
        /// <para>
        /// From the standpoint of the local table, write units of the replica
        /// table define the maximum throughput used for replicating writes
        /// from the replica to the local table. This throughput adds to the
        /// total write throughput of the local table. If the replica has
        /// capacity mode <see cref="CapacityMode.OnDemand"/>,
        /// system-configured limits will be used.
        /// </para>
        /// <para>
        /// Note that reads are done locally so the read units of the replica
        /// table do not affect the read throughput of the local table.
        /// </para>
        /// <para>
        /// Both write and read units can be set separately for each replica.
        /// </para>
        /// </remarks>
        /// <value>Write units of the replica table.</value>
        /// <seealso cref="TableLimits.WriteUnits"/>
        public int WriteUnits { get; internal set; }

        /// <summary>
        /// Gets the operational state of the replica table.
        /// </summary>
        /// <remarks>
        /// Note that replica initialization process (see
        /// <see cref="TableResult.IsLocalReplicaInitialized"/>) does not
        /// affect the replica table state (it will still be
        /// <see cref="TableState.Active"/>).
        /// </remarks>
        /// <value>
        /// Table state of the replica table.
        /// </value>
        /// <seealso cref="SDK.TableState"/>
        public TableState TableState { get; internal set; }
    }
    
}
