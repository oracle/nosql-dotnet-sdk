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
    using System.Diagnostics;
    using System.Threading;
    using System.Threading.Tasks;

    internal class RateLimiterEntry
    {
        internal IRateLimiter ReadRateLimiter { get; set; }

        internal IRateLimiter WriteRateLimiter { get; set; }

        internal int ReadUnits { get; set; }
        
        internal int WriteUnits { get; set; }
    }

    internal sealed class RateLimitingHandler : IDisposable
    {
        // Pre-set limits for On-Demand tables.
        private const int OnDemandReadUnits = 10000;
        private const int OnDemandWriteUnits = 5000;

        // Check table limits in background every 10 minutes.
        private static readonly TimeSpan BackgroundCheckInterval =
            TimeSpan.FromMinutes(10);

        private static readonly GetTableOptions BackgroundGetTableOptions =
            new GetTableOptions
            {
                // Allow enough timeout for multiple retries.
                Timeout = TimeSpan.FromMinutes(5)
            };

        private readonly NoSQLClient client;
        private readonly Func<IRateLimiter> rateLimiterCreator;
        private readonly double? rateLimiterRatio;
        private readonly Dictionary<string, RateLimiterEntry> rateLimiterMap =
            new Dictionary<string, RateLimiterEntry>();
        private readonly Dictionary<string, CancellationTokenSource>
            rateLimiterUpdateMap =
                new Dictionary<string, CancellationTokenSource>();
        private readonly object lockObj = new object();

        internal RateLimitingHandler(NoSQLClient client)
        {
            this.client = client;
            var config = client.Config;
            Debug.Assert(config.RateLimitingEnabled);
            rateLimiterCreator = config.RateLimiterCreator ??
                                 (() => new NoSQLRateLimiter());
            if (config.RateLimiterPercent.HasValue)
            {
                Debug.Assert(config.RateLimiterPercent > 0 &&
                             config.RateLimiterPercent <= 100);
                rateLimiterRatio = config.RateLimiterPercent / 100;
            }
        }

        internal static bool IsRateLimitingEnabled(NoSQLConfig config) =>
            config.RateLimitingEnabled &&
            config.ServiceType != ServiceType.KVStore;

        internal static void ValidateConfig(NoSQLConfig config)
        {
            if (config.RateLimiterPercent.HasValue && (
                    config.RateLimiterPercent <= 0 ||
                    config.RateLimiterPercent > 100))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(config.RateLimiterPercent), 
                    "RateLimiterPercent must be > 0 and <= 100, got " +
                        config.RateLimiterPercent);
            }
        }

        private void SetLimit(IRateLimiter rateLimiter, int units)
        {
            rateLimiter.SetLimit(rateLimiterRatio.HasValue ?
                rateLimiterRatio.Value * units : units);
        }

        private IRateLimiter CreateRateLimiter(int units)
        {
            var rateLimiter = rateLimiterCreator();
            SetLimit(rateLimiter, units);
            return rateLimiter;
        }

        private void RemoveLimiters(string tableNameLower)
        {
            lock (lockObj)
            {
                rateLimiterMap.Remove(tableNameLower);
                if (rateLimiterUpdateMap.Remove(tableNameLower, out var cts))
                {
                    cts.Cancel();
                }
            }
        }

        private void DoUpdateLimiters(string tableNameLower,
            TableResult tableResult)
        {
            if (tableResult.TableState == TableState.Dropped)
            {
                RemoveLimiters(tableNameLower);
                return;
            }

            if (tableResult.TableState != TableState.Active)
            {
                return;
            }

            Debug.Assert(tableResult.TableLimits != null);

            if (!rateLimiterMap.TryGetValue(tableNameLower, out var entry))
            {
                int readUnits;
                int writeUnits;
                if (tableResult.TableLimits.CapacityMode ==
                    CapacityMode.Provisioned)
                {
                    readUnits = tableResult.TableLimits.ReadUnits;
                    writeUnits = tableResult.TableLimits.WriteUnits;
                }
                else
                {
                    readUnits = OnDemandReadUnits;
                    writeUnits = OnDemandWriteUnits;
                }

                entry = new RateLimiterEntry
                {
                    ReadUnits = readUnits,
                    WriteUnits = writeUnits,
                    ReadRateLimiter = CreateRateLimiter(readUnits),
                    WriteRateLimiter = CreateRateLimiter(writeUnits)
                };
                rateLimiterMap.Add(tableNameLower, entry);
            }
            else
            {
                if (entry.ReadUnits != tableResult.TableLimits.ReadUnits)
                {
                    entry.ReadUnits = tableResult.TableLimits.ReadUnits;
                    SetLimit(entry.ReadRateLimiter, entry.ReadUnits);
                }
                if (entry.WriteUnits != tableResult.TableLimits.WriteUnits)
                {
                    entry.WriteUnits = tableResult.TableLimits.WriteUnits;
                    SetLimit(entry.WriteRateLimiter, entry.WriteUnits);
                }
            }
        }

        // We pass tableResult as null if exception occurred during
        // GetTableAsync.
        private void UpdateLimiters(string tableNameLower,
            TableResult tableResult)
        {
            lock (lockObj)
            {
                if (rateLimiterUpdateMap.TryGetValue(tableNameLower,
                        out var cts))
                {
                    cts.Cancel();
                }

                cts = new CancellationTokenSource();
                // Set the value regardless so that we don't launch background
                // update again if not needed.
                rateLimiterUpdateMap[tableNameLower] = cts;

                if (tableResult != null)
                {
                    DoUpdateLimiters(tableNameLower, tableResult);
                }

                // Keep checking table limits at regular interval
                // BackgroundCheckInterval if previous check resulted in
                // exception or if using multiple clients each using portion
                // of table limits. The latter heuristic is to tell us that
                // the table limits may be updated independently of this
                // NoSQLClient instance.
                if (tableResult == null || rateLimiterRatio.HasValue)
                {
                    Task.Run(async () =>
                    {
                        await Task.Delay(BackgroundCheckInterval, cts.Token);
                        await DoBackgroundUpdate(tableNameLower, cts.Token);
                    }, cts.Token);
                }
            }
        }

        private async Task DoBackgroundUpdate(string tableNameLowerCase,
            CancellationToken cancellationToken)
        {
            try
            {
                var tableResult = await client.GetTableAsync(
                    tableNameLowerCase, BackgroundGetTableOptions,
                    cancellationToken);
                UpdateLimiters(tableNameLowerCase, tableResult);
            }
            catch (TableNotFoundException)
            {
                RemoveLimiters(tableNameLowerCase);
            }
            catch (Exception)
            {
                UpdateLimiters(tableNameLowerCase, null);
            }
        }

        internal RateLimiterEntry InitRateLimiterEntry(Request request)
        {
            var tableName = request.InternalTableName;
            if (tableName == null)
            {
                return null;
            }

            tableName = tableName.ToLowerInvariant();

            lock (lockObj)
            {
                if (rateLimiterMap.TryGetValue(tableName, out var entry))
                {
                    return entry;
                }

                if (!rateLimiterUpdateMap.ContainsKey(tableName))
                {
                    var cts = new CancellationTokenSource();
                    rateLimiterUpdateMap.Add(tableName, cts);
                    Task.Run(() => DoBackgroundUpdate(tableName, cts.Token),
                        cts.Token);
                }

                return null;
            }
        }

        internal void ApplyTableResult(TableResult result)
        {
            if (result.TableName == null)
            {
                throw new BadProtocolException(
                    "Missing table name in TableResult");
            }

            UpdateLimiters(result.TableName.ToLowerInvariant(), result);
        }

        public void Dispose()
        {
            foreach (var cts in rateLimiterUpdateMap.Values)
            {
                cts.Cancel();
            }

            rateLimiterUpdateMap.Clear();
            rateLimiterMap.Clear();
        }

    }
}
