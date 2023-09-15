/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using static HttpConstants;
    using static Utils;

    public partial class IAMAuthorizationProvider
    {
        private static readonly TimeSpan MaxEntryLifetime =
            TimeSpan.FromSeconds(300);
        private static readonly TimeSpan DefaultRefreshAhead =
            TimeSpan.FromSeconds(10);
        private static readonly TimeSpan DefaultRequestTimeout =
            TimeSpan.FromSeconds(120);

        private const string SigningHeaders = "(request-target) host date";

        private const string SigningHeadersWithOBO =
            SigningHeaders + " " + OBOTokenHeader;

        internal const string DefaultProfileName = "DEFAULT";

        private AuthenticationProfileProvider profileProvider;
        private string serviceHost;
        private string delegationToken;
        private SignatureDetails signatureDetails;
        private TimeSpan renewInterval;
        private CancellationTokenSource renewCancelSource;

        // Optimization to avoid async lock for common case where signature
        // refresh is not required.
        private readonly object providerLock = new object();
        // Used to obtain AuthenticationProfile.
        private readonly SemaphoreSlim providerAsyncLock =
            new SemaphoreSlim(1, 1);

        private bool disposed;

        private class SignatureDetails
        {
            internal DateTime Time { get; }
            internal string DateStr { get; }
            internal string Header { get; }
            internal string TenantId { get; }

            internal SignatureDetails(DateTime time, string dateStr,
                string header, string tenantId)
            {
                Time = time;
                DateStr = dateStr;
                Header = header;
                TenantId = tenantId;
            }
        }
        private string GetSigningContent(string dateStr,
            string currentDelegationToken)
        {
            var content =
                $"{RequestTarget}: post /{NoSQLDataPath}\n" +
                $"{Host}: {serviceHost}\n" +
                $"{Date}: {dateStr}";

            if (currentDelegationToken != null)
            {
                content += $"\n{OBOTokenHeader}: {currentDelegationToken}";
            }

            return content;
        }

        private async Task<SignatureDetails> CreateSignatureDetails(
            bool forceProfileRefresh, string currentDelegationToken,
            CancellationToken cancellationToken)
        {
            AuthenticationProfile profile;

            // This simplification allows to avoid managing thread safety in
            // all various profile providers (since signature creation is
            // an infrequent operation).
            await providerAsyncLock.WaitAsync(cancellationToken);
            try
            {
                profile = await profileProvider.GetProfileAsync(
                    forceProfileRefresh, cancellationToken);
            }
            finally
            {
                providerAsyncLock.Release();
            }

            var dateTime = DateTime.UtcNow;
            var dateStr = dateTime.ToString("r");

            string signature;
            try
            {
                signature = CreateSignature(
                    GetSigningContent(dateStr, currentDelegationToken),
                    profile.PrivateKey);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    $"Error signing request: {ex.Message}", ex);
            }

            var header = GetSignatureHeader(
                currentDelegationToken == null ?
                    SigningHeaders : SigningHeadersWithOBO,
                profile.KeyId, signature);

            return new SignatureDetails(dateTime, dateStr, header,
                profile.TenantId);
        }

        private async Task<string> LoadDelegationToken(
            CancellationToken cancellationToken)
        {
            try
            {
                if (DelegationTokenProvider != null)
                {
                    var token = await DelegationTokenProvider(
                        cancellationToken);
                    if (string.IsNullOrEmpty(token))
                    {
                        throw new ArgumentException(
                            "Retrieved null or empty delegation token");
                    }

                    return token;
                }
                
                if (DelegationTokenFile != null)
                {
                    var token = string.Join("",
                        await File.ReadAllLinesAsync(DelegationTokenFile,
                            cancellationToken));
                    Debug.Assert(token != null);
                    if (token.Length == 0)
                    {
                        throw new ArgumentException(
                            "Retrieved empty delegation token from file " +
                            DelegationTokenFile);
                    }

                    return token;
                }

                return delegationToken;
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "Failed to retrieve delegation token" +
                    (DelegationTokenFile != null
                        ? $" from file \"{DelegationTokenFile}\""
                        : "") + $": {ex.Message}", ex);
            }
        }

        private async Task RefreshSignatureDetails(
            bool forceProfileRefresh, CancellationToken cancellationToken)
        {
            // If there is no DelegationTokenProvider or DelegationTokenFile,
            // delegationToken could have been initialized in the constructor.
            var currentDelegationToken =
                DelegationTokenProvider != null || DelegationTokenFile != null
                    ? await LoadDelegationToken(cancellationToken)
                    : delegationToken;

            var currentSignatureDetails = await CreateSignatureDetails(
                forceProfileRefresh, currentDelegationToken,
                cancellationToken);

            lock (providerLock)
            {
                delegationToken = currentDelegationToken;
                signatureDetails = currentSignatureDetails;
            }
        }

        private void ScheduleRenew()
        {
            renewCancelSource?.Cancel();
            renewCancelSource = new CancellationTokenSource();
            Task.Run(async () =>
            {
                await Task.Delay(renewInterval, renewCancelSource.Token);
                try
                {
                    await RefreshSignatureDetails(false, renewCancelSource.Token);
                }
                catch (Exception)
                {
                    // This exception will not be handled so we don't rethrow
                    // but only log the error somehow and return without
                    // rescheduling. The user will get the error when
                    // CreateSignatureDetails() is called again by
                    // called again by GetAuthorizationPropertiesAsync().
                    return;
                }
                ScheduleRenew();
            });
        }

        private bool NeedSignatureRefresh()
        {
            lock (providerLock)
            {
                return signatureDetails == null || DateTime.UtcNow >
                    signatureDetails.Time + CacheDuration;
            }
        }

        /// <summary>
        /// Validates and configures the authorization provider.
        /// </summary>
        /// <remarks>
        /// This method will be called when <see cref="NoSQLClient"/> instance
        /// is created.  You do not need to call this method.
        /// </remarks>
        /// <param name="config">The initial configuration.</param>
        /// <exception cref="ArgumentException">If any mutually exclusive
        /// properties are set together or any of the credentials set as
        /// <see cref="Credentials"/> or in OCI configuration file are invalid
        /// or missing.</exception>
        /// <seealso cref="IAuthorizationProvider.ConfigureAuthorization"/>
        public void ConfigureAuthorization(NoSQLConfig config)
        {
            if (UseResourcePrincipal)
            {
                if (UseInstancePrincipal || UseSessionToken ||
                    Credentials != null || ConfigFile != null ||
                    ProfileName != null || CredentialsProvider != null)
                {
                    throw new ArgumentException(
                        "Cannot specify UseInstancePrincipal, " +
                        "UseSessionToken, Credentials, ConfigFile, " +
                        "ProfileName or CredentialsProvider properties " +
                        "together with UseResourcePrincipal property");
                }
                profileProvider = new ResourcePrincipalProvider();
            }
            else if (UseInstancePrincipal)
            {
                if (UseSessionToken || Credentials != null ||
                    ConfigFile != null || ProfileName != null ||
                    CredentialsProvider != null)
                {
                    throw new ArgumentException(
                        "Cannot specify UseSessionToken, Credentials, " +
                        "ConfigFile, ProfileName or CredentialsProvider " +
                        "properties together with UseResourcePrincipal " +
                        "property");
                }
                
                profileProvider = new InstancePrincipalProvider(
                    FederationEndpoint, RequestTimeout,
                    config.ConnectionOptions);
            }
            else if (UseSessionToken)
            {
                if (Credentials != null || CredentialsProvider != null)
                {
                    throw new ArgumentException(
                        "Cannot specify Credentials or CredentialsProvider " +
                        "properties together with UseSessionToken property");
                }

                profileProvider = new OCIConfigProfileProvider(ConfigFile,
                    ProfileName,
                    profile => new SessionTokenProfileProvider(profile));
            }
            else if (Credentials != null)
            {
                if (ConfigFile != null || ProfileName != null ||
                    CredentialsProvider != null)
                {
                    throw new ArgumentException(
                        "Cannot specify ConfigFile, ProfileName or " +
                        "CredentialsProvider properties together with " +
                        "Credentials property");
                }

                profileProvider = new CredentialsProfileProvider(Credentials);
            }
            else if (CredentialsProvider != null)
            {
                if (ConfigFile != null || ProfileName != null)
                {
                    throw new ArgumentException(
                        "Cannot specify ConfigFile or ProfileName " +
                        "properties together with CredentialsProvider " +
                        "property");
                }

                profileProvider = new UserProfileProvider(
                    CredentialsProvider);
            }
            else
            {
                profileProvider = new OCIConfigProfileProvider(ConfigFile,
                    ProfileName);
            }

            if (DelegationTokenProvider != null ||
                DelegationTokenFile != null)
            {
                if (!UseInstancePrincipal)
                {
                    throw new ArgumentException(
                        "Cannot specify delegation token provider or " +
                        "delegation token file if not using instance " +
                        "principal");
                }

                if (delegationToken != null)
                {
                    throw new ArgumentException(
                        "Cannot specify delegation token provider or " +
                        "delegation token file if delegation token is " +
                        "already provided");
                }

                if (DelegationTokenProvider != null &&
                    DelegationTokenFile != null)
                {
                    throw new ArgumentException(
                        "Cannot specify delegation token provider together " +
                        "with delegation token file");
                }
            }

            if (CacheDuration <= TimeSpan.Zero ||
                CacheDuration > MaxEntryLifetime)
            {
                throw new ArgumentException(
                    "Invalid CacheDuration value: " +
                    $"{CacheDuration.TotalSeconds} seconds, must be " +
                    "positive and no greater than " +
                    $"{MaxEntryLifetime.TotalSeconds} seconds",
                    nameof(CacheDuration));
            }

            if (RefreshAhead != TimeSpan.Zero)
            {
                if (RefreshAhead < TimeSpan.Zero)
                {
                    throw new ArgumentException(
                        "Invalid RefreshAhead value: " +
                        $"{RefreshAhead.TotalSeconds}, cannot be negative",
                        nameof(RefreshAhead));
                }

                if (RefreshAhead < CacheDuration)
                {
                    renewInterval = CacheDuration - RefreshAhead;
                }
            }

            // Special case for cloud where the region may be specified in OCI
            // config file or as part of resource principal environment.  In
            // this case we try to get the region from the auth provider and
            // retry getting the uri from this region.
            if (config.Uri == null)
            {
                Debug.Assert(config.Region == null);
                config.Region = Region.FromRegionId(profileProvider.Region);
                config.InitUri();
            }

            // config.Init() will throw if Uri is still null
            serviceHost = config.Uri?.Host;
        }

    }

}
