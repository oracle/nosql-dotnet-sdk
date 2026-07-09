/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Diagnostics;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Logging.Abstractions;
    using static ValidateUtils;

    // Runtime StatsControl implementation.  It owns the lazy Stats aggregator,
    // interval scheduler, logger, and user callback, while request execution
    // only calls Observe/ObserveError/ObserveQuery.
    internal sealed class StatsControlImpl : StatsControl, IDisposable
    {
        private readonly object lockObj = new object();
        private readonly string id = Guid.NewGuid().GetHashCode()
            .ToString("x");
        private readonly bool enableLog;
        private readonly ILogger logger;
        private readonly bool rateLimitingEnabled;
        private volatile Profile profile;
        private volatile bool prettyPrint;
        private volatile StatsHandler statsHandler;
        private volatile bool enableCollection;
        private volatile Stats stats;

        // Serializes scheduled, manual, and shutdown reports so snapshots
        // cannot overlap, matching Java's single-thread stats executor.
        private readonly object statsLogLock = new object();

        // Detects shutdown from inside a StatsHandler so the final report can
        // be deferred until the current callback returns without recursion.
        private readonly AsyncLocal<bool> insideStatsLog =
            new AsyncLocal<bool>();

        // Owns the lifetime of the sequential fixed-rate scheduler.
        private CancellationTokenSource schedulerCancellation;
        private volatile bool disposed;

        // Requests the deferred final report described above.
        private int finalFlushRequested;

        internal StatsControlImpl(NoSQLConfig config,
            bool rateLimitingEnabled)
        {
            profile = config.StatsProfile;
            PercentileStorageMode = config.StatsPercentileMode;
            Interval = config.StatsInterval;
            prettyPrint = config.StatsPrettyPrint;
            enableLog = config.StatsEnableLog;
            statsHandler = config.StatsHandler;
            logger = config.StatsLogger ?? NullLogger.Instance;
            this.rateLimitingEnabled = rateLimitingEnabled;

            if (profile != Profile.None)
            {
                LogStartupSafe();
                Start();
            }
        }

        internal string Id => id;

        internal StatsControl.PercentileMode PercentileStorageMode { get; }

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
            // request-time aggregation or scheduler overhead.
            if (profile != Profile.None && stats == null)
            {
                stats = new Stats(this);
                StartScheduler();
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

        internal MapValue LogClientStatsForTest() => LogClientStatsSafe();

        private void StartScheduler()
        {
            schedulerCancellation = new CancellationTokenSource();

            // The loop owns exception handling and cancellation cleanup, so
            // no scheduler failure can escape into client request execution.
            _ = RunStatsSchedulerAsync(schedulerCancellation);
        }

        private async Task RunStatsSchedulerAsync(
            CancellationTokenSource cancellation)
        {
            var cancellationToken = cancellation.Token;
            try
            {
                await Task.Delay(GetInitialDelay(Interval), cancellationToken)
                    .ConfigureAwait(false);

                /*
                 * Java uses a single-thread scheduled executor with
                 * scheduleAtFixedRate(). Keep one sequential loop here so
                 * snapshots cannot overlap while retaining fixed-rate timing.
                 */
                var intervalTicks = (long)Math.Round(
                    Interval.TotalSeconds * Stopwatch.Frequency);
                var nextRun = Stopwatch.GetTimestamp();

                while (!cancellationToken.IsCancellationRequested)
                {
                    LogClientStatsSafe(cancellationToken);

                    nextRun += intervalTicks;
                    var remainingTicks =
                        nextRun - Stopwatch.GetTimestamp();
                    if (remainingTicks > 0)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(
                                (double)remainingTicks /
                                Stopwatch.Frequency), cancellationToken)
                            .ConfigureAwait(false);
                    }
                }
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                // Normal scheduler shutdown.
            }
            catch
            {
                // Statistics must never affect request execution.
            }
            finally
            {
                lock (lockObj)
                {
                    if (ReferenceEquals(schedulerCancellation,
                            cancellation))
                    {
                        schedulerCancellation = null;
                    }
                }
                cancellation.Dispose();
            }
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

        private void LogStartupSafe()
        {
            try
            {
                LogStartup();
            }
            catch
            {
                // A user-provided logger must never prevent client creation.
            }
        }

        private MapValue LogClientStatsSafe(
            CancellationToken cancellationToken = default)
        {
            lock (statsLogLock)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return null;
                }

                insideStatsLog.Value = true;
                try
                {
                    var snapshot = TryLogClientStats();

                    /*
                     * If shutdown was requested by the StatsHandler itself,
                     * emit the final partial interval after that handler
                     * returns instead of recursively invoking it.
                     */
                    if (Interlocked.Exchange(
                            ref finalFlushRequested, 0) != 0)
                    {
                        TryLogClientStats();
                    }

                    return snapshot;
                }
                finally
                {
                    insideStatsLog.Value = false;
                }
            }
        }

        private MapValue TryLogClientStats()
        {
            try
            {
                return LogClientStats();
            }
            catch
            {
                // Ignore stats failures, matching Java's non-intrusive
                // behavior required for request execution.
                return null;
            }
        }

        private MapValue LogClientStats()
        {
            var snapshot = stats?.GenerateAndClearFieldValueStats(
                DateTime.UtcNow);
            if (snapshot == null)
            {
                return null;
            }

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
            CancellationTokenSource cancellation;
            Stats currentStats;
            lock (lockObj)
            {
                if (disposed)
                {
                    return;
                }

                disposed = true;
                enableCollection = false;
                cancellation = schedulerCancellation;
                schedulerCancellation = null;
                currentStats = stats;
            }

            if (insideStatsLog.Value)
            {
                /*
                 * Shutdown was called from the user StatsHandler. The active
                 * reporting call will perform the final flush on its way out.
                 */
                Interlocked.Exchange(ref finalFlushRequested, 1);
                cancellation?.Cancel();
                return;
            }

            lock (statsLogLock)
            {
                // Match Java Stats.shutdown(): log the remaining interval,
                // then stop the single-thread scheduler.
                if (currentStats != null)
                {
                    TryLogClientStats();
                }
                cancellation?.Cancel();
            }
        }

        public void Dispose()
        {
            Shutdown();
        }
    }
}
