/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests.IAM
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.Diagnostics.Tracing;
    using System.IO;
    using System.Linq;
    using System.Net.Http.Headers;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using System.Text.RegularExpressions;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static TestData;

    internal class RSAKeys
    {
        private const int PEMLineLength = 64;
        internal const string Passphrase = "oracle";

        private static string CreatePEM(byte[] data, string header,
            string footer,
            string[] extraHeaders = null)
        {
            var builder = new StringBuilder();
            builder.AppendLine(header);
            if (extraHeaders != null)
            {
                foreach (var elem in extraHeaders)
                {
                    builder.AppendLine(elem);
                }
            }

            var dataStr = Convert.ToBase64String(data);
            for (var i = 0; i < dataStr.Length; i += PEMLineLength)
            {
                builder.AppendLine(dataStr.Substring(i,
                    Math.Min(PEMLineLength, dataStr.Length - i)));
            }

            builder.AppendLine(footer);
            return builder.ToString();
        }

        internal RSA RSA { get; }

        internal string PrivatePKCS8PEM { get; }

        internal string PrivatePKCS8EncryptedPEM { get; }

        internal string PrivatePKCS1PEM { get; }

        //TODO.
        internal string PrivatePKCS1EncryptedPEM =>
            throw new NotSupportedException();

        internal RSAKeys()
        {
            RSA = RSA.Create(2048);
            PrivatePKCS8PEM = CreatePEM(RSA.ExportPkcs8PrivateKey(),
                "-----BEGIN PRIVATE KEY-----",
                "-----END PRIVATE KEY-----");
            PrivatePKCS8EncryptedPEM = CreatePEM(
                RSA.ExportEncryptedPkcs8PrivateKey(
                    Passphrase,
                    new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc,
                        HashAlgorithmName.SHA256, 1000)),
                "-----BEGIN ENCRYPTED PRIVATE KEY-----",
                "-----END ENCRYPTED PRIVATE KEY-----");
            PrivatePKCS1PEM = CreatePEM(RSA.ExportRSAPrivateKey(),
                "-----BEGIN RSA PRIVATE KEY-----",
                "-----END RSA PRIVATE KEY-----");
        }
    }

    internal static class Utils
    {
        private static readonly Regex AuthHeaderPattern = new Regex(
            "^Signature headers=\".+?\",keyId=\"(.+?)\"," +
            "algorithm=\"(.+?)\",signature=\"(.+?)\",version=\"(.+?)\"$");

        private static string GetSigningContent(string dateStr,
            string delegationToken)
        {
            var content =
                $"{RequestTargetHeader}: post /{NoSQLDataPath}\n" +
                $"{HostHeader}: {TestRegionHost}\n" +
                $"{DateHeader}: {dateStr}";

            if (delegationToken != null)
            {
                content += $"\n{OBOTokenHeader}: {delegationToken}";
            }

            return content;
        }

        // Mostly to prevent the passphrase from being erased in the original
        // creds object.
        internal static IAMCredentials CopyCredentials(IAMCredentials creds) =>
            new IAMCredentials
            {
                TenantId = creds.TenantId,
                UserId = creds.UserId,
                Fingerprint = creds.Fingerprint,
                PrivateKey = creds.PrivateKey,
                PrivateKeyPEM = creds.PrivateKeyPEM,
                PrivateKeyFile = creds.PrivateKeyFile,
                Passphrase = creds.Passphrase?.ToArray()
            };

        internal static string GetKeyIdFromToken(string token) =>
            "ST$" + token;

        internal static string[] GetOCIConfigLines(string profileName,
            IAMCredentials credentials, string region = null) => new string[]
        {
            $"[{profileName}]",
            $"tenancy={credentials.TenantId}",
            $"user={credentials.UserId}",
            $"fingerprint={credentials.Fingerprint}",
            $"key_file={credentials.PrivateKeyFile}",
            credentials.Passphrase != null
                ? $"pass_phrase={credentials.Passphrase}"
                : "",
            region != null ? $"region={region}" : ""
        };

        internal static void VerifyAuthHeader(string header, string keyId,
            RSA publicKey, string dateStr, string delegationToken)
        {
            var match = AuthHeaderPattern.Match(header);
            Assert.IsTrue(match.Success);
            Assert.AreEqual(5, match.Groups.Count);
            Assert.AreEqual(keyId, match.Groups[1].Value);

            var signature = match.Groups[3].Value;
            var sc = GetSigningContent(dateStr, delegationToken);

            var sigBytes = new byte[signature.Length];
            Assert.IsTrue(Convert.TryFromBase64String(signature, sigBytes,
                out int sigBytesLength));
            sigBytes = sigBytes[0..sigBytesLength];
            var scBytes = Encoding.UTF8.GetBytes(sc);
            Assert.IsTrue(publicKey.VerifyData(scBytes, sigBytes,
                HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }

        internal static void VerifyAuth(HttpRequestHeaders headers,
            string keyId, RSA publicKey, string compartment = CompartmentId,
            string delegationToken = null)
        {
            Assert.IsNotNull(headers.Authorization);
            Assert.IsTrue(headers.Contains(DateHeader));
            Assert.IsTrue(headers.Contains(CompartmentIdHeader));

            var authHeader = headers.Authorization.ToString();
            Assert.IsFalse(string.IsNullOrEmpty(authHeader));

            var dateStr = headers.GetValues(DateHeader).FirstOrDefault();
            Assert.IsTrue(DateTimeOffset.TryParse(dateStr,
                out DateTimeOffset dateVal));

            // some reasonable range
            Assert.IsTrue(dateVal <= DateTimeOffset.UtcNow &&
                          dateVal >= DateTimeOffset.UtcNow -
                          TimeSpan.FromDays(1));

            var compartmentVal = headers.GetValues(CompartmentIdHeader)
                .FirstOrDefault();
            // test self-check
            Assert.IsNotNull(compartment);
            Assert.AreEqual(compartment, compartmentVal);

            if (delegationToken != null)
            {
                Assert.IsTrue(headers.Contains(OBOTokenHeader));
                var delegationTokenVal = headers.GetValues(OBOTokenHeader)
                    .FirstOrDefault();
                Assert.AreEqual(delegationToken, delegationTokenVal);
            }

            VerifyAuthHeader(authHeader, keyId, publicKey, dateStr,
                delegationToken);
        }

        internal static void VerifyAuthEqual(HttpRequestHeaders newHeaders,
            HttpRequestHeaders oldHeaders, string keyId, RSA publicKey,
            string compartment = CompartmentId,
            string delegationToken = null)
        {
            VerifyAuth(newHeaders, keyId, publicKey, compartment,
                delegationToken);
            VerifyAuth(oldHeaders, keyId, publicKey, compartment,
                delegationToken);

            var newAuthHeader = newHeaders.Authorization?.ToString();
            var oldAuthHeader = oldHeaders.Authorization?.ToString();
            Assert.AreEqual(oldAuthHeader, newAuthHeader);

            var newDateStr = newHeaders.GetValues(DateHeader)
                .FirstOrDefault();
            var oldDateStr = oldHeaders.GetValues(DateHeader)
                .FirstOrDefault();
            Assert.AreEqual(oldDateStr, newDateStr);
        }

        internal static void VerifyAuthLaterDate(
            HttpRequestHeaders newHeaders, HttpRequestHeaders oldHeaders,
            string newKeyId, string oldKeyId, RSA newPublicKey,
            RSA oldPublicKey, string compartment = CompartmentId,
            string newDelegationToken = null,
            string oldDelegationToken = null)
        {
            VerifyAuth(newHeaders, newKeyId, newPublicKey, compartment,
                newDelegationToken);
            VerifyAuth(oldHeaders, oldKeyId, oldPublicKey, compartment,
                oldDelegationToken);

            var newAuthHeader = newHeaders.Authorization?.ToString();
            var oldAuthHeader = oldHeaders.Authorization?.ToString();
            Assert.AreNotEqual(oldAuthHeader, newAuthHeader);

            var newDate = newHeaders.Date;
            Assert.IsTrue(newDate.HasValue);
            var oldDate = oldHeaders.Date;
            Assert.IsTrue(oldDate.HasValue);

            Assert.IsTrue(newDate.Value > oldDate.Value);
        }

        internal static void VerifyAuthLaterDate(HttpRequestHeaders newHeaders,
            HttpRequestHeaders oldHeaders, string keyId, RSA publicKey,
            string compartment = CompartmentId,
            string delegationToken = null) => VerifyAuthLaterDate(newHeaders,
            oldHeaders, keyId, keyId, publicKey, publicKey, compartment,
            delegationToken, delegationToken);
    }

}
