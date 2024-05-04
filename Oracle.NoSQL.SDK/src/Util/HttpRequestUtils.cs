/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Net;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;

    internal static class HttpRequestUtils
    {
        private const string RetriesKey =
            "Oracle.NoSQL.SDK_nosql_ex_retries";
        private const string LastExceptionKey =
            "Oracle.NoSQL.SDK_nosql_last_exception";

        internal static AuthenticationHeaderValue GetBasicAuth(
            string userName, char[] password)
        {
            var charArray = new char[userName.Length + password.Length + 1];
            Array.Copy(userName.ToCharArray(), charArray, userName.Length);
            charArray[userName.Length] = ':';
            Array.Copy(password, 0, charArray, userName.Length + 1, password.Length);
            var byteArray = Encoding.UTF8.GetBytes(charArray);
            var result = new AuthenticationHeaderValue("Basic",
                Convert.ToBase64String(byteArray));
            Array.Clear(byteArray, 0, byteArray.Length);
            Array.Clear(charArray, 0, charArray.Length);
            return result;
        }

        internal static bool IsStatusCodeRetryable(HttpStatusCode statusCode)
        {
            // Not 100 % sure about BadGateway
            return statusCode == HttpStatusCode.InternalServerError ||
                   statusCode == HttpStatusCode.BadGateway ||
                   statusCode == HttpStatusCode.ServiceUnavailable ||
                   statusCode == HttpStatusCode.GatewayTimeout;
        }

        internal static bool IsHttpRequestExceptionRetryable(
            HttpRequestException ex)
        {
            // TODO:
            // Add other cases where HttpRequestException should not be
            // retried.
            return  !(ex.InnerException is
                System.Security.Authentication.AuthenticationException);
        }

        internal static async Task<ServiceResponseException>
            CreateServiceResponseExceptionAsync(HttpResponseMessage response)
        {
            var responseContent =
                await response.Content.ReadAsStringAsync();
            return new ServiceResponseException(response.StatusCode,
                response.ReasonPhrase, responseContent);
        }

        // Since HttpClient does not let setting timeout per request, we use
        // cancellation token to cancel request after given timeout.  In
        // addition, SendAsync throws TaskCanceledException if the request
        // times out, so we implement timeout via CancellationToken, in which
        // case we can differentiate between user cancellation and request
        // timeout (in which case we rethrow TimeoutException).
        internal static async Task<HttpResponseMessage> SendWithTimeoutAsync(
            HttpClient client,
            HttpRequestMessage message,
            int timeoutMillis,
            CancellationToken cancellationToken)
        {
            using var linkedSource = CancellationTokenSource
                .CreateLinkedTokenSource(cancellationToken);
            linkedSource.CancelAfter(timeoutMillis);
            try
            {
                return await client.SendAsync(message, linkedSource.Token);
            }
            catch (OperationCanceledException ex)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                var msg =
                    $"HTTP Request timed out after {timeoutMillis} ms";
                if (ex.Data[RetriesKey] is string retries)
                {
                    msg += " and " + retries;
                }

                throw new TimeoutException(msg,
                    ex.Data[LastExceptionKey] as Exception);
            }
        }

        internal abstract class HttpRetryHandler : DelegatingHandler
        {
            private protected HttpRetryHandler(
                HttpMessageHandler innerHandler) :
                base(innerHandler)
            {
            }

            private protected abstract TimeSpan GetDelay(int retryCount);

            protected override async Task<HttpResponseMessage> SendAsync(
                HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var retryCount = 1;
                Exception lastException = null;
                try
                {
                    while (true)
                    {
                        try
                        {
                            var response = await base.SendAsync(request,
                                cancellationToken);
                            if (response.IsSuccessStatusCode ||
                                !IsStatusCodeRetryable(response.StatusCode))
                            {
                                return response;
                            }

                            lastException = await
                                CreateServiceResponseExceptionAsync(response);
                        }
                        catch (HttpRequestException ex)
                        {
                            if (!IsHttpRequestExceptionRetryable(ex))
                            {
                                throw;
                            }

                            lastException = ex;
                        }

                        await Task.Delay(GetDelay(retryCount++),
                            cancellationToken);
                    }
                }
                catch (TaskCanceledException ex)
                {
                    ex.Data[RetriesKey] = $"{retryCount} retries";
                    ex.Data[LastExceptionKey] = lastException;
                    throw;
                }
            }
        }

        internal class HttpConstDelayRetryHandler : HttpRetryHandler
        {
            private readonly TimeSpan delay;

            internal HttpConstDelayRetryHandler(
                HttpMessageHandler innerHandler, TimeSpan delay) :
                base(innerHandler)
            {
                this.delay = delay;
            }

            private protected override TimeSpan GetDelay(int retryCount) =>
                delay;
        }
    }
}
