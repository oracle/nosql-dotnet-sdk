/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{

    using System;
    using System.Diagnostics;
    using System.Net.Security;
    using System.Security.Cryptography.X509Certificates;

    internal static class X509Utils
    {
#if !NET5_0_OR_GREATER
        private const string CertPrefix = "-----BEGIN CERTIFICATE-----";
        private const string CertPostfix = "-----END CERTIFICATE-----";
#endif

        internal static X509Certificate2Collection GetCertificatesFromPEM(
            string pem)
        {
            var result = new X509Certificate2Collection();
#if NET5_0_OR_GREATER
            result.ImportFromPem(pem);
#else
            var offset = 0;
            for(;;)
            {
                var idx = pem.IndexOf(CertPrefix, offset,
                    StringComparison.Ordinal);

                if (idx == -1)
                {
                    break;
                }

                offset = idx + CertPrefix.Length;
                idx = pem.IndexOf(CertPostfix, offset,
                    StringComparison.Ordinal);

                if (idx == -1)
                {
                    break;
                }

                var data = Convert.FromBase64String(pem.Substring(offset,
                    idx - offset));
                result.Add(new X509Certificate2(data));

                offset = idx;
            }
#endif
            return result;
        }

        // There is no Dispose() method on the collection and Clear() will not
        // dispose of the items in the collection.
        internal static void DisposeCertificates(X509Certificate2Collection
            certificates)
        {
            foreach (var certificate in certificates)
            {
                certificate.Dispose();
            }

            certificates.Clear();
        }

#if !NET5_0_OR_GREATER
        private static bool VerifyCertPre50(X509Certificate2 certificate,
            X509Chain chain, X509Certificate2Collection trustedRoots)
        {
            chain.ChainPolicy.VerificationFlags =
                X509VerificationFlags.AllowUnknownCertificateAuthority;
            chain.ChainPolicy.ExtraStore.AddRange(trustedRoots);
            for (int i = 1; i < chain.ChainElements.Count; i++)
            {
                chain.ChainPolicy.ExtraStore.Add(
                    chain.ChainElements[i].Certificate);
            }

            var isVerified = chain.Build(certificate);

            // X509VerificationFlags.AllowUnknownCertificateAuthority flag
            // allows not just untrusted root CA but any CA with unknown
            // issuer or just leaf certificate itself
            // (X509StatusFlags.PartialChain status), so X509Chain.Build()
            // return value is not enough, we also have to ensure that the
            // built chain terminates on the certificate we trust.

            // After the chain is built, the root should be the last element.
            var root = chain.ChainElements[chain.ChainElements.Count- 1]
                .Certificate;
            var rootFound = false;

            foreach (var cert in trustedRoots)
            {
                if (BinaryValue.ByteArraysEqual(root.RawData,
                    cert.RawData))
                {
                    rootFound = true;
                    break;
                }
            }

            return isVerified && rootFound;
        }
#endif

        internal static bool ValidateCertificate(X509Certificate2 certificate,
            X509Chain chain, SslPolicyErrors errors,
            ConnectionOptions options)
        {
            Debug.Assert(options != null);

            // Allow RemoteCertificateNameMismatch error if
            // ConnectionOptions.DisableHostNameVerification is set.
            if (options.DisableHostnameVerification)
            {
                errors &= ~SslPolicyErrors.RemoteCertificateNameMismatch;
            }

            if (errors == SslPolicyErrors.None)
            {
                return true;
            }

            if ((errors &
                ~SslPolicyErrors.RemoteCertificateChainErrors) != 0 ||
                options.TrustedRootCertificates == null)
            {
                return false;
            }

#if NET5_0_OR_GREATER
            for (int i = 1; i < chain.ChainElements.Count; i++)
            {
                chain.ChainPolicy.ExtraStore.Add(
                    chain.ChainElements[i].Certificate);
            }

            chain.ChainPolicy.CustomTrustStore.Clear();
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.AddRange(
                options.TrustedRootCertificates);
            return chain.Build(certificate);
#else
            return VerifyCertPre50(certificate, chain,
                options.TrustedRootCertificates);
#endif
        }
    }
}
