/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using static ValidateUtils;

    /// <summary>
    /// On-Premise only.
    /// SyncPolicy defines the synchronization policy to be used when
    /// committing a transaction.
    /// </summary>
    /// <remarks>
    /// High levels of synchronization offer a greater guarantee that the
    /// transaction is persistent to disk, but trade that off for lower
    /// performance.
    /// </remarks>
    /// <seealso cref="Durability"/>
    public enum SyncPolicy
    {
        /// <summary>
        /// Write and synchronously flush the log on transaction commit.
        /// Transactions exhibit all the ACID (atomicity, consistency,
        /// isolation, and durability) properties.
        /// </summary>
        Sync = 0,

        /// <summary>
        /// Do not write or synchronously flush the log on transaction commit.
        /// Transactions exhibit the ACI (atomicity, consistency, and
        /// isolation) properties, but not D (durability); that is, database
        /// integrity will be maintained, but if the application or system
        /// fails, it is possible some number of the most recently committed
        /// transactions may be undone during recovery. The number of
        /// transactions at risk is governed by how many log updates can fit
        /// into the log buffer, how often the operating system flushes dirty
        /// buffers to disk, and how often log checkpoints occur.
        /// </summary>
        NoSync = 1,

        /// <summary>
        /// Write but do not synchronously flush the log on transaction
        /// commit.  Transactions exhibit the ACI (atomicity, consistency, and
        /// isolation) properties, but not D (durability); that is, database
        /// integrity will be maintained, but if the operating system fails,
        /// it is possible some number of the most recently committed
        /// transactions may be undone during recovery. The number of
        /// transactions at risk is governed by how often the operating system
        /// flushes dirty buffers to disk, and how often log checkpoints
        /// occur.
        /// </summary>
        WriteNoSync = 2
    }

    /// <summary>
    /// On-Premise only.
    /// ReplicaAckPolicy defines the policy for how commits are handled in a
    /// replicated environment.
    /// </summary>
    /// <remarks>
    /// A replicated environment makes it possible to increase an
    /// application's transaction commit guarantees by committing changes to
    /// its replicas on the network. ReplicaAckPolicy defines the policy for
    /// how such network commits are handled.
    /// </remarks>
    /// <seealso cref="Durability"/>
    public enum ReplicaAckPolicy {

        /// <summary>
        /// All replicas must acknowledge that they have committed the
        /// transaction. This policy should be selected only if your
        /// replication group has a small number of replicas, and those
        /// replicas are on extremely reliable networks and servers.
        /// </summary>
        All = 0,

        /// <summary>
        /// No transaction commit acknowledgments are required and the master
        /// will never wait for replica acknowledgments. In this case,
        /// transaction durability is determined entirely by the type of
        /// commit that is being performed on the master.
        /// </summary>
        None = 1,

        /// <summary>
        /// A simple majority of replicas must acknowledge that they have
        /// committed the transaction. This acknowledgment policy, in
        /// conjunction with an election policy which requires at least a
        /// simple majority, ensures that the changes made by the transaction
        /// remain durable if a new election is held.
        /// </summary>
        SimpleMajority = 2
    }

    /// <summary>
    /// On-Premise only.
    /// Durability specifies the master and replica sync and ack policies to
    /// be used for a write operation.
    /// </summary>
    /// <remarks>
    /// Durability applies to Put operations, executed by
    /// <see cref="NoSQLClient.PutAsync"/> and its variants, Delete
    /// operations, executed by <see cref="NoSQLClient.DeleteAsync"/> and its
    /// variants, DeleteRange operations executed by
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.DeleteRangeAsync*"/> and its
    /// variants, WriteMany operations executed by
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.WriteManyAsync*"/> and its
    /// variants and Query operations executed by
    /// <see cref="M:Oracle.NoSQL.SDK.NoSQLClient.QueryAsync*"/> and its
    /// variants for update queries.
    /// </remarks>
    /// <seealso cref="PutOptions.Durability"/>
    /// <seealso cref="DeleteOptions.Durability"/>
    /// <seealso cref="DeleteRangeOptions.Durability"/>
    /// <seealso cref="WriteManyOptions.Durability"/>
    /// <seealso cref="QueryOptions.Durability"/>
    public readonly struct Durability : IEquatable<Durability>
    {
        // Save some space.
        private readonly byte masterSync;
        private readonly byte replicaSync;
        private readonly byte replicaAck;

        /// <summary>
        /// Initializes a new instance of <see cref="Durability"/> structure
        /// using specified Master and Replica sync policies and Replica
        /// acknowledgement policy.
        /// </summary>
        /// <param name="masterSync">
        /// The <see cref="SyncPolicy"/> to be used when committing the
        /// transaction on the master node.
        /// </param>
        /// <param name="replicaSync">
        /// The <see cref="SyncPolicy"/> to be used remotely, as part of a
        /// transaction acknowledgment at a Replica node.
        /// </param>
        /// <param name="replicaAck">
        /// The acknowledgment policy used when obtaining transaction
        /// acknowledgments from Replicas.
        /// </param>
        public Durability(SyncPolicy masterSync, SyncPolicy replicaSync,
            ReplicaAckPolicy replicaAck)
        {
            this.masterSync = (byte)masterSync;
            this.replicaSync = (byte)replicaSync;
            this.replicaAck = (byte)replicaAck;
        }

        /// <summary>
        /// Represents a durability policy with <see cref="SyncPolicy.Sync"/>
        /// for Master commit synchronization.
        /// </summary>
        /// <remarks>
        /// This policy specifies <see cref="SyncPolicy.NoSync"/> for
        /// commits of replicated transactions that need acknowledgment and
        /// <see cref="ReplicaAckPolicy.SimpleMajority"/> for the
        /// acknowledgment policy.
        /// </remarks>
        public static readonly Durability CommitSync = new Durability(
            SyncPolicy.Sync, SyncPolicy.NoSync,
            ReplicaAckPolicy.SimpleMajority);

        /// <summary>
        /// Represents a durability policy with
        /// <see cref="SyncPolicy.NoSync"/> for Master commit synchronization.
        /// </summary>
        /// <remarks>
        /// This policy specifies <see cref="SyncPolicy.NoSync"/> for
        /// commits of replicated transactions that need acknowledgment and
        /// <see cref="ReplicaAckPolicy.SimpleMajority"/> for the
        /// acknowledgment policy.
        /// </remarks>
        public static readonly Durability CommitNoSync = new Durability(
            SyncPolicy.NoSync, SyncPolicy.NoSync,
            ReplicaAckPolicy.SimpleMajority);

        /// <summary>
        /// Represents a durability policy with
        /// <see cref="SyncPolicy.WriteNoSync"/> for Master commit
        /// synchronization.
        /// </summary>
        /// <remarks>
        /// This policy specifies <see cref="SyncPolicy.NoSync"/> for
        /// commits of replicated transactions that need acknowledgment and
        /// <see cref="ReplicaAckPolicy.SimpleMajority"/> for the
        /// acknowledgment policy.
        /// </remarks>
        public static readonly Durability CommitWriteNoSync = new Durability(
            SyncPolicy.WriteNoSync, SyncPolicy.NoSync,
            ReplicaAckPolicy.SimpleMajority);

        /// <summary>
        /// Gets the transaction synchronization policy to be used on the
        /// Master node when committing a transaction.
        /// </summary>
        /// <value>
        /// The master transaction synchronization policy.
        /// </value>
        public SyncPolicy MasterSync => (SyncPolicy)masterSync;

        /// <summary>
        /// Gets the transaction synchronization policy to be used by the
        /// Replica node as it replays a transaction that needs an
        /// acknowledgment.
        /// </summary>
        /// <value>
        /// The replica transaction synchronization policy.
        /// </value>
        public SyncPolicy ReplicaSync => (SyncPolicy)replicaSync;

        /// <summary>
        /// Gets the replica acknowledgment policy used by the master when
        /// committing changes to a replicated environment.
        /// </summary>
        /// <value>
        /// The replica acknowledgment policy.
        /// </value>
        public ReplicaAckPolicy ReplicaAck => (ReplicaAckPolicy)replicaAck;

        /// <inheritdoc/>
        public override bool Equals(object obj) =>
            obj is Durability other && Equals(other);

        /// <summary>
        /// Returns a value indicating whether the value of this instance is
        /// equal to the value of the specified <see cref="Durability"/>
        /// instance.
        /// </summary>
        /// <param name="value">The value to compare to this instance.</param>
        /// <returns>
        /// <c>true</c> if the <paramref name="value"/> equals the value of
        /// this instance, otherwise <c>false</c>.
        /// </returns>
        public bool Equals(Durability value) =>
            MasterSync == value.MasterSync &&
            ReplicaSync == value.ReplicaSync &&
            ReplicaAck == value.ReplicaAck;

        /// <inheritdoc/>
        public override int GetHashCode() =>
            (MasterSync, ReplicaSync, ReplicaAck).GetHashCode();

        /// <summary>
        /// Determines whether two instances of <see cref="Durability"/> are
        /// equal.
        /// </summary>
        /// <param name="d1">First value to compare.</param>
        /// <param name="d2">Second value to compare.</param>
        /// <returns>
        /// <c>true</c> if the values are equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator ==(Durability d1, Durability d2) =>
            d1.Equals(d2);

        /// <summary>
        /// Determines whether two instances of <see cref="Durability"/> are
        /// not equal.
        /// </summary>
        /// <param name="d1">First value to compare.</param>
        /// <param name="d2">Second value to compare.</param>
        /// <returns>
        /// <c>true</c> if the values are not equal, otherwise <c>false</c>.
        /// </returns>
        public static bool operator !=(Durability d1, Durability d2) =>
            !(d1 == d2);

        internal void Validate()
        {
            CheckEnumValue(MasterSync);
            CheckEnumValue(ReplicaSync);
            CheckEnumValue(ReplicaAck);
        }
    }

}
