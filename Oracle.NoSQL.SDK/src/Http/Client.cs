/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
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
        // runtimes or sampling points where those metrics have not reported
        // a positive value.
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

            var message = new HttpRequestMessage(HttpMethod.Head, dataPathUri);
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

            var response = await SendWithTimeoutAsync(client, message,
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
                var activeConnections =
                    connectionMetrics.ActiveConnectionCount;
                return activeConnections > 0 ? activeConnections :
                    Volatile.Read(ref activeHttpExchanges);
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

            if (request is ILastWriteMetadataRequest metadataRequest &&
                metadataRequest.HasLastWriteMetadata &&
                !await IsFeatureEnabledAsync(FeatureFlagLastWriteMetadata,
                    request, cancellationToken))
            {
                throw new NotSupportedException(
                    "Last write metadata is not supported by this server");
            }

            var message = new HttpRequestMessage(HttpMethod.Post,
                dataPathUri);

            timeoutMillis = GetRemainingTimeoutMillis(startTime,
                timeoutMillis);
            request.RequestTimeoutMillis = timeoutMillis;

            var stream = new MemoryStream();
            protocolHandler.StartWrite(stream, request);
            request.Serialize(protocolHandler.Serializer, stream);
            request.StatsRequestSize = (int)stream.Position;

            message.Content = new ByteArrayContent(stream.GetBuffer(), 0,
                (int)stream.Position);
            message.Content.Headers.ContentType = new MediaTypeHeaderValue(
                protocolHandler.ContentType);
            message.Content.Headers.ContentLength = stream.Position;

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
            // and authorization, and ends after the full response body is read.
            var stopwatch = Stopwatch.StartNew();
            Interlocked.Increment(ref activeHttpExchanges);
            try
            {
                var response = await SendWithTimeoutAsync(client, message,
                    timeoutMillis, cancellationToken);
                var connectionCount = AcquiredConnectionCount;
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    request.StatsConnectionCount = connectionCount;
                    throw await CreateServiceResponseExceptionAsync(response);
                }

                UpdateCachedResponseInfo(response);

                // The stream returned by ReadAsStreamAsync(), even though it is
                // usually a MemoryStream, doesn't allow access to the buffer
                // via MemoryStream.GetBuffer() which is needed for
                // deserialization, so we have to use ReadAsByteArrayAsync().
                var buffer = await response.Content.ReadAsByteArrayAsync();
                stopwatch.Stop();
                request.StatsResponseSize = buffer.Length;
                request.StatsRequestLatencyMs =
                    Request.ToStatsMilliseconds(stopwatch.Elapsed);
                request.StatsConnectionCount = connectionCount > 0 ?
                    connectionCount : AcquiredConnectionCount;

                stream = new MemoryStream(buffer, 0, buffer.Length, false,
                    true);
                protocolHandler.StartRead(stream, request);
                return request.Deserialize(protocolHandler.Serializer,
                    stream);
            }
            catch
            {
                request.StatsConnectionCount = AcquiredConnectionCount;
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
