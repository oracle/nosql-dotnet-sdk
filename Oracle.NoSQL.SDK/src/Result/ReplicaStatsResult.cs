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

    /// <summary>
    /// Cloud Service only.
    /// Result returned by
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetReplicaStatsAsync*"/>. It
    /// contains replica statistics for the requested table.
    /// </summary>
    /// <seealso cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetReplicaStatsAsync*"/>
    /// <seealso cref="ReplicaStatsRecord"/>
    public class ReplicaStatsResult
    {
        internal ReplicaStatsResult()
        {
        }

        /// <summary>
        /// Gets the name of the table.
        /// </summary>
        /// <value>
        /// Table name.
        /// </value>
        public string TableName { get; internal set; }

        /// <summary>
        /// Gets the next start time.
        /// </summary>
        /// <remarks>
        /// This can be used when retrieving large number of replica stats
        /// records over multiple calls to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetReplicaStatsAsync*"/>.
        /// Pass this value as <see cref="GetReplicaStatsOptions.StartTime"/>
        /// on the subsequent call to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetReplicaStatsAsync*"/>.
        /// </remarks>
        /// <value>
        /// Start time to use for next call to
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetReplicaStatsAsync*"/>.
        /// </value>
        public DateTime NextStartTime { get; internal set; }

        /// <summary>
        /// Gets the replica statistics information for one or more replicas.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This value is represented as a dictionary with keys being region
        /// id (see <see cref="Region.RegionId"/>) of a replica and values
        /// being a list of <see cref="ReplicaStatsRecord"/> objects for that
        /// replica. If you passed region as parameter to one of
        /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.GetReplicaStatsAsync*"/>
        /// methods, this dictionary will contain only one key-value pair for
        /// the given region.
        /// </para>
        /// <para>
        /// Note that in either case this object will contain only keys for
        /// which there is at least one <see cref="ReplicaStatsRecord"/>
        /// returned (it will not contain keys for regions for which no stats
        /// records were found according to parameters specified in
        /// <see cref="GetReplicaStatsOptions"/> or applicable defaults).
        /// </para>
        /// </remarks>
        /// <value>
        /// Replica statistics information as a dictionary with one entry per
        /// each replica for which the stats were requested/available.
        /// </value>
        /// <example>
        /// Print replica lag info for <see cref="Region.EU_ZURICH_1"/>
        /// region.
        /// <code>
        /// var statsRecords = statsResult.StatsRecords["eu-zurich-1"];
        /// foreach(var statsRecord in statsRecords) {
        ///     Console.WriteLine(statsRecord);
        ///         $"Replica lag: {statsRecord.ReplicaLag}, " +
        ///         $"collected at {statsRecord.CollectionTime}");
        /// }
        /// </code>
        /// </example>
        /// <seealso cref="GetReplicaStatsOptions"/>
        public IReadOnlyDictionary<string,IReadOnlyList<ReplicaStatsRecord>>
                StatsRecords { get; internal set; }
    }

    /// <summary>
    /// Cloud Service only.
    /// Instances of this class contain information about replica lag for a
    /// specific replica.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Replica lag is a measure of how current this table is relative to the
    /// remote replica and indicates that this table has not yet received
    /// updates that happened within the lag period.
    /// </para>
    /// <para>
    /// For example, if the replica lag is 5,000 milliseconds (5 seconds), then
    /// this table will have all updates that occurred at the remote replica
    /// that are more than 5 seconds old.
    /// </para>
    /// <para>
    /// Replica lag is calculated based on how long it took for the latest
    /// operation from the table at the remote replica to be replayed at this
    /// table. If there have been no application writes for the table at the
    /// remote replica, the service uses other mechanisms to calculate an
    /// approximation of the lag, and the lag statistic will still be
    /// available.
    /// </para>
    /// </remarks>
    public class ReplicaStatsRecord
    {
        internal ReplicaStatsRecord()
        {
        }

        /// <summary>
        /// Value representing unknown replica lag.
        /// </summary>
        /// <value>
        /// Value of -1 milliseconds.
        /// </value>
        public static readonly TimeSpan UnknownLag =
            TimeSpan.FromMilliseconds(-1);

        /// <summary>
        /// Gets the time the replica stats collection was performed.
        /// </summary>
        /// <value>
        /// Collection time.
        /// </value>
        public DateTime CollectionTime { get; internal set; }

        /// <summary>
        /// Gets the replica lag collected at the specified time.
        /// </summary>
        /// <value>
        /// Replica lag. In rare cases where replica lag could not be
        /// determined, negative value of -1 ms is returned (see
        /// <see cref="UnknownLag"/>).
        /// </value>
        public TimeSpan ReplicaLag { get; internal set; }

        /// <summary>
        /// Returns a string representing this replica stats record.
        /// </summary>
        /// <returns>A string containing property names and values of this
        /// replica stats record.</returns>
        public override string ToString() =>
            $"Collection time: {CollectionTime}, replica lag: {ReplicaLag}";
    }

}
