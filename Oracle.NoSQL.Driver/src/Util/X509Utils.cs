/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{

    using System;
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

        internal static bool ValidateCertificate(X509Certificate2 certificate,
            X509Chain chain, SslPolicyErrors errors,
            X509Certificate2Collection trustedRoots)
        {
            if (errors == SslPolicyErrors.None)
            {
                return true;
            }

            if ((errors & ~SslPolicyErrors.RemoteCertificateChainErrors) != 0)
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
            chain.ChainPolicy.CustomTrustStore.AddRange(trustedRoots);
            return chain.Build(certificate);
#else
            foreach (var status in chain.ChainStatus)
            {
                if (status.Status != X509ChainStatusFlags.UntrustedRoot)
                {
                    return false;
                }
            }

            foreach (var element in chain.ChainElements)
            {
                foreach (var status in element.ChainElementStatus)
                {
                    if (status.Status != X509ChainStatusFlags.UntrustedRoot)
                    {
                        return false;
                    }

                    var found = false;
                    foreach (var cert in trustedRoots)
                    {
                        if (BinaryValue.ByteArraysEqual(
                            element.Certificate.RawData, cert.RawData))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        return false;
                    }
                }
            }

            return true;
#endif
        }

    }

}
