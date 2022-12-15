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
    using System.Net.Http.Headers;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;
    using static HttpConstants;
    using static Utils;

    /// <include file="IAMAuthorizationProvider.Doc.xml" path="IAMAuthorizationProvider/*"/>
    public class IAMAuthorizationProvider : IAuthorizationProvider,
        IDisposable
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

        private async Task RefreshSignatureDetails(
            bool forceProfileRefresh, CancellationToken cancellationToken)
        {
            // If there is no DelegationTokenProvider, delegationToken was
            // initialized in the constructor.
            var currentDelegationToken = DelegationTokenProvider != null ?
                await DelegationTokenProvider(cancellationToken) :
                delegationToken;

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
        /// Gets or sets the credentials for the specific user identity.
        /// </summary>
        /// <value>
        /// The credentials for the specific user identity.  This property is
        /// exclusive with <see cref="ConfigFile"/>,
        /// <see cref="ProfileName"/>, <see cref="CredentialsProvider"/>,
        /// <see cref="UseInstancePrincipal"/> and
        /// <see cref="UseResourcePrincipal"/>.
        /// </value>
        public IAMCredentials Credentials { get; set; }

        /// <summary>
        /// Gets or sets the path of the OCI configuration file.
        /// </summary>
        /// <value>
        /// The path (absolute or relative) to the OCI configuration file that
        /// is used to supply the credentials.  The default is
        /// <em>~/.oci/config</em> where <em>~</em> represents user's home
        /// directory on Unix systems and the user's profile directory (see
        /// <em>USERPROFILE</em> environment variable) on Windows systems.
        /// This property is exclusive with <see cref="Credentials"/>,
        /// <see cref="CredentialsProvider"/>,
        /// <see cref="UseInstancePrincipal"/> and
        /// <see cref="UseResourcePrincipal"/>.
        /// </value>
        public string ConfigFile { get; set; }

        /// <summary>
        /// Gets or sets the profile name in the OCI configuration file.
        /// </summary>
        /// <value>
        /// The profile name in the OCI configuration file if the OCI
        /// configuration file is used to supply the credentials.  The
        /// default profile name is <em>DEFAULT</em>.  This property is
        /// exclusive with <see cref="Credentials"/>,
        /// <see cref="CredentialsProvider"/>,
        /// <see cref="UseInstancePrincipal"/> and
        /// <see cref="UseResourcePrincipal"/>.
        /// </value>
        public string ProfileName { get; set; }

        /// <summary>
        /// Gets or sets the credentials provider delegate.
        /// </summary>
        /// <value>
        /// The credentials provider delegate to supply the credentials for
        /// the specific user's identity.   This property is
        /// exclusive with <see cref="Credentials"/>,
        /// <see cref="ConfigFile"/>, <see cref="ProfileName"/>,
        /// <see cref="UseInstancePrincipal"/> and
        /// <see cref="UseResourcePrincipal"/>.
        /// </value>
        public Func<CancellationToken, Task<IAMCredentials>>
            CredentialsProvider { get; set; }

        /// <summary>
        /// Gets or sets the value that determines whether to use an instance
        /// principal.
        /// </summary>
        /// <value>
        /// <c>true</c> to use an instance principal, otherwise <c>false</c>.
        /// The default is <c>false</c>.  The <c>true</c> value is exclusive
        /// with <see cref="Credentials"/>, <see cref="ConfigFile"/>,
        /// <see cref="ProfileName"/>, <see cref="CredentialsProvider"/> and
        /// <see cref="UseResourcePrincipal"/>.
        /// </value>
        public bool UseInstancePrincipal { get; set; }

        /// <summary>
        /// Gets or sets the federation endpoint for use with instance
        /// principal.
        /// </summary>
        /// <remarks>
        /// Federation endpoint is the endpoint used by instance principal to
        /// obtain the security token used to create the request signature.
        /// In most cases the applications do not need to set this property
        /// because the driver will detect the federation endpoint
        /// automatically.  This property is used only with instance principal
        /// and is ignored otherwise.
        /// </remarks>
        /// <value>
        /// The federation endpoint to communicate with the IAM service.  If
        /// not set, the federation endpoint is auto-detected.
        /// </value>
        public string FederationEndpoint { get; set; }

        /// <summary>
        /// Gets or sets the delegation token provider delegate.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is used only with instance principal and ignored
        /// otherwise.  The delegation token allows the instance to assume the
        /// privileges of the user for which the token was created and act on
        /// behalf of that user.
        /// </para>
        /// <para>
        /// The delegation token provider delegate is called to obtain the
        /// delegation token each time the request signature is renewed.
        /// </para>
        /// </remarks>
        /// <value>The delegation token provider.</value>
        /// <seealso cref="CreateWithInstancePrincipalForDelegation(Func{CancellationToken,Task{string}},string)"/>
        public Func<CancellationToken, Task<string>> DelegationTokenProvider
        { get; set; }

        /// <summary>
        /// Gets or sets the value that determines whether to use a resource
        /// principal.
        /// </summary>
        /// <value>
        /// <c>true</c> to use a resource principal, otherwise <c>false</c>.
        /// The default is <c>false</c>.  The <c>true</c> value is exclusive
        /// with <see cref="Credentials"/>, <see cref="ConfigFile"/>,
        /// <see cref="ProfileName"/>, <see cref="CredentialsProvider"/> and
        /// <see cref="UseResourcePrincipal"/>.
        /// </value>
        public bool UseResourcePrincipal { get; set; }

        /// <summary>
        /// Gets or sets the duration of the request signature in the cache.
        /// </summary>
        /// <remarks>
        /// This property specifies for how long the request signature may be
        /// used before the driver needs to create a new signature.  Most
        /// applications do not need to set this property as the default value
        /// of 5 minutes will suffice.  The maximum allowed value of this
        /// property is the same as the default value, 5 minutes.
        /// </remarks>
        /// <value>The cache duration of the request signature.  The default
        /// value, which is also the maximum allowed value, is 5 minutes.
        /// </value>
        public TimeSpan CacheDuration { get; set; } = MaxEntryLifetime;

        /// <summary>
        /// Gets or sets the value indicating when to automatically refresh
        /// the request signature before its expiration.
        /// </summary>
        /// <remarks>
        /// The default value of 10 seconds will suffice for most
        /// applications.
        /// </remarks>
        /// <value>
        /// The time interval indicating how long before the signature
        /// expiration the signature should be refreshed.  The default value
        /// is 10 seconds.  To disable automatic refresh, set this value to
        /// <see cref="TimeSpan.Zero"/>.
        /// </value>
        public TimeSpan RefreshAhead { get; set; } = DefaultRefreshAhead;

        /// <summary>
        /// Gets or sets the timeout for requests made to the authorization
        /// service.
        /// </summary>
        /// <remarks>
        /// Currently the driver needs to make requests to the authorization
        /// service only when using instance principal.  The default value
        /// will suffice for most applications.
        /// </remarks>
        /// <value>The timeout for requests made to the authorization service.
        /// The default is 2 minutes (120 seconds).</value>
        public TimeSpan RequestTimeout { get; set; } = DefaultRequestTimeout;

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="IAMAuthorizationProvider"/> with default configuration.
        /// </summary>
        /// <remarks>
        /// Upon using this constructor, you may set the properties described
        /// to create the desired configuration.  Otherwise the default
        /// configuration is used, which obtains credentials from the OCI
        /// configuration file using default OCI configuration file path and
        /// the default profile name as indicated in <see cref="ConfigFile"/>
        /// and <see cref="ProfileName"/>.
        /// </remarks>
        /// <seealso cref="ConfigFile"/>
        /// <seealso cref="ProfileName"/>
        public IAMAuthorizationProvider()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="IAMAuthorizationProvider"/> using the default OCI
        /// configuration file and the specified profile name.
        /// </summary>
        /// <remarks>
        /// This constructor uses the OCI configuration file at the default
        /// path specified in <see cref="ConfigFile"/>.
        /// </remarks>
        /// <param name="profileName">The profile name within the default OCI
        /// configuration file.</param>
        /// <seealso cref="ConfigFile"/>
        /// <seealso cref="ProfileName"/>
        public IAMAuthorizationProvider(string profileName)
        {
            ProfileName = profileName;
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="IAMAuthorizationProvider"/> using specified OCI
        /// configuration file and profile name.
        /// </summary>
        /// <param name="configFile">The path (absolute or relative) to the
        /// OCI configuration file.</param>
        /// <param name="profileName">Name of the profile within the OCI
        /// configuration file.</param>
        public IAMAuthorizationProvider(string configFile,
            string profileName)
        {
            ConfigFile = configFile;
            ProfileName = profileName;
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="IAMAuthorizationProvider"/> with the specified
        /// credentials.
        /// </summary>
        /// <param name="credentials">Credentials for the specific user
        /// identity.</param>
        /// <seealso cref="Credentials"/>
        public IAMAuthorizationProvider(IAMCredentials credentials)
        {
            Credentials = credentials;
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="IAMAuthorizationProvider"/> using specified credentials
        /// provider delegate.
        /// </summary>
        /// <param name="credentialsProvider">Credentials provider delegate
        /// used to obtain the user's credentials.</param>
        /// <seealso cref="CredentialsProvider"/>
        public IAMAuthorizationProvider(
            Func<CancellationToken, Task<IAMCredentials>> credentialsProvider)
        {
            CredentialsProvider = credentialsProvider;
        }

        /// <summary>
        /// Creates a new instance of <see cref="IAMAuthorizationProvider"/>
        /// using the resource principal.
        /// </summary>
        /// <returns>A new instance of <see cref="IAMAuthorizationProvider"/>
        /// that uses the resource principal.</returns>
        public static IAMAuthorizationProvider
            CreateWithResourcePrincipal() => new IAMAuthorizationProvider
            {
                UseResourcePrincipal = true
            };

        /// <summary>
        /// Creates a new instance of <see cref="IAMAuthorizationProvider"/>
        /// using the instance principal.
        /// </summary>
        /// <param name="federationEndpoint">(Optional) The federation
        /// endpoint.  If not specified, the federation endpoint is
        /// auto-detected.  Most applications do not need to specify this
        /// parameter.</param>
        /// <returns>A new instance of <see cref="IAMAuthorizationProvider"/>
        /// that uses the instance principal.</returns>
        public static IAMAuthorizationProvider CreateWithInstancePrincipal(
            string federationEndpoint = null) =>
            new IAMAuthorizationProvider
            {
                UseInstancePrincipal = true,
                FederationEndpoint = federationEndpoint
            };

        /// <summary>
        /// Creates a new instance of <see cref="IAMAuthorizationProvider"/>
        /// using an instance principal with a delegation token.
        /// </summary>
        /// <remarks>
        /// The delegation token allows the instance to assume the privileges
        /// of the user for which the token was created.
        /// </remarks>
        /// <param name="delegationToken">The delegation token.</param>
        /// <param name="federationEndpoint">(Optional) The federation
        /// endpoint.  If not specified, the federation endpoint is
        /// auto-detected.  Most applications do not need to specify this
        /// parameter.</param>
        /// <returns>A new instance of <see cref="IAMAuthorizationProvider"/>
        /// that uses the instance principal with the specified delegation
        /// token.</returns>
        public static IAMAuthorizationProvider
            CreateWithInstancePrincipalForDelegation(string delegationToken,
                string federationEndpoint = null) =>
            new IAMAuthorizationProvider
            {
                UseInstancePrincipal = true,
                delegationToken = delegationToken,
                FederationEndpoint = federationEndpoint
            };

        /// <summary>
        /// Creates a new instance of <see cref="IAMAuthorizationProvider"/>
        /// using an instance principal with a delegation token using the
        /// specified delegation token provider delegate.
        /// </summary>
        /// <remarks>
        /// The delegation token allows the instance to assume the privileges
        /// of the user for which the token was created.  The delegation token
        /// provider delegate will be used to obtain the delegation token each
        /// time the request signature is renewed.
        /// </remarks>
        /// <param name="delegationTokenProvider">The delegation token
        /// provider delegate.</param>
        /// <param name="federationEndpoint">(Optional) The federation
        /// endpoint.  If not specified, the federation endpoint is
        /// auto-detected.  Most applications do not need to specify this
        /// parameter.</param>
        /// <returns>A new instance of <see cref="IAMAuthorizationProvider"/>
        /// that uses the instance principal and the specified delegation
        /// token provider delegate.</returns>
        public static IAMAuthorizationProvider
            CreateWithInstancePrincipalForDelegation(
                Func<CancellationToken, Task<string>> delegationTokenProvider,
                string federationEndpoint = null) =>
            new IAMAuthorizationProvider
            {
                UseInstancePrincipal = true,
                DelegationTokenProvider = delegationTokenProvider,
                FederationEndpoint = federationEndpoint
            };

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
                if (UseInstancePrincipal || Credentials != null ||
                    ConfigFile != null || ProfileName != null ||
                    CredentialsProvider != null)
                {
                    throw new ArgumentException(
                        "Cannot specify UseInstancePrincipal, Credentials, " +
                        "ConfigFile, ProfileName or CredentialsProvider " +
                        "properties together with UseResourcePrincipal " +
                        "property");
                }
                profileProvider = new ResourcePrincipalProvider();
            }
            else if (UseInstancePrincipal)
            {
                if (Credentials != null || ConfigFile != null ||
                    ProfileName != null || CredentialsProvider != null)
                {
                    throw new ArgumentException(
                        "Cannot specify Credentials, ConfigFile, " +
                        "ProfileName or CredentialsProvider properties " +
                        "together with UseResourcePrincipal property");
                }
                profileProvider = new InstancePrincipalProvider(
                    FederationEndpoint, RequestTimeout,
                    config.ConnectionOptions);
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

        /// <summary>
        /// Obtains and adds the required HTTP headers for authorization with
        /// IAM.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is called by the driver to obtain and apply the
        /// authorization information to the request.  You do not need
        /// to call this method.
        /// </para>
        /// <para>
        /// The required information includes the authorization generated
        /// based on the request signature, the timestamp of the request,
        /// the compartment id if present and the delegation token if used
        /// with instance principal.
        /// </para>
        /// </remarks>
        /// <param name="request">The <see cref="Request"/> object
        /// representing the running operation.</param>
        /// <param name="headers">HTTP headers collection to which the
        /// implementation needs to add the required authorization headers.
        /// </param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><see cref="Task"/> that completes when the required
        /// authorization headers are obtained and added to the
        /// <paramref name="headers"/> collection.</returns>
        /// <exception cref="AuthorizationException">If failed to generate
        /// the request signature.</exception>
        /// <seealso cref="IAuthorizationProvider.ApplyAuthorizationAsync"/>
        public async Task ApplyAuthorizationAsync(Request request,
            HttpRequestHeaders headers, CancellationToken cancellationToken)
        {
            var isInvalidAuth =
                request.LastException is InvalidAuthorizationException;

            if (isInvalidAuth || NeedSignatureRefresh())
            {
                await RefreshSignatureDetails(isInvalidAuth,
                    cancellationToken);
                if (renewInterval != default)
                {
                    lock (providerLock)
                    {
                        ScheduleRenew();
                    }
                }
            }

            lock (providerLock)
            {
                headers.Add(Authorization, signatureDetails.Header);
                headers.Add(Date, signatureDetails.DateStr);

                if (delegationToken != null)
                {
                    headers.Add(OBOTokenHeader, delegationToken);
                }


                /*
                 * If request doesn't has compartment id, set the tenant id as
                 * the default compartment, which is the root compartment in
                 * IAM if using user principal. If using an instance principal
                 * this value is null.
                 */
                var compartment = request.Compartment ?? signatureDetails.TenantId;

                if (compartment != null)
                {
                    headers.Add(CompartmentId, compartment);
                }
            }
        }

        /// <inheritdoc cref="NoSQLClient.Dispose(bool)"/>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                renewCancelSource?.Cancel();
                profileProvider?.Dispose();
                disposed = true;
            }
        }

        /// <summary>
        /// Releases resources used by this
        /// <see cref="IAMAuthorizationProvider"/> instance.
        /// </summary>
        /// <remarks>
        /// Applications should not call this method.  The driver will call
        /// this method when <see cref="NoSQLClient"/> instance is disposed.
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

}
