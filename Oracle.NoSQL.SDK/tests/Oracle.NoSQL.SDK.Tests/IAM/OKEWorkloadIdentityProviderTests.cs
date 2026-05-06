/*-
 * Copyright (c) 2020, 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests.IAM
{
    using System;
    using System.IO;
    using System.Net.Security;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Security.Cryptography.X509Certificates;
    using System.Text;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static TestData;

    [TestClass]
    public class OKEWorkloadIdentityProviderTests : TestBase
    {
        private const string KubernetesServiceAccountCertPathEnv =
            "OCI_KUBERNETES_SERVICE_ACCOUNT_CERT_PATH";

        private const string KubernetesServiceHostEnv =
            "KUBERNETES_SERVICE_HOST";

        private string oldCertPath;
        private string oldServiceHost;
        private string certFile;
        private bool environmentSaved;

        private static string CreatePEM(byte[] data, string header,
            string footer)
        {
            var builder = new StringBuilder();
            builder.AppendLine(header);

            var dataStr = Convert.ToBase64String(data);
            for (var i = 0; i < dataStr.Length; i += 64)
            {
                builder.AppendLine(dataStr.Substring(i,
                    Math.Min(64, dataStr.Length - i)));
            }

            builder.AppendLine(footer);
            return builder.ToString();
        }

        private static string CreateSelfSignedCertificatePEM()
        {
            using var rsa = RSA.Create(2048);
            var request = new CertificateRequest(
                "CN=kubernetes.default.svc",
                rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            using var cert = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(1));

            return CreatePEM(cert.Export(X509ContentType.Cert),
                "-----BEGIN CERTIFICATE-----",
                "-----END CERTIFICATE-----");
        }

        private void SaveAndSetEnvironment(string caFile)
        {
            oldCertPath = Environment.GetEnvironmentVariable(
                KubernetesServiceAccountCertPathEnv);
            oldServiceHost = Environment.GetEnvironmentVariable(
                KubernetesServiceHostEnv);
            environmentSaved = true;

            Environment.SetEnvironmentVariable(
                KubernetesServiceAccountCertPathEnv, caFile);
            Environment.SetEnvironmentVariable(
                KubernetesServiceHostEnv, "kubernetes.default.svc");
        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (environmentSaved)
            {
                Environment.SetEnvironmentVariable(
                    KubernetesServiceAccountCertPathEnv, oldCertPath);
                Environment.SetEnvironmentVariable(
                    KubernetesServiceHostEnv, oldServiceHost);
            }

            if (certFile != null)
            {
                File.Delete(certFile);
                certFile = null;
            }
        }

        [TestMethod]
        public void TestOKEWorkloadIdentityKeepsHostnameVerificationEnabled()
        {
            certFile = Path.GetTempFileName();
            File.WriteAllText(certFile, CreateSelfSignedCertificatePEM());
            SaveAndSetEnvironment(certFile);

            using var iam =
                IAMAuthorizationProvider.CreateWithOKEWorkloadIdentity();
            using var client = new NoSQLClient(new NoSQLConfig
            {
                Endpoint = TestRegion.Endpoint,
                AuthorizationProvider = iam
            });

            var profileProviderField =
                typeof(IAMAuthorizationProvider).GetField("profileProvider",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(profileProviderField);
            var profileProvider = profileProviderField.GetValue(iam);
            Assert.IsInstanceOfType(profileProvider,
                typeof(OKEWorkloadIdentityProvider));

            var connectionOptionsField =
                typeof(OKEWorkloadIdentityProvider).GetField(
                    "connectionOptions",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(connectionOptionsField);

            var connectionOptions = (ConnectionOptions)
                connectionOptionsField.GetValue(profileProvider);

            Assert.AreEqual(certFile,
                connectionOptions.TrustedRootCertificateFile);
            Assert.IsNotNull(connectionOptions.TrustedRootCertificates);
            Assert.IsFalse(connectionOptions.DisableHostnameVerification);
        }

        [TestMethod]
        public void TestCertificateValidationRejectsHostnameMismatch()
        {
            var options = new ConnectionOptions
            {
                TrustedRootCertificates = new X509Certificate2Collection()
            };

            Assert.IsFalse(X509Utils.ValidateCertificate(null, null,
                SslPolicyErrors.RemoteCertificateNameMismatch, options));
        }
    }
}
