/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using System.Text.RegularExpressions;

    internal static class Utils
    {
        private const string AlgRSA = "rsa-sha256";

        /* OCI signature version only version 1 is allowed*/
        private const int SignatureVersion = 1;

        private static readonly Regex OCIDPattern = new Regex(
            "^([0-9a-zA-Z-_]+[.:])([0-9a-zA-Z-_]*[.:]){3,}([0-9a-zA-Z-_]+)$");

        internal static string Base64UrlDecode(string value)
        {
            var asBase64 = value.Replace('_', '/').Replace('-', '+');
            if ((value.Length & 3) != 0)
            {
                asBase64 = asBase64.PadRight(
                    ((asBase64.Length >> 2) + 1) << 2, '=');
            }

            var bytes = Convert.FromBase64String(asBase64);
            return Encoding.UTF8.GetString(bytes);
        }

        internal static bool IsValidOCID(string ocid)
        {
            return OCIDPattern.IsMatch(ocid);
        }

        internal static string GetSignatureHeader(string signingHeaders,
            string keyId, string signature) =>
            $"Signature headers=\"{signingHeaders}\"," +
            $"keyId=\"{keyId}\",algorithm=\"{AlgRSA}\"," +
            $"signature=\"{signature}\",version=\"{SignatureVersion}\"";

        internal static string CreateSignature(string content, RSA privateKey)
        {
            var data = Encoding.UTF8.GetBytes(content);
            var sign = privateKey.SignData(data, HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            return Convert.ToBase64String(sign);
        }

        internal static X509Certificate2 GetCertificateFromPEM(string pem) =>
            new X509Certificate2(Encoding.UTF8.GetBytes(pem));

        // Taken from C# OCI SDK
        internal static string GetTenantIdFromInstanceCertificate(
            X509Certificate certificate)
        {
            if (certificate.Subject == null)
            {
                throw new InvalidOperationException(
                    "Invalid instance certificate, missing subject");
            }

            foreach (var item in certificate.Subject.Split(','))
            {
                if (item.Contains("opc-tenant"))
                {
                    return item.Split(':')[1];
                }

                if (item.Contains("opc-identity"))
                {
                    return item.Split(':')[1];
                }
            }

            throw new AuthorizationException(
                "Instance certificate does not contain tenant id");
        }

        internal static RSA GenerateRSAKeyPair() => RSA.Create(2048);

        internal static string GetFingerprint(X509Certificate2 certificate)
        {
            var builder = new StringBuilder();

            var thumbprint = certificate.Thumbprint;
            Debug.Assert(thumbprint != null);
            thumbprint = thumbprint.ToLower();

            for(var i = 0; i < thumbprint.Length; i++)
            {
                builder.Append(thumbprint[i]);
                if ((i & 1) != 0 && i != thumbprint.Length - 1)
                {
                    builder.Append(':');
                }
            }

            return builder.ToString();
        }

        internal static byte[] ComputeSHA256Digest(string payload)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(Encoding.UTF8.GetBytes(payload));
        }

        internal static byte[] ComputeSHA256Digest(Stream payload)
        {
            using var sha256 = SHA256.Create();
            return sha256.ComputeHash(payload);
        }
    }

}
