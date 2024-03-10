/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Http
{
    using System;
    using System.IO;
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
        private readonly Uri dataPathUri = new Uri(NoSQLDataPath,
            UriKind.Relative);

        private readonly NoSQLConfig config;
        private readonly ProtocolHandler protocolHandler;
        private readonly HttpClient client;
        private int requestId;

        internal static HttpMessageHandler CreateHandler(
            ConnectionOptions connectionOptions)
        {
            var handler = new HttpClientHandler();
            if (connectionOptions?.TrustedRootCertificates != null)
            {
                handler.ServerCertificateCustomValidationCallback =
                    (request, certificate, chain, errors) =>
                        ValidateCertificate(certificate, chain, errors,
                            connectionOptions.TrustedRootCertificates);
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

            client = new HttpClient(CreateHandler(config.ConnectionOptions),
                true)
            {
                BaseAddress = config.Uri
            };

            client.DefaultRequestHeaders.Host = config.Uri.Host;
            client.DefaultRequestHeaders.Connection.Add("keep-alive");
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue(protocolHandler.ContentType));
            // Disable default timeout since we use our own timeout mechanism
            client.Timeout = Timeout.InfiniteTimeSpan;
        }

        internal async Task<object> ExecuteRequestAsync(Request request,
            CancellationToken cancellationToken)
        {
            var message = new HttpRequestMessage(HttpMethod.Post,
                dataPathUri);

            var stream = new MemoryStream();
            protocolHandler.StartWrite(stream, request);
            request.Serialize(protocolHandler.Serializer, stream);

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

            var response = await SendWithTimeoutAsync(client, message,
                request.RequestTimeoutMillis, cancellationToken);
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw await CreateServiceResponseExceptionAsync(response);
            }

            // The stream returned by ReadAsStreamAsync(), even though it is
            // usually a MemoryStream, doesn't allow access to the buffer
            // via MemoryStream.GetBuffer() which is needed for
            // deserialization, so we have to use ReadAsByteArrayAsync().
            var buffer = await response.Content.ReadAsByteArrayAsync();
            stream = new MemoryStream(buffer, 0, buffer.Length, false, true);
            protocolHandler.StartRead(stream, request);
            return request.Deserialize(protocolHandler.Serializer, stream);
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}
