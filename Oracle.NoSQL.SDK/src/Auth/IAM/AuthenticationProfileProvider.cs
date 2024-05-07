/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
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
    using static Utils;
    using static OCIConfigFileConstants;

    internal static class OCIConfigFileConstants
    {
        internal const string DefaultProfile =
            IAMAuthorizationProvider.DefaultProfileName;
        internal const string TenancyProp = "tenancy";
        internal const string UserProp = "user";
        internal const string FingerprintProp = "fingerprint";
        internal const string KeyFileProp = "key_file";
        internal const string PassphraseProp = "pass_phrase";
        internal const string RegionProp = "region";
        internal const string SessionTokenFileProp = "security_token_file";
    }

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

        internal virtual string RegionId => null;

        internal virtual bool IsProfileValid => true;

        internal virtual TimeSpan ProfileTTL => TimeSpan.MaxValue;

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

        // from OCI config file
        internal CredentialsProfileProvider(Dictionary<string, string> profile)
            : this(new IAMCredentials
            {
                TenantId = profile.GetValueOrDefault(TenancyProp),
                UserId = profile.GetValueOrDefault(UserProp),
                PrivateKeyFile = profile.GetValueOrDefault(KeyFileProp),
                Fingerprint = profile.GetValueOrDefault(FingerprintProp),
                Passphrase = profile.GetValueOrDefault(
                    PassphraseProp)?.ToCharArray()
            }, true)
        {
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

    internal class SessionTokenProfileProvider : AuthenticationProfileProvider
    {
        private readonly string tenantId;
        private readonly string privateKeyFile;
        private readonly string sessionTokenFile;
        private char[] passphrase;
        private RSA privateKey;
        private bool disposed;

        internal SessionTokenProfileProvider(
            Dictionary<string, string> profile)
        {
            tenantId = profile.GetValueOrDefault(TenancyProp);

            if (string.IsNullOrEmpty(tenantId))
            {
                throw new ArgumentException("Missing tenant id");
            }

            if (!IsValidOCID(tenantId))
            {
                throw new ArgumentException(
                    $"Tenant id is not a valid OCID: {tenantId}",
                    nameof(tenantId));
            }

            privateKeyFile = profile.GetValueOrDefault(KeyFileProp);


            if (string.IsNullOrEmpty(privateKeyFile))
            {
                throw new ArgumentException("Missing private key file name");
            }

            passphrase = profile.GetValueOrDefault(PassphraseProp)
                ?.ToCharArray();

            sessionTokenFile = profile.GetValueOrDefault(
                SessionTokenFileProp);

            if (string.IsNullOrEmpty(sessionTokenFile))
            {
                throw new ArgumentException(
                    "Missing session token file name");
            }
        }

        // Erase sensitive info when no longer needed.
        private void CheckEraseCredentials()
        {
            if (passphrase != null)
            {
                Array.Clear(passphrase, 0, passphrase.Length);
                passphrase = null;
            }
        }

        internal override async Task<AuthenticationProfile> GetProfileAsync(
            bool forceRefresh, CancellationToken cancellationToken)
        {
            if (privateKey == null)
            {
                try
                {
                    privateKey =
                        await PemPrivateKeyUtils.GetFromFileAsync(
                            privateKeyFile, passphrase, cancellationToken);
                }
                finally
                {
                    CheckEraseCredentials();
                }
            }

            string sessionToken;
            try
            {
                sessionToken = string.Join("", await File.ReadAllLinesAsync(
                    sessionTokenFile, cancellationToken));
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "Failed to retrieve security token from file " +
                    $"{sessionTokenFile}: {ex.Message}");
            }

            if (sessionToken.Length == 0)
            {
                throw new ArgumentException(
                    $"Security token from file {sessionTokenFile} is empty");
            }

            return new AuthenticationProfile("ST$" + sessionToken, privateKey,
                tenantId);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                privateKey?.Clear();
                CheckEraseCredentials();
                disposed = true;
            }
        }
    }

    internal class OCIConfigProfileProvider : AuthenticationProfileProvider
    {
        private readonly AuthenticationProfileProvider provider;

        internal OCIConfigProfileProvider(string configFile,
            string profileName,
            Func<Dictionary<string,string>, AuthenticationProfileProvider>
                createProviderFunc)
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
                provider = createProviderFunc(profile);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "Error retrieving credentials from config file " +
                    $"{configFile}, profile {profileName}", ex);
            }

            RegionId = profile.GetValueOrDefault(RegionProp);
        }

        internal OCIConfigProfileProvider(string configFile,
            string profileName) : this(configFile, profileName,
            profile => new CredentialsProfileProvider(profile))
        {
        }

        internal override Task<AuthenticationProfile> GetProfileAsync(
            bool forceRefresh, CancellationToken cancellationToken) =>
            provider.GetProfileAsync(forceRefresh, cancellationToken);

        internal override string RegionId { get; }

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
