/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
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
    using System.Net.Http.Headers;
    using System.Security.Authentication;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;
    using static Utils;
    using static HttpConstants;

    internal class X509FederationClient
    {
        // Signing headers used to obtain security token
        private const string SigningHeaders =
            "date (request-target) content-length content-type x-content-sha256";

        private readonly AuthHttpClient httpClient;
        private readonly Uri federationUri;
        private readonly string tenantId;
        private readonly string purpose;

        internal X509FederationClient(AuthHttpClient httpClient,
            string federationEndpoint, string tenantId, string purpose)
        {
            this.httpClient = httpClient;
            federationUri = new Uri(federationEndpoint + "/v1/x509");
            this.tenantId = tenantId;
            this.purpose = purpose;
        }

        private string GetRequestPayload(RSA publicKey,
            X509Certificate2 instanceCertificate,
            X509Certificate2[] intermediateCertificates)
        {
            var publicKeyDerBase64 = Convert.ToBase64String(
                publicKey.ExportSubjectPublicKeyInfo());
            var instanceCertificateBase64 = Convert.ToBase64String(
                instanceCertificate.RawData);
            var intermediateStrings =
                new string[intermediateCertificates.Length];

            for (var i = 0; i < intermediateCertificates.Length; i++)
            {
                intermediateStrings[i] = Convert.ToBase64String(
                    intermediateCertificates[i].RawData);
            }

            return JsonSerializer.Serialize(new
            {
                publicKey = publicKeyDerBase64,
                certificate = instanceCertificateBase64,
                purpose,
                intermediateCertificates = intermediateStrings
            });
        }

        private string GetSigningContent(string dateStr, string payload,
            string digest) =>
            $"{Date}: {dateStr}\n" +
            $"{RequestTarget}: post {federationUri.AbsolutePath}\n" +
            $"{ContentLength.ToLower()}: {payload.Length}\n" +
            $"{ContentType.ToLower()}: {ApplicationJson}\n" +
            $"{ContentSHA256}: {digest}";

        private string GetAuthorizationHeader(
            X509Certificate2 instanceCertificate, RSA instancePrivateKey,
            string dateStr, string payload, string digest)
        {
            string signature;
            try
            {
                signature = CreateSignature(GetSigningContent(
                    dateStr, payload, digest), instancePrivateKey);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    "Error signing instance principal federation request: " +
                    ex.Message, ex);
            }

            var fingerprint = GetFingerprint(instanceCertificate);
            var keyId = $"{tenantId}/fed-x509/{fingerprint}";
            return GetSignatureHeader(SigningHeaders, keyId, signature);
        }

        private void SetRequestHeaders(HttpRequestMessage request,
            X509Certificate2 instanceCertificate, RSA instancePrivateKey,
            string payload)
        {
            var dateStr = DateTime.UtcNow.ToString("r");
            var digest = Convert.ToBase64String(ComputeSHA256Digest(payload));

            request.Content.Headers.ContentType = new MediaTypeHeaderValue(
                ApplicationJson);
            request.Content.Headers.Add(ContentSHA256, digest);
            request.Headers.Add(Date, dateStr);
            request.Headers.Add(HttpConstants.Authorization,
                GetAuthorizationHeader(instanceCertificate,
                    instancePrivateKey, dateStr, payload, digest));
        }

        private static string ParseTokenResult(string result)
        {
            try
            {
                using var doc = JsonDocument.Parse(result);
                var token = doc.RootElement.GetProperty("token")
                    .GetString();
                if (token == null)
                {
                    throw new ArgumentNullException(nameof(token),
                        "Token value is null");
                }

                return token;
            }
            catch (Exception ex)
            {
                throw new AuthorizationException(
                    "Received invalid security token response from IAM: " +
                    ex.Message, ex);
            }
        }

        internal async Task<string> GetSecurityTokenAsync(RSA publicKey,
            X509Certificate2 instanceCertificate, RSA instancePrivateKey,
            X509Certificate2[] intermediateCertificates,
            CancellationToken cancellationToken)
        {
            var request =
                new HttpRequestMessage(HttpMethod.Post, federationUri);
            var payload = GetRequestPayload(publicKey, instanceCertificate,
                intermediateCertificates);
            request.Content = new StringContent(payload);
            SetRequestHeaders(request, instanceCertificate,
                instancePrivateKey, payload);

            string result;
            try
            {
                result = await httpClient.ExecuteRequestAsync(request,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                throw new AuthorizationException(
                    $"Error getting security token from IAM: {ex.Message}",
                    ex);
            }

            return ParseTokenResult(result);
        }
    }
}
