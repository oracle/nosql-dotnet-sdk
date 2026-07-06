/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Reflection;
    using System.Threading;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using static ValidateUtils;

    // Runtime StatsControl implementation.  It owns the lazy Stats aggregator,
    // interval timer, logger, and user callback, while request execution only
    // calls Observe/ObserveError/ObserveQuery.
    internal sealed class StatsControlImpl : StatsControl, IDisposable
    {
        private readonly object lockObj = new object();
        private readonly string id = Guid.NewGuid().GetHashCode()
            .ToString("x");
        private readonly bool enableLog;
        private readonly ILogger logger;
        private readonly bool rateLimitingEnabled;
        private Profile profile;
        private bool prettyPrint;
        private StatsHandler statsHandler;
        private bool enableCollection;
        private Stats stats;
        private Timer timer;
        private bool disposed;

        internal StatsControlImpl(NoSQLConfig config,
            bool rateLimitingEnabled)
        {
            profile = config.StatsProfile;
            Interval = config.StatsInterval;
            prettyPrint = config.StatsPrettyPrint;
            enableLog = config.StatsEnableLog;
            statsHandler = config.StatsHandler;
            logger = config.StatsLogger ?? NullLogger.Instance;
            this.rateLimitingEnabled = rateLimitingEnabled;

            if (profile != Profile.None)
            {
                LogStartup();
                Start();
            }
        }

        internal string Id => id;

        internal TimeSpan Interval { get; }

        public override TimeSpan GetInterval() => Interval;

        public override StatsControl SetProfile(Profile profile)
        {
            CheckEnumValue(profile);
            lock (lockObj)
            {
                // Match Java: setProfile changes the configured profile but
                // does not start, stop, or rebuild an existing Stats object.
                this.profile = profile;
            }
            return this;
        }

        public override Profile GetProfile() => profile;

        public override StatsControl SetPrettyPrint(bool enablePrettyPrint)
        {
            prettyPrint = enablePrettyPrint;
            return this;
        }

        public override bool GetPrettyPrint() => prettyPrint;

        public override StatsControl SetStatsHandler(
            StatsHandler statsHandler)
        {
            this.statsHandler = statsHandler;
            return this;
        }

        public override StatsHandler GetStatsHandler() => statsHandler;

        public override void Start()
        {
            lock (lockObj)
            {
                if (disposed)
                {
                    return;
                }

                EnsureStats();
                enableCollection = true;
            }
        }

        public override void Stop()
        {
            enableCollection = false;
        }

        public override bool IsStarted() => enableCollection && !disposed;

        private void EnsureStats()
        {
            // Stats is created lazily so the default NONE profile has no
            // request-time aggregation or timer overhead.
            if (profile != Profile.None && stats == null)
            {
                stats = new Stats(this);
                StartTimer();
            }
        }

        private bool ShouldCollect() =>
            !disposed && enableCollection && stats != null;

        internal void Observe(Request request)
        {
            if (!ShouldCollect())
            {
                return;
            }

            try
            {
                stats?.Observe(request, false);
            }
            catch
            {
                // Statistics must never affect request execution.
            }
        }

        internal void ObserveError(Request request)
        {
            if (!ShouldCollect())
            {
                return;
            }

            try
            {
                stats?.Observe(request, true);
            }
            catch
            {
                // Statistics must never affect request execution.
            }
        }

        internal void ObserveQuery(QueryRequest queryRequest)
        {
            if (!ShouldCollect())
            {
                return;
            }

            try
            {
                stats?.ObserveQuery(queryRequest);
            }
            catch
            {
                // Statistics must never affect request execution.
            }
        }

        internal MapValue GenerateStats()
        {
            try
            {
                return stats?.GenerateFieldValueStats(DateTime.UtcNow);
            }
            catch
            {
                return null;
            }
        }

        internal MapValue LogClientStatsForTest() => LogClientStats();

        private void StartTimer()
        {
            timer?.Dispose();
            timer = new Timer(_ => LogClientStatsSafe(), null,
                GetInitialDelay(Interval), Interval);
        }

        private static TimeSpan GetInitialDelay(TimeSpan interval)
        {
            // Align periodic snapshots to wall-clock interval boundaries, as
            // Java Stats does. The first interval may therefore be shorter.
            var intervalMs = Math.Max(1L,
                (long)Math.Round(interval.TotalMilliseconds));
            var now = DateTime.Now;
            var elapsedMs =
                now.Minute * 60_000L +
                now.Second * 1_000L +
                now.Millisecond;
            var delayMs = intervalMs - elapsedMs % intervalMs;
            return TimeSpan.FromMilliseconds(delayMs);
        }

        private static string GetLibraryVersion()
        {
            return typeof(NoSQLClient).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                ?.InformationalVersion ??
                typeof(NoSQLClient).Assembly.GetName().Version?.ToString() ??
                string.Empty;
        }

        private void LogStartup()
        {
            if (!enableLog)
            {
                return;
            }

            var startup = new MapValue
            {
                ["sdkName"] = "Oracle NoSQL SDK for .NET",
                ["sdkVersion"] = GetLibraryVersion(),
                ["clientId"] = id,
                ["profile"] = profile.ToString().ToUpperInvariant(),
                ["intervalSec"] = (int)Interval.TotalSeconds,
                ["prettyPrint"] = prettyPrint,
                ["rateLimitingEnabled"] = rateLimitingEnabled
            };

            logger.LogInformation("{StatsLog}",
                LogPrefix + startup.ToJsonString());
        }

        private void LogClientStatsSafe()
        {
            try
            {
                LogClientStats();
            }
            catch
            {
                // Ignore stats failures, matching the non-intrusive
                // behavior required for request execution.
            }
        }

        private MapValue LogClientStats()
        {
            var snapshot = stats?.GenerateFieldValueStats(DateTime.UtcNow);
            if (snapshot == null)
            {
                return null;
            }

            stats?.ClearStats();

            try
            {
                // The handler is the .NET equivalent of Java's StatsHandler
                // callback: applications receive the snapshot at interval end.
                statsHandler?.Invoke(snapshot);
            }
            catch
            {
                // User handlers must not prevent periodic stats logging or
                // interfere with request execution.
            }

            if (enableLog && snapshot != null)
            {
                logger.LogInformation("{StatsLog}",
                    LogPrefix + snapshot.ToJsonString(
                        prettyPrint ? new JsonOutputOptions
                        {
                            Indented = true
                        } : null));
            }

            return snapshot;
        }

        internal void Shutdown()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            enableCollection = false;
            timer?.Dispose();
            if (stats != null)
            {
                LogClientStatsSafe();
            }
        }

        public void Dispose()
        {
            Shutdown();
        }
    }
}
