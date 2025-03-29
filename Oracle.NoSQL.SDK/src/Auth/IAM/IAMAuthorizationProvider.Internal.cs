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
    using System.Net.Http;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using static HttpConstants;
    using static Utils;
    using static ValidateUtils;

    public partial class IAMAuthorizationProvider
    {
        private static readonly TimeSpan MaxEntryLifetime =
            TimeSpan.FromSeconds(300);
        private static readonly TimeSpan DefaultRefreshAhead =
            TimeSpan.FromSeconds(10);

        private static readonly TimeSpan DefaultProfileExpireBefore =
            TimeSpan.FromSeconds(10);
        private static readonly TimeSpan DefaultMaxProfileRefreshAhead =
            TimeSpan.FromSeconds(60);
        internal static readonly TimeSpan DefaultRequestTimeout =
            TimeSpan.FromSeconds(120);

        private const string SigningHeaders = "(request-target) host date";
        private const string ContentHeaders =
            "content-length content-type x-content-sha256";

        internal const string DefaultProfileName = "DEFAULT";

        private AuthenticationProfileProvider profileProvider;
        private string serviceHost;
        private SignatureDetails internalSignatureDetails;
        private CancellationTokenSource renewCancelSource;

        // Optimization to avoid async lock for common case where signature
        // refresh is not required.
        private readonly object providerLock = new object();
        // Used to obtain AuthenticationProfile.
        private readonly SemaphoreSlim providerAsyncLock =
            new SemaphoreSlim(1, 1);

        private bool disposed;

        private string DelegationToken { get; set; }
        
        internal string ServiceAccountToken { get; set; }

        internal Func<CancellationToken, Task<string>>
            ServiceAccountTokenProvider { get; set; }

        // Not currently exposed to the user, but different values can be used
        // in tests.
        internal TimeSpan ProfileExpireBefore { get; set; } =
            DefaultProfileExpireBefore;

        internal TimeSpan MaxProfileRefreshAhead { get; set; } =
            DefaultMaxProfileRefreshAhead;

        // We could also use volatile field, but in general using a lock is
        // recommended. This can be changed if needed.
        private SignatureDetails CachedSignatureDetails
        {
            get
            {
                lock (providerLock)
                {
                    return internalSignatureDetails;
                }
            }
            set
            {
                lock (providerLock)
                {
                    internalSignatureDetails = value;
                }
            }
        }

        private class SignatureDetails
        {
            internal DateTime Time { get; }
            internal string DateStr { get; }
            internal string Header { get; }
            internal string TenantId { get; }
            internal string DelegationToken { get; }
            // SHA256 digest of request content needed for potential
            // cross-region requests.
            internal string Digest { get; }

            internal SignatureDetails(DateTime time, string dateStr,
                string header, string tenantId, string delegationToken,
                string digest)
            {
                Time = time;
                DateStr = dateStr;
                Header = header;
                TenantId = tenantId;
                Digest = digest;
                DelegationToken = delegationToken;
            }
        }

        private class ContentSigningInfo
        {
            internal string ContentType { get; }
            internal long ContentLength { get; }
            internal string Digest { get; }

            private ContentSigningInfo(string contentType, long contentLength,
                string digest)
            {
                ContentType = contentType;
                ContentLength = contentLength;
                Digest = digest;
            }

            internal static async Task<ContentSigningInfo> Create(
                HttpRequestMessage message)
            {
                Debug.Assert(message.Content != null);
                Debug.Assert(message.Content.Headers.ContentType != null);
                var contentType =
                    message.Content.Headers.ContentType.ToString();
                Debug.Assert(message.Content.Headers.ContentLength.HasValue);
                var contentLength =
                    message.Content.Headers.ContentLength.Value;
                // .Net Core 3.1 does not have HttpContent.ReadAsStream() or
                // other sync methods to access content bytes, so we have to
                // use the async method.
                var stream = await message.Content.ReadAsStreamAsync();
                var digest =
                    Convert.ToBase64String(ComputeSHA256Digest(stream));
                return new ContentSigningInfo(contentType, contentLength, digest);
            }
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

                return DelegationToken;
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

        private static string GetSigningHeaders(bool hasContent, bool hasOBO)
        {
            var res = SigningHeaders;
            if (hasContent)
            {
                res += ' ' + ContentHeaders;
            }
            if (hasOBO)
            {
                res += ' ' + OBOTokenHeader;
            }
            return res;
        }

        // The order of headers in GetSigningHeaders() and GetSigningContent()
        // should match.
        private string GetSigningContent(string dateStr,
            string currentDelegationToken,
            ContentSigningInfo contentSigningInfo)
        {
            var content =
                $"{RequestTarget}: post /{NoSQLDataPath}\n" +
                $"{Host}: {serviceHost}\n" +
                $"{Date}: {dateStr}";

            if (contentSigningInfo != null)
            {
                content +=
                    $"\n{ContentLengthLowerCase}: {contentSigningInfo.ContentLength}\n" +
                    $"{ContentTypeLowerCase}: {contentSigningInfo.ContentType}\n" +
                    $"{ContentSHA256}: {contentSigningInfo.Digest}";
            }

            if (currentDelegationToken != null)
            {
                content += $"\n{OBOTokenHeader}: {currentDelegationToken}";
            }

            return content;
        }

        private async Task<SignatureDetails> CreateSignatureDetailsAsync(
            Request request, HttpRequestMessage message,
            bool forceProfileRefresh, CancellationToken cancellationToken)
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

            // If there is no DelegationTokenProvider or DelegationTokenFile,
            // delegationToken could have been initialized in the constructor.
            var currentDelegationToken =
                DelegationTokenProvider != null || DelegationTokenFile != null
                    ? await LoadDelegationToken(cancellationToken)
                    : DelegationToken;

            ContentSigningInfo contentSigningInfo = null;
            if (request?.NeedsContentSigned ?? false)
            {
                Debug.Assert(message != null);
                contentSigningInfo = await ContentSigningInfo.Create(message);
            }

            var dateTime = DateTime.UtcNow;
            var dateStr = dateTime.ToString("r");

            string signature;
            try
            {
                signature = CreateSignature(
                    GetSigningContent(dateStr, currentDelegationToken,
                        contentSigningInfo),
                    profile.PrivateKey);
            }
            catch (CryptographicException ex)
            {
                throw new InvalidOperationException(
                    $"Error signing request: {ex.Message}", ex);
            }

            var header = GetSignatureHeader(
                GetSigningHeaders(contentSigningInfo != null,
                    currentDelegationToken != null),
                profile.KeyId, signature);

            return new SignatureDetails(dateTime, dateStr, header,
                profile.TenantId, currentDelegationToken,
                contentSigningInfo?.Digest);
        }
        
        private void ScheduleRenew()
        {
            lock (providerLock)
            {
                // If renewCancelSource.Cancel() has been called from
                // Dispose(), make sure we don't schedule another renew.
                if (renewCancelSource?.IsCancellationRequested ?? false)
                {
                    return;
                }

                renewCancelSource?.Cancel();
                renewCancelSource = new CancellationTokenSource();

                // This captures cancellation token to make sure the task
                // below uses cancellation token from CancellationTokenSource
                // created in this invocation of ScheduleRenew(). This ensures
                // that this task will be cancelled on the next invocation of
                // ScheduleRenew().
                var cancellationToken = renewCancelSource.Token;

                // Check if the signature or the profile would expire first.
                // The reason for using MaxProfileRefreshAhead threshold is to
                // avoid too frequent signature refresh. E.g. if signature
                // duration is 5:00 and profile expires in 5:20, we might as
                // well refresh both profile and signature on next refresh
                // (at 4:50) rather than refresh only signature and then have
                // to refresh the signature and profile again in 20 seconds
                // (because of profile expiration). On the other hand, we
                // don't want to do profile/token refresh too often
                // unnecessarily because it may be an expensive operation
                // (require HTTP request).
                var needProfileRefresh =
                    profileProvider.ProfileTTL - (CacheDuration -
                    RefreshAhead) <= MaxProfileRefreshAhead;

                var renewInterval =
                    (profileProvider.ProfileTTL < CacheDuration
                        ? profileProvider.ProfileTTL
                        : CacheDuration) - RefreshAhead;
                
                // This may only happen if token has very short lifetime
                // (< RefreshAhead), in which case we don't auto-renew.
                if (renewInterval <= TimeSpan.Zero)
                {
                    return;
                }

                Task.Run(async () =>
                {
                    try
                    {

                        await Task.Delay(renewInterval, cancellationToken);
                        CachedSignatureDetails =
                            await CreateSignatureDetailsAsync(null, null,
                                needProfileRefresh, cancellationToken);
                    }
                    catch (Exception)
                    {
                        // Return if the task was canceled. If caused by
                        // signature creation, we also return without
                        // rescheduling because exception thrown here cannot
                        // be handled by the user (although we could log it).
                        // The user will likely get the same exception when
                        // CreateSignatureDetailsAsync() is called again by
                        // ApplyAuthorizationAsync().
                        return;
                    }

                    ScheduleRenew();
                }, cancellationToken);
            }
        }

        // Used by tests.
        internal void ClearSignatureCache() => CachedSignatureDetails = null;

        private bool NeedSignatureRefresh(
            SignatureDetails signatureDetails) =>
            signatureDetails == null || DateTime.UtcNow >
            signatureDetails.Time + CacheDuration;

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
            CheckPositiveTimeSpan(RequestTimeout,
                nameof(RequestTimeout) + " in authorization provider");

            if (UseResourcePrincipal)
            {
                if (UseInstancePrincipal || UseOKEWorkloadIdentity ||
                    UseSessionToken || Credentials != null ||
                    ConfigFile != null || ProfileName != null ||
                    CredentialsProvider != null)
                {
                    throw new ArgumentException(
                        "Cannot specify UseInstancePrincipal, " +
                        "UseOKEWorkloadIdentity UseSessionToken, " +
                        "Credentials, ConfigFile, ProfileName or " +
                        "CredentialsProvider properties " +
                        "together with UseResourcePrincipal property");
                }
                profileProvider = new ResourcePrincipalProvider(this);
            }
            else if (UseInstancePrincipal)
            {
                if (UseOKEWorkloadIdentity || UseSessionToken ||
                    Credentials != null || ConfigFile != null ||
                    ProfileName != null || CredentialsProvider != null)
                {
                    throw new ArgumentException(
                        "Cannot specify UseOKEWorkloadIdentity, " +
                        "UseSessionToken, Credentials, ConfigFile, " +
                        "ProfileName or CredentialsProvider properties " +
                        "together with UseResourcePrincipal " +
                        "property");
                }

                profileProvider = new InstancePrincipalProvider(this, config);
            }
            else if (UseOKEWorkloadIdentity)
            {
                if (UseSessionToken || Credentials != null ||
                    ConfigFile != null || ProfileName != null ||
                    CredentialsProvider != null)
                {
                    throw new ArgumentException(
                        "Cannot specify UseSessionToken, Credentials, " +
                        "ConfigFile, ProfileName or CredentialsProvider " +
                        "properties together with UseOKEWorkloadIdentity " +
                        "property");
                }

                profileProvider = new OKEWorkloadIdentityProvider(this);
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

                if (DelegationToken != null)
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
                    $"Invalid {nameof(CacheDuration)} value in " +
                    "authorization provider: " +
                    $"{CacheDuration.TotalSeconds} seconds, " +
                    "must be positive and no greater than " +
                    $"{MaxEntryLifetime.TotalSeconds} seconds",
                    nameof(CacheDuration));
            }

            if (RefreshAhead != TimeSpan.Zero)
            {
                CheckPositiveTimeSpan(RefreshAhead,
                    nameof(RefreshAhead) + "in authorization provider");
                if (RefreshAhead >= CacheDuration)
                {
                    RefreshAhead = TimeSpan.Zero;
                }
                else if (MaxProfileRefreshAhead < RefreshAhead)
                {
                    MaxProfileRefreshAhead = RefreshAhead;
                }
            }

            // Special case for cloud where the region may be specified in OCI
            // config file or as part of resource principal environment.  In
            // this case we try to get the region from the auth provider and
            // retry getting the uri from this region.
            if (config.Uri == null)
            {
                Debug.Assert(config.Region == null);
                config.Region = Region.FromRegionId(profileProvider.RegionId);
                config.InitUri();
            }

            // config.Init() will throw if Uri is still null
            serviceHost = config.Uri?.Host;
        }

    }

}
