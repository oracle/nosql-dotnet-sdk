/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Threading;
    using System.Threading.Tasks;
    using static Utils;

    internal class InstancePrincipalProvider : SecurityTokenBasedProvider
    {
        // The default purpose value in federation requests against IAM
        private const string DefaultPurpose = "DEFAULT";

        private readonly AuthHttpClient httpClient;
        private readonly InstanceMetadataClient imdsClient;
        private string federationEndpoint;
        private RSA keyPair;
        private RSA instancePrivateKey;
        private X509Certificate2 instanceCertificate;
        private readonly X509Certificate2[] intermediateCertificates =
            new X509Certificate2[1];
        private string tenantId;
        private X509FederationClient federationClient;
        private bool disposed;

        internal InstancePrincipalProvider(IAMAuthorizationProvider iam,
            NoSQLConfig config): base(iam.ProfileExpireBefore)
        {
            federationEndpoint = iam.FederationEndpoint;
            httpClient = new AuthHttpClient(iam.RequestTimeout,
                config.ConnectionOptions);
            imdsClient = new InstanceMetadataClient(httpClient);
        }

        private protected override RSA PrivateKey => keyPair;

        // Auto-detect the endpoint that should be used when talking to IAM
        // if no endpoint has been configured.
        private async Task<string> GetFederationEndpointAsync(
            CancellationToken cancellationToken)
        {
            try
            {
                var region = await imdsClient.GetRegionAsync(cancellationToken);
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
                    await imdsClient.GetValueAsync("identity/cert.pem",
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
                var privateKey = await imdsClient.GetValueAsync(
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
                    GetCertificateFromPEM(await imdsClient.GetValueAsync(
                        "identity/intermediate.pem", cancellationToken));
            }
            catch (Exception ex)
            {
                throw new AuthorizationException(
                    "Error retrieving instance intermediate certificate: " +
                    ex.Message, ex);
            }
        }

        private protected override async Task<string>
            RefreshSecurityTokenAsync(CancellationToken cancellationToken)
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

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                imdsClient.Dispose();
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
