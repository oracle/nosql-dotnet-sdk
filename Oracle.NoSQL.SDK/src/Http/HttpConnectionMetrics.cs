/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Http
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Metrics;
    using System.Linq;
    using System.Threading;

    // Tracks per-client active HTTP pool connections. This is closer to
    // Java's acquired Netty channels than counting request concurrency.
    internal sealed class HttpConnectionMetrics : IDisposable
    {
        private const string OpenConnectionsMetric =
            "http.client.open_connections";
        private const string ConnectionStateTag = "http.connection.state";
        private const string ActiveConnectionState = "active";

        private readonly IsolatedMeterFactory meterFactory =
            new IsolatedMeterFactory();
        private readonly MeterListener listener = new MeterListener();
        private long activeConnections;
        private int metricAvailable;

        internal HttpConnectionMetrics()
        {
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (meterFactory.Owns(instrument.Meter) &&
                    instrument.Name == OpenConnectionsMetric)
                {
                    meterListener.EnableMeasurementEvents(instrument);
                    // Availability is independent of the current value. Once
                    // published, zero is a valid active-connection count.
                    Volatile.Write(ref metricAvailable, 1);
                }
            };

            listener.SetMeasurementEventCallback<long>(OnMeasurement);
            listener.SetMeasurementEventCallback<int>(
                (instrument, measurement, tags, state) =>
                    OnMeasurement(instrument, measurement, tags, state));
            listener.Start();
        }

        internal IMeterFactory MeterFactory => meterFactory;

        internal int ActiveConnectionCount
        {
            get
            {
                var count = Volatile.Read(ref activeConnections);
                return count <= 0 ? 0 :
                    count > int.MaxValue ? int.MaxValue : (int)count;
            }
        }

        internal bool TryGetActiveConnectionCount(out int count)
        {
            if (Volatile.Read(ref metricAvailable) == 0)
            {
                count = 0;
                return false;
            }

            count = ActiveConnectionCount;
            return true;
        }

        private void OnMeasurement(Instrument instrument, long measurement,
            ReadOnlySpan<KeyValuePair<string, object>> tags, object state)
        {
            if (!IsActiveConnectionMeasurement(tags))
            {
                return;
            }

            var count = Interlocked.Add(ref activeConnections, measurement);
            if (count < 0)
            {
                Interlocked.Exchange(ref activeConnections, 0);
            }

            Volatile.Write(ref metricAvailable, 1);
        }

        private static bool IsActiveConnectionMeasurement(
            ReadOnlySpan<KeyValuePair<string, object>> tags)
        {
            foreach (var tag in tags)
            {
                if (tag.Key == ConnectionStateTag &&
                    string.Equals(tag.Value?.ToString(),
                        ActiveConnectionState,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public void Dispose()
        {
            listener.Dispose();
            meterFactory.Dispose();
        }

        // SocketsHttpHandler uses this factory to publish its standard
        // System.Net.Http metrics. Each SDK client owns a separate factory,
        // which isolates its connection count without changing the meter name
        // seen by external observability tools.
        private sealed class IsolatedMeterFactory : IMeterFactory
        {
            private readonly object lockObj = new object();
            private readonly List<MeterEntry> entries =
                new List<MeterEntry>();
            private bool disposed;

            public Meter Create(MeterOptions options)
            {
                if (options == null)
                {
                    throw new ArgumentNullException(nameof(options));
                }

                var tags = options.Tags?.ToArray() ??
                    Array.Empty<KeyValuePair<string, object>>();

                lock (lockObj)
                {
                    if (disposed)
                    {
                        throw new ObjectDisposedException(
                            nameof(IsolatedMeterFactory));
                    }

                    var entry = entries.FirstOrDefault(value =>
                        value.Name == options.Name &&
                        value.Version == options.Version &&
                        value.Tags.SequenceEqual(tags));
                    if (entry != null)
                    {
                        return entry.Meter;
                    }

                    var meterOptions = new MeterOptions(options.Name)
                    {
                        Version = options.Version,
                        Tags = tags,
                        Scope = this
                    };
#if NET10_0_OR_GREATER
                    // This property was added after the net8/net9 reference
                    // assemblies. Preserve it where the target supports it.
                    meterOptions.TelemetrySchemaUrl =
                        options.TelemetrySchemaUrl;
#endif
                    var meter = new Meter(meterOptions);
                    entries.Add(new MeterEntry(options.Name,
                        options.Version, tags, meter));
                    return meter;
                }
            }

            internal bool Owns(Meter meter)
            {
                lock (lockObj)
                {
                    return entries.Any(entry =>
                        ReferenceEquals(entry.Meter, meter));
                }
            }

            public void Dispose()
            {
                Meter[] meters;
                lock (lockObj)
                {
                    if (disposed)
                    {
                        return;
                    }

                    disposed = true;
                    meters = entries.Select(entry => entry.Meter).ToArray();
                    entries.Clear();
                }

                foreach (var meter in meters)
                {
                    meter.Dispose();
                }
            }

            private sealed class MeterEntry
            {
                internal MeterEntry(string name, string version,
                    KeyValuePair<string, object>[] tags, Meter meter)
                {
                    Name = name;
                    Version = version;
                    Tags = tags;
                    Meter = meter;
                }

                internal string Name { get; }

                internal string Version { get; }

                internal KeyValuePair<string, object>[] Tags { get; }

                internal Meter Meter { get; }
            }
        }
    }
}
