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
    using System.Threading;

    // Tracks per-client active HTTP pool connections. This is closer to
    // Java's acquired Netty channels than counting request concurrency.
    internal sealed class HttpConnectionMetrics : IDisposable
    {
        private const string OpenConnectionsMetric =
            "http.client.open_connections";
        private const string ConnectionStateTag = "http.connection.state";
        private const string ActiveConnectionState = "active";

        private readonly SingleMeterFactory meterFactory =
            new SingleMeterFactory();
        private readonly MeterListener listener = new MeterListener();
        private long activeConnections;

        internal HttpConnectionMetrics()
        {
            listener.InstrumentPublished = (instrument, meterListener) =>
            {
                if (ReferenceEquals(instrument.Meter, meterFactory.Meter) &&
                    instrument.Name == OpenConnectionsMetric)
                {
                    meterListener.EnableMeasurementEvents(instrument);
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

        private sealed class SingleMeterFactory : IMeterFactory
        {
            internal SingleMeterFactory()
            {
                Meter = new Meter("Oracle.NoSQL.SDK.Http." +
                    Guid.NewGuid().ToString("N"));
            }

            internal Meter Meter { get; }

            public Meter Create(MeterOptions options) => Meter;

            public void Dispose()
            {
                Meter.Dispose();
            }
        }
    }
}
