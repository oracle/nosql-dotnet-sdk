/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Http
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using static HttpConstants;
    using static HttpRequestUtils;
    using static X509Utils;

    internal sealed class Client : IDisposable
    {
        private const string FeaturesKey = "features=";
        private const long FeatureFlagLastWriteMetadata = 1L << 0;

        private readonly Uri dataPathUri = new Uri(NoSQLDataPath,
            UriKind.Relative);

        private readonly NoSQLConfig config;
        private readonly ProtocolHandler protocolHandler;
        private readonly HttpConnectionMetrics connectionMetrics =
            new HttpConnectionMetrics();
        private readonly HttpClient client;
        private int requestId;
        // System.Net.Http active-connection metrics are the primary source
        // for connection stats. This request counter is only a fallback for
        // runtimes where the connection metric is unavailable.
        private int activeHttpExchanges;
        // Does it need to be volatile?
        private int serverSerialVersion;
        private long enabledFeatures;
        private int isFeatureInfoProcessed;

        private static int GetServerSerialVersion(
            HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues(
                HttpConstants.ServerSerialVersion,
                out var values))
            {
                return 0;
            }

            var verStr = values.FirstOrDefault();
            
            if (verStr == null || !int.TryParse(verStr, out var ver))
            {
                return 0;
            }

            return ver;
        }

        internal static int GetRateLimitDelayFromHeader(
            HttpResponseMessage response)
        {
            // The service/proxy reports server-side throttling delay on a
            // successful response using the same header as the Java SDK.
            if (!response.Headers.TryGetValues(RateLimitDelay,
                    out var values))
            {
                return 0;
            }

            var value = values.FirstOrDefault();
            return !string.IsNullOrEmpty(value) &&
                   int.TryParse(value, out var delayMs) ? delayMs : 0;
        }

        internal static long? GetEnabledFeatures(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues(HttpConstants.ServerVersion,
                    out var values))
            {
                return null;
            }

            var versionInfo = values.FirstOrDefault();
            if (versionInfo == null)
            {
                return null;
            }

            var start = versionInfo.IndexOf(FeaturesKey,
                StringComparison.Ordinal);
            if (start < 0)
            {
                return null;
            }

            start += FeaturesKey.Length;
            var end = versionInfo.IndexOf(' ', start);
            var featureValue = end >= 0
                ? versionInfo.Substring(start, end - start)
                : versionInfo.Substring(start);

            return long.TryParse(featureValue,
                    System.Globalization.NumberStyles.HexNumber, null,
                    out var features)
                ? features
                : (long?)null;
        }

        private void UpdateCachedResponseInfo(HttpResponseMessage response,
            bool isFeatureProbe = false)
        {
            if (serverSerialVersion == 0)
            {
                serverSerialVersion = GetServerSerialVersion(response);
            }

            var features = GetEnabledFeatures(response);
            if (features.HasValue)
            {
                Interlocked.Exchange(ref enabledFeatures, features.Value);
                Interlocked.Exchange(ref isFeatureInfoProcessed, 1);
            }
            else if (isFeatureProbe)
            {
                MarkFeaturesUnavailable();
            }
        }

        private void MarkFeaturesUnavailable()
        {
            Interlocked.Exchange(ref enabledFeatures, 0);
            Interlocked.Exchange(ref isFeatureInfoProcessed, 1);
        }

        private static bool IsUnsupportedFeatureProbeResponse(
            HttpStatusCode statusCode) =>
            statusCode == HttpStatusCode.BadRequest ||
            statusCode == HttpStatusCode.NotFound ||
            statusCode == HttpStatusCode.MethodNotAllowed ||
            statusCode == HttpStatusCode.NotImplemented;

        private async Task ExecuteFeatureProbeAsync(Request request,
            CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref isFeatureInfoProcessed, 0,
                    0) != 0)
            {
                return;
            }

            using var message = new HttpRequestMessage(HttpMethod.Head,
                dataPathUri);
            message.Headers.Add(RequestId, Convert.ToString(
                Interlocked.Increment(ref requestId)));

            if (config.AuthorizationProvider != null)
            {
                await config.AuthorizationProvider.ApplyAuthorizationAsync(
                    request, message, cancellationToken);
            }

            if (request.Namespace is var ns && ns != null)
            {
                message.Headers.Add(Namespace, ns);
            }

            using var response = await SendWithTimeoutAsync(client, message,
                request.RequestTimeoutMillis, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                if (IsUnsupportedFeatureProbeResponse(response.StatusCode))
                {
                    UpdateCachedResponseInfo(response, true);
                    return;
                }

                throw await CreateServiceResponseExceptionAsync(response);
            }

            UpdateCachedResponseInfo(response, true);
        }

        private async Task<bool> IsFeatureEnabledAsync(long featureFlag,
            Request request, CancellationToken cancellationToken)
        {
            if (Interlocked.CompareExchange(ref isFeatureInfoProcessed, 0,
                    0) == 0)
            {
                await ExecuteFeatureProbeAsync(request, cancellationToken);
            }

            return (Interlocked.Read(ref enabledFeatures) & featureFlag) != 0;
        }

        private static int GetRemainingTimeoutMillis(DateTime startTime,
            int timeoutMillis)
        {
            var elapsedMillis = (int)(DateTime.UtcNow - startTime)
                .TotalMilliseconds;
            var remainingMillis = timeoutMillis - elapsedMillis;
            if (remainingMillis <= 0)
            {
                throw new TimeoutException(
                    $"Operation timed out after {elapsedMillis} ms");
            }

            return remainingMillis;
        }

        internal int ServerSerialVersion => serverSerialVersion;

        private int AcquiredConnectionCount
        {
            get
            {
                if (connectionMetrics.TryGetActiveConnectionCount(
                        out var activeConnections))
                {
                    // Zero is a valid measured connection count.
                    return activeConnections;
                }

                return Volatile.Read(ref activeHttpExchanges);
            }
        }

        internal static HttpMessageHandler CreateHandler(
            ConnectionOptions connectionOptions,
            HttpConnectionMetrics connectionMetrics = null)
        {
            var handler = new HttpClientHandler();
            if (connectionMetrics != null)
            {
                handler.MeterFactory = connectionMetrics.MeterFactory;
            }

            if (connectionOptions != null &&
                (connectionOptions.TrustedRootCertificates != null ||
                connectionOptions.DisableHostnameVerification))
            {
                handler.ServerCertificateCustomValidationCallback =
                    (request, certificate, chain, errors) =>
                        ValidateCertificate(certificate, chain, errors,
                            connectionOptions);
            }

            return handler;
        }

        internal bool IsRetryableNetworkException(Exception ex)
        {
            return ex is HttpRequestException httpEx &&
                   IsHttpRequestExceptionRetryable(httpEx);
        }

        internal async Task<bool> ValidateRequestFeaturesAsync(Request request,
            CancellationToken cancellationToken)
        {
            if (request is not ILastWriteMetadataRequest metadataRequest ||
                !metadataRequest.HasLastWriteMetadata)
            {
                return false;
            }

            if (!await IsFeatureEnabledAsync(FeatureFlagLastWriteMetadata,
                    request, cancellationToken))
            {
                throw new NotSupportedException(
                    "Last write metadata is not supported by this server");
            }

            return true;
        }

        internal Client(NoSQLConfig config, ProtocolHandler protocolHandler)
        {
            this.config = config;
            this.protocolHandler = protocolHandler;

            client = new HttpClient(CreateHandler(config.ConnectionOptions,
                connectionMetrics), true)
            {
                BaseAddress = config.Uri
            };

            client.DefaultRequestHeaders.Host = config.Uri.Authority;
            client.DefaultRequestHeaders.Connection.Add("keep-alive");
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue(protocolHandler.ContentType));
            // Disable default timeout since we use our own timeout mechanism
            client.Timeout = Timeout.InfiniteTimeSpan;
        }

        internal async Task<object> ExecuteRequestAsync(Request request,
            CancellationToken cancellationToken)
        {
            var startTime = DateTime.UtcNow;
            var timeoutMillis = request.RequestTimeoutMillis;

            using var message = new HttpRequestMessage(HttpMethod.Post,
                dataPathUri);

            timeoutMillis = GetRemainingTimeoutMillis(startTime,
                timeoutMillis);
            request.RequestTimeoutMillis = timeoutMillis;

            using var requestStream = new MemoryStream();
            protocolHandler.StartWrite(requestStream, request);
            request.Serialize(protocolHandler.Serializer, requestStream);
            request.StatsRequestSize = (int)requestStream.Position;

            message.Content = new ByteArrayContent(requestStream.GetBuffer(),
                0, (int)requestStream.Position);
            message.Content.Headers.ContentType = new MediaTypeHeaderValue(
                protocolHandler.ContentType);
            message.Content.Headers.ContentLength = requestStream.Position;

            message.Headers.Add(RequestId, Convert.ToString(
                Interlocked.Increment(ref requestId)));

            // Add authorization headers
            if (config.AuthorizationProvider != null)
            {
                await config.AuthorizationProvider.ApplyAuthorizationAsync(
                    request, message, cancellationToken);
            }

            if (request.Namespace is var ns && ns != null)
            {
                message.Headers.Add(Namespace, ns);
            }

            // Match Java stats semantics: latency starts after serialization
            // and authorization, and ends after the response is deserialized.
            var stopwatch = Stopwatch.StartNew();
            var connectionCount = 0;
            var connectionSampled = false;
            var serverRateLimitDelayMs = 0;
            Interlocked.Increment(ref activeHttpExchanges);
            try
            {
                var buffer = await ExecuteWithTimeoutAsync(
                    async timeoutToken =>
                    {
                        using var response = await client.SendAsync(message,
                            HttpCompletionOption.ResponseHeadersRead,
                            timeoutToken);

                        // Sample while the response still owns an active
                        // connection. A measured zero must remain zero.
                        connectionCount = AcquiredConnectionCount;
                        connectionSampled = true;
                        request.StatsConnectionCount = connectionCount;
                        serverRateLimitDelayMs =
                            GetRateLimitDelayFromHeader(response);

                        if (response.StatusCode != HttpStatusCode.OK)
                        {
                            throw await CreateServiceResponseExceptionAsync(
                                response, timeoutToken);
                        }

                        UpdateCachedResponseInfo(response);

                        // Keep the same timeout active until the complete
                        // response body has been received.
                        return await response.Content.ReadAsByteArrayAsync(
                            timeoutToken);
                    }, timeoutMillis, cancellationToken);

                request.StatsResponseSize = buffer.Length;

                using var responseStream = new MemoryStream(buffer, 0,
                    buffer.Length, false, true);
                protocolHandler.StartRead(responseStream, request);
                var result = request.Deserialize(protocolHandler.Serializer,
                    responseStream);
                request.AddStatsServerRateLimitDelay(
                    serverRateLimitDelayMs);
                stopwatch.Stop();
                request.StatsRequestLatencyMs =
                    Request.ToStatsMilliseconds(stopwatch.Elapsed);
                return result;
            }
            catch
            {
                if (!connectionSampled)
                {
                    // A failure before response headers (for example DNS or
                    // TLS) must not invent a socket from request concurrency.
                    if (connectionMetrics.TryGetActiveConnectionCount(
                            out var activeConnections))
                    {
                        request.StatsConnectionCount = activeConnections;
                    }
                    else
                    {
                        request.StatsConnectionCount = 0;
                    }
                }

                throw;
            }
            finally
            {
                Interlocked.Decrement(ref activeHttpExchanges);
            }
        }

        public void Dispose()
        {
            client.Dispose();
            connectionMetrics.Dispose();
        }
    }
}
