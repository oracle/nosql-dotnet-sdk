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
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    
    internal class ResourcePrincipalProvider : SecurityTokenBasedProvider
    {
        // Environment variable names used to fetch artifacts.
        private const string OCIResourcePrincipalVersion =
            "OCI_RESOURCE_PRINCIPAL_VERSION";

        private const string KnownVersion = "2.2";

        private const string OCIResourcePrincipalRPST =
            "OCI_RESOURCE_PRINCIPAL_RPST";

        private const string OCIResourcePrincipalPrivatePEM =
            "OCI_RESOURCE_PRINCIPAL_PRIVATE_PEM";

        private const string OCIResourcePrincipalPrivatePEMPassphrase =
            "OCI_RESOURCE_PRINCIPAL_PRIVATE_PEM_PASSPHRASE";

        private const string OCIResourcePrincipalRegion =
            "OCI_RESOURCE_PRINCIPAL_REGION";

        private readonly string privateKeyFile;
        private RSA privateKey;
        private readonly string passphraseFile;
        private readonly string rpst;
        private readonly string region;
        private readonly bool rpstInFile;

        internal ResourcePrincipalProvider(IAMAuthorizationProvider iam) :
            base(iam.ProfileExpireBefore)
        {
            var version = Environment.GetEnvironmentVariable(
                OCIResourcePrincipalVersion);

            if (version == null)
            {
                throw new ArgumentException(
                    "Missing environment variable " +
                    OCIResourcePrincipalVersion);
            }

            if (version != KnownVersion)
            {
                throw new ArgumentException(
                    "Unknown or unsupported value of environment variable" +
                    $"{OCIResourcePrincipalVersion}: {version}");
            }

            var privatePEM = Environment.GetEnvironmentVariable(
                OCIResourcePrincipalPrivatePEM);

            if (privatePEM == null)
            {
                throw new ArgumentException(
                    "Missing environment variable " +
                    OCIResourcePrincipalPrivatePEM);
            }

            var passphrase = Environment.GetEnvironmentVariable(
                OCIResourcePrincipalPrivatePEMPassphrase);

            if (Path.IsPathFullyQualified(privatePEM))
            {
                if (passphrase != null &&
                    !Path.IsPathFullyQualified(passphrase))
                {
                    throw new ArgumentException(
                        "Cannot mix path and constant settings for " +
                        $"{OCIResourcePrincipalPrivatePEM} and " +
                        $"{OCIResourcePrincipalPrivatePEMPassphrase}, both " +
                        "must be paths or constants");
                }

                privateKeyFile = privatePEM;
                passphraseFile = passphrase;
            }
            else
            {
                privateKey = PemPrivateKeyUtils.GetFromString(privatePEM,
                    passphrase?.ToCharArray());
            }

            rpst = Environment.GetEnvironmentVariable(
                OCIResourcePrincipalRPST);

            if (rpst == null)
            {
                throw new ArgumentException(
                    "Missing environment variable " +
                    OCIResourcePrincipalRPST);
            }

            if (Path.IsPathFullyQualified(rpst))
            {
                rpstInFile = true;
            }

            region = Environment.GetEnvironmentVariable(
                OCIResourcePrincipalRegion);

            if (region == null)
            {
                throw new ArgumentException(
                    "Missing environment variable " +
                    OCIResourcePrincipalRegion);
            }
        }

        private protected override RSA PrivateKey => privateKey;

        private async Task<string> GetRPSTFromFileAsync(
            CancellationToken cancellationToken)
        {
            Debug.Assert(rpst != null && rpstInFile);
            try
            {
                return await File.ReadAllTextAsync(rpst, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "Error reading security token from file " +
                    $"{rpst}: {ex.Message}", ex);
            }
        }

        private async Task<char[]> GetPassphraseFromFileAsync(
            CancellationToken cancellationToken)
        {
            Debug.Assert(passphraseFile != null);
            byte[] bytes = null;
            try
            {
                bytes = await File.ReadAllBytesAsync(passphraseFile,
                    cancellationToken);
                return Encoding.UTF8.GetChars(bytes);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "Error reading private key passphrase from file " +
                    $"{passphraseFile}: {ex.Message}", ex);
            }
            finally
            {
                if (bytes != null)
                {
                    Array.Clear(bytes, 0, bytes.Length);
                }
            }
        }

        private async Task GetPrivateKeyFromFileAsync(
            CancellationToken cancellationToken)
        {
            privateKey?.Clear();

            var passphrase = passphraseFile == null ?
                null : await GetPassphraseFromFileAsync(cancellationToken);

            try
            {
                privateKey = await PemPrivateKeyUtils.GetFromFileAsync(
                    privateKeyFile, passphrase, cancellationToken);
            }
            finally
            {
                if (passphrase != null)
                {
                    Array.Clear(passphrase, 0, passphrase.Length);
                }
            }
        }

        private protected override SecurityToken
            CreateSecurityToken(string val) =>
            ResourcePrincipalSecurityToken.Create(val);

        private protected override async Task<string>
            RefreshSecurityTokenAsync(CancellationToken cancellationToken)
        {
            if (privateKeyFile != null)
            {
                await GetPrivateKeyFromFileAsync(cancellationToken);
            }

            Debug.Assert(privateKey != null);

            if (rpstInFile)
            {
                return await GetRPSTFromFileAsync(cancellationToken);
            }

            return rpst;
        }

        internal override string RegionId => region;

        protected override void Dispose(bool disposing)
        {
            if (disposing && privateKey != null)
            {
                privateKey.Clear();
                privateKey = null;
            }
        }
    }
}
