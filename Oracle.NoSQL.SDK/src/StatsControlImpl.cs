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
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Extensions.Logging.Abstractions;
    using static ValidateUtils;

    // Coordinates StatsControl lifecycle and interval reporting. Aggregation
    // belongs to IStatsCollector and output belongs to IStatsExporter, while
    // request execution only calls Observe/ObserveError/ObserveQuery.
    internal sealed class StatsControlImpl : StatsControl, IDisposable
    {
        private readonly object lockObj = new object();
        private readonly string id = Guid.NewGuid().GetHashCode()
            .ToString("x");
        private readonly bool rateLimitingEnabled;
        private readonly JavaCompatibleMapValueExporter mapValueExporter;
        private readonly LoggerStatsExporter loggerExporter;
        private readonly IStatsExporter[] exporters;
        private volatile Profile profile;
        private volatile bool prettyPrint;
        private volatile StatsHandler statsHandler;
        private volatile bool enableCollection;
        private volatile IStatsCollector stats;

        // Serializes scheduled, manual, and shutdown reports so snapshots
        // cannot overlap, matching Java's single-thread stats executor.
        private readonly object statsLogLock = new object();

        /*
         * Detects shutdown from inside a StatsHandler so the final report can
         * be deferred until the current callback returns without recursion.
         * The shared scope expires when reporting ends, which prevents an
         * inherited ExecutionContext in a later task from looking active.
         */
        private readonly AsyncLocal<StatsLogScope> statsLogScope =
            new AsyncLocal<StatsLogScope>();

        // Owns the lifetime of the sequential fixed-rate scheduler.
        private CancellationTokenSource schedulerCancellation;
        private volatile bool disposed;

        internal StatsControlImpl(NoSQLConfig config,
            bool rateLimitingEnabled)
        {
            profile = config.StatsProfile;
            Interval = config.StatsInterval;
            prettyPrint = config.StatsPrettyPrint;
            statsHandler = config.StatsHandler;
            this.rateLimitingEnabled = rateLimitingEnabled;
            mapValueExporter = new JavaCompatibleMapValueExporter();
            loggerExporter = new LoggerStatsExporter(
                config.StatsEnableLog,
                config.StatsLogger ?? NullLogger.Instance,
                () => prettyPrint,
                mapValueExporter);
            exporters = new IStatsExporter[]
            {
                loggerExporter,
                new HandlerStatsExporter(() => statsHandler,
                    mapValueExporter)
            };

            if (profile != Profile.None)
            {
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
                this.profile = profile;
                stats?.UpdateProfile(profile);

                // Start() may have been requested while the profile was NONE.
                // Activate that pending request as soon as a collecting
                // profile is selected, without rebuilding existing stats.
                if (enableCollection)
                {
                    EnsureStats();
                }
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

                enableCollection = true;
                EnsureStats();
            }
        }

        public override void Stop()
        {
            enableCollection = false;
        }

        public override bool IsStarted() =>
            enableCollection && stats != null && !disposed;

        private void EnsureStats()
        {
            // Stats is created lazily so the default NONE profile has no
            // request-time aggregation or scheduler overhead.
            if (profile != Profile.None && stats == null)
            {
                stats = new Stats(id, profile);
                // Emit startup metadata when collection actually becomes
                // active, including when it is enabled after construction.
                LogStartupSafe();
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
                var collector = stats;
                if (collector == null)
                {
                    return;
                }
                var observation = StatsObservation.FromRequest(request,
                    false, collector.IncludesQueryDetails);
                collector.Record(observation);
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
                var collector = stats;
                if (collector == null)
                {
                    return;
                }
                var observation = StatsObservation.FromRequest(request,
                    true, collector.IncludesQueryDetails);
                collector.Record(observation);
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
                var collector = stats;
                if (collector == null ||
                    !collector.IncludesQueryDetails)
                {
                    return;
                }

                var observation = StatsObservation.FromQuery(queryRequest);
                collector.Record(observation);
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
                return mapValueExporter.Export(
                    stats?.Snapshot(DateTime.UtcNow));
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

        private void LogStartupSafe()
        {
            try
            {
                loggerExporter.LogStartup(id, profile, Interval,
                    prettyPrint, rateLimitingEnabled);
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

                var previousScope = statsLogScope.Value;
                var currentScope = new StatsLogScope();
                statsLogScope.Value = currentScope;
                MapValue snapshot = null;
                var finalFlush = false;
                try
                {
                    snapshot = TryLogClientStats();
                }
                finally
                {
                    /*
                     * Expiring and reading the deferred-flush request is one
                     * atomic transition. Shutdown either leaves the request
                     * here or observes an expired scope and performs the
                     * flush itself after acquiring statsLogLock.
                     */
                    finalFlush = currentScope.ExpireAndTakeFinalFlush();
                    statsLogScope.Value = previousScope;
                }

                if (finalFlush)
                {
                    TryLogClientStats();
                }

                return snapshot;
            }
        }

        private MapValue TryLogClientStats()
        {
            try
            {
                return LogClientStats();
            }
            catch (Exception ex)
            {
                // Match Java's non-intrusive behavior while retaining a
                // diagnostic when the configured logger is available.
                loggerExporter.LogStatsError(ex);
                return null;
            }
        }

        private MapValue LogClientStats()
        {
            var immutableSnapshot = stats?.Rotate(DateTime.UtcNow);
            if (immutableSnapshot == null)
            {
                return null;
            }

            // Each exporter receives the immutable snapshot and creates its
            // own compatibility view, isolating logger and handler output.
            foreach (var exporter in exporters)
            {
                try
                {
                    exporter.Export(immutableSnapshot);
                }
                catch (Exception ex)
                {
                    // Exporters remain isolated from one another and from
                    // requests, but failures are visible when logging is on.
                    loggerExporter.LogStatsError(ex);
                }
            }

            return mapValueExporter.Export(immutableSnapshot);
        }

        internal void Shutdown()
        {
            CancellationTokenSource cancellation;
            IStatsCollector currentStats;
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

            var currentScope = statsLogScope.Value;
            if (currentScope?.TryRequestFinalFlush() == true)
            {
                /*
                 * Shutdown was called from the user StatsHandler. The active
                 * reporting call will perform the final flush on its way out.
                 */
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

        private sealed class StatsLogScope
        {
            private const int Active = 0;
            private const int FlushRequested = 1;
            private const int Expired = 2;
            private int state;

            internal bool TryRequestFinalFlush()
            {
                while (true)
                {
                    var current = Volatile.Read(ref state);
                    if (current == Expired)
                    {
                        return false;
                    }
                    if (current == FlushRequested)
                    {
                        return true;
                    }
                    if (Interlocked.CompareExchange(ref state,
                            FlushRequested, Active) == Active)
                    {
                        return true;
                    }
                }
            }

            internal bool ExpireAndTakeFinalFlush() =>
                Interlocked.Exchange(ref state, Expired) == FlushRequested;
        }
    }
}
