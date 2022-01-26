/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    internal class AuthenticationProfile
    {
        internal AuthenticationProfile(string keyId, RSA privateKey,
            string tenantId = null)
        {
            KeyId = keyId;
            PrivateKey = privateKey;
            TenantId = tenantId;
        }

        internal string KeyId { get; }

        internal RSA PrivateKey { get; } // need to call Dispose()

        internal string TenantId { get; }

    }

    internal abstract class AuthenticationProfileProvider : IDisposable
    {
        internal abstract Task<AuthenticationProfile> GetProfileAsync(
            bool forceRefresh, CancellationToken cancellationToken);

        internal virtual string Region => null;

        protected abstract void Dispose(bool disposing);

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

    // Note that authentication profile providers are not thread or async
    // safe.  Since retrieving AuthenticationProfile is infrequent,
    // IAMAuthorizationProvider acquires async lock (SemaphoreSlim) when
    // GetProfileAsync() is called.  This simplification allows to avoid
    // managing thread safety in various profile providers.  Region property
    // must be initialized when profile provider is constructed.

    internal class CredentialsProfileProvider : AuthenticationProfileProvider
    {
        private readonly IAMCredentials credentials;
        private readonly string keyId;
        private RSA privateKey;
        private readonly bool ownsCredentials;
        private bool disposed;

        internal CredentialsProfileProvider(IAMCredentials credentials,
            bool ownsCredentials = false)
        {
            Debug.Assert(credentials != null);
            this.credentials = credentials;
            this.ownsCredentials = ownsCredentials;
            this.credentials.Validate();
            keyId = $"{credentials.TenantId}/{credentials.UserId}/" +
                credentials.Fingerprint;
        }

        // Erase sensitive info when no longer needed.
        private void CheckEraseCredentials()
        {
            if (ownsCredentials && credentials.Passphrase != null)
            {
                Array.Clear(credentials.Passphrase, 0,
                    credentials.Passphrase.Length);
                credentials.Passphrase = null;
            }
        }

        internal override async Task<AuthenticationProfile> GetProfileAsync(
            bool forceRefresh, CancellationToken cancellationToken)
        {
            if (privateKey == null)
            {
                try
                {
                    if (credentials.PrivateKey != null)
                    {
                        privateKey = credentials.PrivateKey;
                    }
                    else if (credentials.PrivateKeyPEM != null)
                    {
                        privateKey = PemPrivateKeyUtils.GetFromString(
                            credentials.PrivateKeyPEM,
                            credentials.Passphrase);
                    }
                    else
                    {
                        Debug.Assert(credentials.PrivateKeyFile != null);
                        privateKey =
                            await PemPrivateKeyUtils.GetFromFileAsync(
                            credentials.PrivateKeyFile,
                            credentials.Passphrase, cancellationToken);
                    }
                }
                finally
                {
                    CheckEraseCredentials();
                }
            }

            return new AuthenticationProfile(keyId, privateKey,
                credentials.TenantId);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                // Clear only if we own the RSA object.
                if (privateKey != null && credentials.PrivateKey == null)
                {
                    privateKey.Clear();
                }
                CheckEraseCredentials();
                disposed = true;
            }
        }

    }

    internal class OCIConfigProfileProvider : AuthenticationProfileProvider
    {
        private const string DefaultProfile =
            IAMAuthorizationProvider.DefaultProfileName;
        private const string TenancyProp = "tenancy";
        private const string UserProp = "user";
        private const string FingerprintProp = "fingerprint";
        private const string KeyFileProp = "key_file";
        private const string PassphraseProp = "pass_phrase";
        private const string RegionProp = "region";

        private readonly CredentialsProfileProvider provider;

        internal OCIConfigProfileProvider(string configFile,
            string profileName)
        {
            if (configFile == null)
            {
                var home = Environment.GetFolderPath(
                    Environment.SpecialFolder.UserProfile,
                    Environment.SpecialFolderOption.DoNotVerify);
                configFile = Path.Join(home, ".oci", "config");
            }

            profileName ??= DefaultProfile;

            Dictionary<string, string> profile;
            try
            {
                profile = IniFile.GetProfile(configFile, profileName);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Error retrieving profile {profileName} from " +
                    $"config file {configFile}: {ex.Message}", ex);
            }

            if (profile == null)
            {
                throw new ArgumentException(
                    $"Cannot find profile {profileName} in " +
                    $"config file {configFile}");
            }

            try
            {
                provider = new CredentialsProfileProvider(new IAMCredentials
                {
                    TenantId = profile.GetValueOrDefault(TenancyProp),
                    UserId = profile.GetValueOrDefault(UserProp),
                    PrivateKeyFile = profile.GetValueOrDefault(KeyFileProp),
                    Fingerprint = profile.GetValueOrDefault(FingerprintProp),
                    Passphrase = profile.GetValueOrDefault(
                        PassphraseProp)?.ToCharArray()
                }, true);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "Error retrieving credentials from config file " +
                    $"{configFile}, profile {profileName}", ex);
            }

            Region = profile.GetValueOrDefault(RegionProp);
        }

        internal override Task<AuthenticationProfile> GetProfileAsync(
            bool forceRefresh, CancellationToken cancellationToken) =>
            provider.GetProfileAsync(forceRefresh, cancellationToken);

        internal override string Region { get; }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                provider?.Dispose();
            }
        }
    }

    internal class UserProfileProvider : AuthenticationProfileProvider
    {
        private readonly Func<CancellationToken, Task<IAMCredentials>>
            providerFunc;
        // We keep CredentialsProfileProvider here so that we can dispose it
        // before we create new provider and also when this provider is
        // disposed. We can't dispose it in GetProfileAsync() because
        // the private key will be in use.
        private CredentialsProfileProvider provider;

        internal UserProfileProvider(
            Func<CancellationToken, Task<IAMCredentials>> providerFunc)
        {
            this.providerFunc = providerFunc;
        }

        internal override async Task<AuthenticationProfile> GetProfileAsync(
            bool forceRefresh, CancellationToken cancellationToken)
        {
            try
            {
                var credentials = await providerFunc(cancellationToken);
                if (credentials == null)
                {
                    throw new ArgumentException("Credentials are null");
                }

                provider?.Dispose();
                provider = new CredentialsProfileProvider(credentials, true);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "Error retrieving credentials from user-defined " +
                    $"credentials provider: ${ex.Message}", ex);
            }

            return await provider.GetProfileAsync(forceRefresh,
                cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                provider?.Dispose();
            }
        }
    }

}
