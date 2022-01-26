/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

using System.Reflection;

namespace Oracle.NoSQL.SDK
{

    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using static Utils;

    internal class ResourcePrincipalProvider : AuthenticationProfileProvider
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
        private readonly string rpstFile;
        private ResourcePrincipalSecurityToken rpst;
        private readonly string region;

        internal ResourcePrincipalProvider()
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

            var rpstVal = Environment.GetEnvironmentVariable(
                OCIResourcePrincipalRPST);

            if (rpstVal == null)
            {
                throw new ArgumentException(
                    "Missing environment variable " +
                    OCIResourcePrincipalRPST);
            }

            if (Path.IsPathFullyQualified(rpstVal))
            {
                rpstFile = rpstVal;
            }
            else
            {
                rpst = ResourcePrincipalSecurityToken.Create(rpstVal);
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

        private async Task RefreshRPSTAsync(CancellationToken cancellationToken)
        {
            Debug.Assert(rpstFile != null);
            try
            {
                var rpstVal = await File.ReadAllTextAsync(rpstFile,
                    cancellationToken);
                rpst = ResourcePrincipalSecurityToken.Create(rpstVal);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "Error reading security token from file " +
                    $"{rpstFile}: {ex.Message}", ex);
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

        private async Task RefreshPrivateKeyAsync(
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

        internal override async Task<AuthenticationProfile> GetProfileAsync(
            bool forceRefresh, CancellationToken cancellationToken)
        {
            var needRefreshRPST =
                forceRefresh ||
                rpstFile != null && (rpst == null || !rpst.IsValid);

            if (needRefreshRPST)
            {
                await RefreshRPSTAsync(cancellationToken);
            }

            if (privateKeyFile != null &&
                (privateKey == null || needRefreshRPST))
            {
                await RefreshPrivateKeyAsync(cancellationToken);
            }

            Debug.Assert(rpst != null);
            Debug.Assert(privateKey != null);
            return new AuthenticationProfile("ST$" + rpst.Value, privateKey);
        }

        internal override string Region => region;

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
