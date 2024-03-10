/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

using System.Net.Http;
using System.Reflection;
using Oracle.NoSQL.SDK;

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Net;
    using System.Net.Http;
    using System.Security.Authentication;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using static Utils;

    internal class InstancePrincipalProvider : AuthenticationProfileProvider
    {
        // Instance metadata service base URL
        private const string MetadataServiceBaseUrl =
            "http://169.254.169.254/opc/v2/";
        private const string FallbackMetadataServiceUrl =
            "http://169.254.169.254/opc/v1/";
        // The authorization header need to send to metadata service since V2
        private const string AuthorizationHeaderValue = "Bearer Oracle";
        // The default purpose value in federation requests against IAM
        private const string DefaultPurpose = "DEFAULT";

        private readonly AuthHttpClient httpClient;
        private string federationEndpoint;
        private string metadataUrl;
        private RSA keyPair;
        private RSA instancePrivateKey;
        private X509Certificate2 instanceCertificate;
        private readonly X509Certificate2[] intermediateCertificates =
            new X509Certificate2[1];
        private string tenantId;
        private X509FederationClient federationClient;
        private SecurityToken token;
        private bool disposed;

        internal InstancePrincipalProvider(string federationEndpoint,
            TimeSpan requestTimeout, ConnectionOptions connectionOptions)
        {
            this.federationEndpoint = federationEndpoint;
            httpClient = new AuthHttpClient(requestTimeout,
                connectionOptions);
        }

        private async Task<string> GetInstanceMetadataAsync(string path,
            CancellationToken cancellationToken)
        {
            var checkFallback = false;
            if (metadataUrl == null)
            {
                metadataUrl = MetadataServiceBaseUrl;
                checkFallback = true;
            }

            var request = new HttpRequestMessage(HttpMethod.Get,
                metadataUrl + path);
            request.Headers.Add(HttpConstants.Authorization, AuthorizationHeaderValue);

            try
            {
                return await httpClient.ExecuteRequestAsync(request,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                if (checkFallback && ex is ServiceResponseException srex &&
                    srex.StatusCode == HttpStatusCode.NotFound)
                {
                    metadataUrl = FallbackMetadataServiceUrl;
                    try
                    {
                        request.RequestUri = new Uri(metadataUrl + path);
                        return await httpClient.ExecuteRequestAsync(request,
                            cancellationToken);
                    }
                    catch (Exception ex2)
                    {
                        throw new AuthorizationException(
                            $"Unable to get resource {path} from instance " +
                            $"metadata {MetadataServiceBaseUrl} or " +
                            $"fall back to {FallbackMetadataServiceUrl}, " +
                            $"error: {ex2.Message}", ex2);
                    }
                }

                throw new AuthorizationException(
                    $"Unable to get resource {path} from instance metadata " +
                    $"{metadataUrl}, error: {ex.Message}", ex);
            }
        }

        // Auto-detect the endpoint that should be used when talking to IAM
        // if no endpoint has been configured.
        private async Task<string> GetFederationEndpointAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                var result = await GetInstanceMetadataAsync("instance/region",
                    cancellationToken);
                var region = SDK.Region.FromRegionCodeOrId(result);
                return $"https://auth.{region.RegionId}.{region.SecondLevelDomain}";
            }
            catch (Exception ex)
            {
                throw new AuthorizationException(
                    $"Error retrieving federation endpoint: {ex.Message}", ex);
            }
        }

        private async Task RefreshInstanceCertificatesAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                instanceCertificate?.Dispose();
                instanceCertificate = GetCertificateFromPEM(
                    await GetInstanceMetadataAsync("identity/cert.pem",
                        cancellationToken));
            }
            catch (Exception ex)
            {
                throw new AuthorizationException(
                    "Error retrieving instance leaf certificate: " +
                    ex.Message, ex);
            }

            var certTenantId =
                GetTenantIdFromInstanceCertificate(instanceCertificate);

            if (tenantId == null)
            {
                tenantId = certTenantId;
            }
            else if (certTenantId != tenantId)
            {
                throw new AuthorizationException(
                $"Tenant id in instance leaf certificate {certTenantId} " +
                "is different from previously retrieved or set tenant id " +
                tenantId +
                ". Tenant id in certificate should never be changed");
            }

            try
            {
                var privateKey = await GetInstanceMetadataAsync(
                    "identity/key.pem", cancellationToken);
                instancePrivateKey?.Dispose();
                instancePrivateKey = PemPrivateKeyUtils.GetFromString(
                    privateKey);
            }
            catch (Exception ex)
            {
                throw new AuthorizationException(
                    $"Error retrieving instance private key: {ex.Message}",
                    ex);
            }

            try
            {
                intermediateCertificates[0]?.Dispose();
                intermediateCertificates[0] =
                    GetCertificateFromPEM(await GetInstanceMetadataAsync(
                        "identity/intermediate.pem", cancellationToken));
            }
            catch (Exception ex)
            {
                throw new AuthorizationException(
                    "Error retrieving instance intermediate certificate: " +
                    ex.Message, ex);
            }
        }

        private async Task<string> GetSecurityTokenAsync(
            CancellationToken cancellationToken)
        {
            federationEndpoint ??= await GetFederationEndpointAsync(
                cancellationToken);

            await RefreshInstanceCertificatesAsync(cancellationToken);

            keyPair?.Dispose();
            keyPair = GenerateRSAKeyPair();

            federationClient ??= new X509FederationClient(httpClient,
                federationEndpoint, tenantId, DefaultPurpose);

            return await federationClient.GetSecurityTokenAsync(keyPair,
                instanceCertificate, instancePrivateKey,
                intermediateCertificates, cancellationToken);
        }

        internal override async Task<AuthenticationProfile> GetProfileAsync(
            bool forceRefresh, CancellationToken cancellationToken)
        {
            if (forceRefresh || token == null || !token.IsValid)
            {
                token = SecurityToken.Create(
                    await GetSecurityTokenAsync(cancellationToken));
            }

            return new AuthenticationProfile("ST$" + token.Value, keyPair);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                httpClient.Dispose();
                keyPair?.Dispose();
                instancePrivateKey?.Dispose();
                instanceCertificate?.Dispose();
                intermediateCertificates[0]?.Dispose();
                disposed = true;
            }
        }
    }

}
