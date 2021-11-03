/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using static HttpRequestUtils;
    using static DateTimeUtils;

    internal sealed class KVStoreTokenProvider : IDisposable
    {
        internal class TokenResult
        {
            internal string Token { get; set; }
            internal DateTime ExpireAt { get; set; }

            internal TokenResult(string token, DateTime expireAt)
            {
                Token = token;
                ExpireAt = expireAt;
            }
        }

        private const string LoginEndpoint = "/login";
        private const string RenewEndpoint = "/renew";
        private const string LogoutEndpoint = "/logout";
        private static readonly string BasePath =
            $"/{HttpConstants.NoSQLVersion}/nosql/security";

        private readonly Uri loginUri;
        private readonly Uri renewUri;
        private readonly Uri logoutUri;
        private readonly AuthHttpClient httpClient;

        internal KVStoreTokenProvider(
            KVStoreAuthorizationProvider authProvider, Uri uri,
            ConnectionOptions connectionOptions)
        {
            if (uri.Scheme != Uri.UriSchemeHttps)
            {
                throw new ArgumentException(
                    "Invalid protocol in uri for secure KVStore: " +
                    $"{uri.Scheme}, requires https");
            }

            loginUri = new Uri(uri, BasePath + LoginEndpoint);
            renewUri = new Uri(uri, BasePath + RenewEndpoint);
            logoutUri = new Uri(uri, BasePath + LogoutEndpoint);
            httpClient = new AuthHttpClient(authProvider.RequestTimeout,
                connectionOptions);
        }

        private static TokenResult ParseTokenResponse(string response)
        {
            const string resultName = "kvstore authentication token result";

            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(response);
            }
            catch (JsonException ex)
            {
                throw new BadProtocolException(
                    $"Failed to parse {resultName}", ex);
            }

            if (!document.RootElement.TryGetProperty("token",
                    out var tokenElement))
            {
                throw new BadProtocolException(
                    $"Missing token value in {resultName}");
            }

            if (tokenElement.ValueKind != JsonValueKind.String)
            {
                throw new BadProtocolException(
                    $"Invalid token value kind in {resultName}: " +
                    tokenElement.ValueKind);
            }

            if (!document.RootElement.TryGetProperty("expireAt",
                out var expireAtElement))
            {
                throw new BadProtocolException(
                    $"Missing token expiration time in {resultName}");
            }

            if (!expireAtElement.TryGetInt64(out var expireAt) ||
                expireAt <= 0)
            {
                throw new BadProtocolException(
                    $"Invalid token expiration time value in {resultName}");
            }

            return new TokenResult(tokenElement.GetString(),
                UnixMillisToDateTime(expireAt));
        }

        internal async Task<TokenResult> LoginAsync(
            KVStoreCredentials credentials,
            CancellationToken cancellationToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, loginUri);
                request.Headers.Authorization = GetBasicAuth(
                    credentials.UserName,
                    credentials.Password);
                var response = await httpClient.ExecuteRequestAsync(request,
                    cancellationToken);
                return ParseTokenResponse(response);
            }
            catch (Exception ex)
            {
                throw new AuthorizationException(
                    $"Failed to login to service: {ex.Message}", ex);
            }
        }

        internal async Task<TokenResult> RenewAsync(string token,
            CancellationToken cancellationToken)
        {
            try
            {
                var request = new HttpRequestMessage(HttpMethod.Get, renewUri);
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer", token);
                var response = await httpClient.ExecuteRequestAsync(request,
                    cancellationToken);
                return ParseTokenResponse(response);
            }
            catch (Exception ex)
            {
                // Will be caught by
                // KVStoreAuthorizationProvider.ScheduleRenew()
                throw new AuthorizationException(
                    $"Failed to renew login token: {ex.Message}", ex);
            }
        }

        internal async Task LogoutAsync(string token,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            try
            {
                var request =
                    new HttpRequestMessage(HttpMethod.Get, logoutUri);
                request.Headers.Authorization = new AuthenticationHeaderValue(
                    "Bearer", token);
                await httpClient.ExecuteRequestAsync(request,
                    cancellationToken);
            }
            catch (Exception)
            {
                // We don't throw on logout
                //TODO: log the error
            }
        }

        public void Dispose()
        {
            httpClient.Dispose();
        }
    }

}
