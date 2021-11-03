/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using static HttpRequestUtils;

    internal class AuthHttpClient : IDisposable
    {
        private static readonly TimeSpan DefaultDelay =
            TimeSpan.FromSeconds(1);

        private readonly HttpClient httpClient;
        private readonly int timeoutMillis;

        internal AuthHttpClient(TimeSpan timeout,
            ConnectionOptions connectionOptions)
        {
            httpClient = new HttpClient(new HttpConstDelayRetryHandler(
                Http.Client.CreateHandler(connectionOptions), DefaultDelay));
            httpClient.DefaultRequestHeaders.ConnectionClose = true;
            httpClient.DefaultRequestHeaders.CacheControl =
                CacheControlHeaderValue.Parse("no-store");
            timeoutMillis = (int)timeout.TotalMilliseconds;
        }

        internal async Task<string> ExecuteRequestAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            request.Headers.Host = request.RequestUri.Host;

            var response = await SendWithTimeoutAsync(httpClient, request,
                timeoutMillis, cancellationToken);

            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw await CreateServiceResponseExceptionAsync(response);
            }

            return await response.Content.ReadAsStringAsync();
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }
}
