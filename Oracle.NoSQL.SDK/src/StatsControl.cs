/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;

    /// <summary>
    /// Controls collection and periodic reporting of client statistics.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Statistics collection follows the same profile model as the Oracle
    /// NoSQL Java SDK.  The default profile is <see cref="Profile.None"/>,
    /// which means that no statistics are collected unless enabled in
    /// <see cref="NoSQLConfig"/> or by setting a different profile and
    /// calling <see cref="Start"/>.
    /// </para>
    /// <para>
    /// Example:
    /// </para>
    /// <code>
    /// var statsControl = client.GetStatsControl();
    /// statsControl
    ///     .SetProfile(StatsControl.Profile.All)
    ///     .SetPrettyPrint(true)
    ///     .SetStatsHandler(stats => Console.WriteLine(stats));
    /// statsControl.Start();
    /// </code>
    /// </remarks>
    public abstract class StatsControl
    {
        /// <summary>
        /// Prefix used for statistics log messages.
        /// </summary>
        public const string LogPrefix = "Client stats|";

        /// <summary>
        /// Statistics collection profile.
        /// </summary>
        public enum Profile
        {
            /// <summary>
            /// Statistics collection is disabled.
            /// </summary>
            None,

            /// <summary>
            /// Collect request, retry, rate limit, size, latency and
            /// connection statistics.
            /// </summary>
            Regular,

            /// <summary>
            /// Collect regular statistics plus 95th and 99th percentile
            /// request latency.
            /// </summary>
            More,

            /// <summary>
            /// Collect more statistics plus per-query statistics.  Query
            /// statistics may include SQL text and query plans.
            /// </summary>
            All
        }

        /// <summary>
        /// Handler invoked with each generated statistics snapshot.
        /// </summary>
        /// <param name="stats">Statistics snapshot.</param>
        public delegate void StatsHandler(MapValue stats);

        /// <summary>
        /// Gets the configured statistics interval.
        /// </summary>
        /// <returns>Statistics interval.</returns>
        public abstract TimeSpan GetInterval();

        /// <summary>
        /// Sets the configured statistics collection profile. This method
        /// does not itself request that collection start or stop and does not
        /// rebuild an existing statistics aggregator. If <see cref="Start"/>
        /// was previously called while the profile was
        /// <see cref="Profile.None"/>, selecting a collecting profile creates
        /// the aggregator and activates that pending start request. Request
        /// bucket capabilities such as percentile collection are fixed when
        /// the aggregator is first created. Use <see cref="Stop"/> to disable
        /// an already-running collector before setting
        /// <see cref="Profile.None"/>.
        /// </summary>
        /// <param name="profile">Statistics collection profile.</param>
        /// <returns>This instance.</returns>
        public abstract StatsControl SetProfile(Profile profile);

        /// <summary>
        /// Gets the current statistics collection profile.
        /// </summary>
        /// <returns>Current statistics collection profile.</returns>
        public abstract Profile GetProfile();

        /// <summary>
        /// Enables or disables pretty printing of JSON statistics output.
        /// </summary>
        /// <param name="enablePrettyPrint">Pretty-print flag.</param>
        /// <returns>This instance.</returns>
        public abstract StatsControl SetPrettyPrint(bool enablePrettyPrint);

        /// <summary>
        /// Gets the current JSON pretty-print flag.
        /// </summary>
        /// <returns><c>true</c> if pretty printing is enabled.</returns>
        public abstract bool GetPrettyPrint();

        /// <summary>
        /// Sets the handler invoked for generated statistics snapshots.
        /// </summary>
        /// <param name="statsHandler">Statistics handler or <c>null</c>.</param>
        /// <returns>This instance.</returns>
        public abstract StatsControl SetStatsHandler(
            StatsHandler statsHandler);

        /// <summary>
        /// Gets the current statistics handler.
        /// </summary>
        /// <returns>Statistics handler or <c>null</c>.</returns>
        public abstract StatsHandler GetStatsHandler();

        /// <summary>
        /// Starts statistics collection. If the current profile is
        /// <see cref="Profile.None"/>, the start request is remembered but no
        /// aggregator is created and <see cref="IsStarted"/> remains false.
        /// Selecting a collecting profile later activates collection without
        /// another call to this method.
        /// </summary>
        public abstract void Start();

        /// <summary>
        /// Stops collection of new statistics. For parity with the Java SDK,
        /// an existing periodic reporting scheduler remains active and may
        /// report statistics collected before this method was called. Periodic
        /// reporting ends when the owning client is disposed. Collection may
        /// be started again later while the client remains active.
        /// </summary>
        public abstract void Stop();

        /// <summary>
        /// Gets whether statistics collection is currently active.
        /// </summary>
        /// <returns><c>true</c> if statistics collection is active.</returns>
        public abstract bool IsStarted();
    }
}
