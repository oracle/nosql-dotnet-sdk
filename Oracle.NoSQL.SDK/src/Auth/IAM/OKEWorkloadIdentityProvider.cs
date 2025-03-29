/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.IO;
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using static Utils;
    using static HttpConstants;

    internal class OKEWorkloadIdentityProvider : SecurityTokenBasedProvider
    {
        /* Default path for reading Kubernetes service account cert */
        private const string DefaultKubernetesServiceAccountCertPath =
            "/var/run/secrets/kubernetes.io/serviceaccount/ca.crt";

        /* Environment variable of the path for Kubernetes service account cert */
        private const string KubernetesServiceAccountCertPathEnv =
            "OCI_KUBERNETES_SERVICE_ACCOUNT_CERT_PATH";

        /* Environment variable of Kubernetes service host */
        private const string KubernetesServiceHostEnv =
            "KUBERNETES_SERVICE_HOST";

        /* Kubernetes service port */
        private const int KubernetesServerPort = 12250;

        /* Default path for service account token */
        private const string KubernetesServiceAccountTokenPath =
            "/var/run/secrets/kubernetes.io/serviceaccount/token";

        private const string ServiceAccountTokenName =
            "Kubernetes service account token";

        private const string TokenPath = "/resourcePrincipalSessionTokens";

        private Func<CancellationToken, Task<string>>
            serviceAccountTokenProvider;

        private ConnectionOptions connectionOptions;
        private AuthHttpClient httpClient;
        private readonly string tokenUrl;
        private RSA keyPair;
        private bool disposed;

        public OKEWorkloadIdentityProvider(IAMAuthorizationProvider iam) :
            base(iam.ProfileExpireBefore)
        {
            InitServiceAccountTokenProvider(iam);
            InitHttpClient(iam);

            var host = Environment.GetEnvironmentVariable(
                KubernetesServiceHostEnv);
            if (string.IsNullOrEmpty(host))
            {
                throw new ArgumentException(
                    "Missing environment variable " +
                    KubernetesServiceHostEnv + ", please contact OKE " +
                    "Foundation team for help.");
            }

            tokenUrl = $"https://{host}:{KubernetesServerPort}{TokenPath}";
        }

        private void InitServiceAccountTokenProvider(
            IAMAuthorizationProvider iam)
        {
            string serviceAccountTokenFile = null;
            if (iam.ServiceAccountToken != null)
            {
                if (iam.ServiceAccountTokenFile != null ||
                    iam.ServiceAccountTokenProvider != null)
                {
                    throw new ArgumentException(
                        "Cannot specify service account token file or " +
                        "service account token provider together with " +
                        "service account token");
                }

                var serviceAccountToken = iam.ServiceAccountToken;
                serviceAccountTokenProvider =
                    cancellationToken => Task.FromResult(serviceAccountToken);
            }
            else if (iam.ServiceAccountTokenFile != null)
            {
                if (iam.ServiceAccountTokenProvider != null)
                {
                    throw new ArgumentException(
                        "Cannot specify service account token file " +
                        "together with service account token provider");
                }

                serviceAccountTokenFile = iam.ServiceAccountTokenFile;
            }
            else if (iam.ServiceAccountTokenProvider != null)
            {
                serviceAccountTokenProvider = iam.ServiceAccountTokenProvider;
            }

            if (serviceAccountTokenProvider == null)
            {
                serviceAccountTokenFile ??= KubernetesServiceAccountTokenPath;
                serviceAccountTokenProvider = cancellationToken =>
                    File.ReadAllTextAsync(serviceAccountTokenFile,
                        cancellationToken);
            }
        }

        private void InitHttpClient(IAMAuthorizationProvider iam)
        {
            var caCertFile =
                Environment.GetEnvironmentVariable(
                    KubernetesServiceAccountCertPathEnv) ??
                DefaultKubernetesServiceAccountCertPath;

            connectionOptions = new ConnectionOptions
            {
                TrustedRootCertificateFile = caCertFile,
                DisableHostnameVerification = true
            };
            connectionOptions.Init();

            httpClient = new AuthHttpClient(iam.RequestTimeout,
                connectionOptions);
        }

        private async Task<string> GetServiceAccountTokenAsync(
            CancellationToken cancellationToken)
        {
            string tokenStr;
            try
            {
                tokenStr = await serviceAccountTokenProvider(
                    cancellationToken);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Error retrieving {ServiceAccountTokenName}: " +
                    ex.Message, ex);
            }

            // Validate the token.
            var token = SecurityToken.Create(tokenStr,
                ServiceAccountTokenName);
            if (!token.IsValid())
            {
                throw new ArgumentException(
                    $"{ServiceAccountTokenName} is invalid or expired");
            }

            return tokenStr;
        }

        private async Task<string> GetSecurityTokenInnerAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string result;
            try
            {
                result = await httpClient.ExecuteRequestAsync(request,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                throw new AuthorizationException(
                    "Error getting security token from Kubernetes: " +
                    $"{ex.Message}", ex);
            }

            // The encoded response is returned with quotation marks.
            result = result.Replace("\"", "");

            try
            {
                result = System.Text.Encoding.UTF8.GetString(
                    Convert.FromBase64String(result));
            }
            catch (Exception ex)
            {
                throw new AuthorizationException(
                    "Error decoding security token from Kubernetes: " +
                    ex.Message);
            }

            result = ParseTokenResult(result, "Kubernetes");

            // Kubernetes token has duplicated key id prefix "ST$".
            if (result.Length <= 3)
            {
                throw new AuthorizationException(
                    "Security token from Kubernetes is missing or invalid");
            }

            return result.Substring(3);
        }

        private protected override RSA PrivateKey => keyPair;

        private protected override async Task<string>
            RefreshSecurityTokenAsync(CancellationToken cancellationToken)
        {
            keyPair = GenerateRSAKeyPair();
            var serviceAccountToken = await GetServiceAccountTokenAsync(
                cancellationToken);
            var requestId = GenerateOpcRequestId();

            var request = new HttpRequestMessage(HttpMethod.Post, tokenUrl);

            var podKey = Convert.ToBase64String(
                keyPair.ExportSubjectPublicKeyInfo());
            var payload = $"{{\"podKey\":\"{podKey}\"}}";

            request.Content = new StringContent(payload);
            request.Content.Headers.ContentType = new MediaTypeHeaderValue(
                ApplicationJson);
            request.Headers.Add(OPCRequestId, requestId);
            request.Headers.Add(HttpConstants.Authorization,
                "Bearer " + serviceAccountToken);

            try
            {
                return await GetSecurityTokenInnerAsync(request,
                    cancellationToken);
            }
            catch (AuthorizationException ex)
            {
                ex.AddExtraInfo($"opc-request-id: {requestId}");
                throw;
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                httpClient.Dispose();
                connectionOptions.ReleaseResources();
                disposed = true;
            }
        }
    }
}
