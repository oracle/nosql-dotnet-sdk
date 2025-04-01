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
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using static HttpConstants;

    /// <include file="IAMAuthorizationProvider.Doc.xml" path="IAMAuthorizationProvider/*"/>
    public partial class IAMAuthorizationProvider : IAuthorizationProvider,
        IDisposable
    {
        /// <summary>
        /// Gets or sets the credentials for the specific user identity.
        /// </summary>
        /// <value>
        /// The credentials for the specific user identity.  This property is
        /// exclusive with <see cref="ConfigFile"/>,
        /// <see cref="ProfileName"/>, <see cref="CredentialsProvider"/>,
        /// <see cref="UseInstancePrincipal"/>,
        /// <see cref="UseResourcePrincipal"/>, <see cref="UseSessionToken"/>
        /// and <see cref="UseOKEWorkloadIdentity"/>.
        /// </value>
        public IAMCredentials Credentials { get; set; }

        /// <summary>
        /// Gets or sets the path of the OCI configuration file.
        /// </summary>
        /// <value>
        /// The path (absolute or relative) to the OCI configuration file that
        /// is used to supply the credentials.  The default is
        /// <c>~/.oci/config</c> where <c>~</c> represents user's home
        /// directory on Unix systems and the user's profile directory (see
        /// <c>USERPROFILE</c> environment variable) on Windows systems.
        /// This property is exclusive with <see cref="Credentials"/>,
        /// <see cref="CredentialsProvider"/>,
        /// <see cref="UseInstancePrincipal"/>,
        /// <see cref="UseResourcePrincipal"/> and
        /// <see cref="UseOKEWorkloadIdentity"/>.
        /// </value>
        public string ConfigFile { get; set; }

        /// <summary>
        /// Gets or sets the profile name in the OCI configuration file.
        /// </summary>
        /// <value>
        /// The profile name in the OCI configuration file if the OCI
        /// configuration file is used to supply the credentials.  The
        /// default profile name is <c>DEFAULT</c>.  This property is
        /// exclusive with <see cref="Credentials"/>,
        /// <see cref="CredentialsProvider"/>,
        /// <see cref="UseInstancePrincipal"/>,
        /// <see cref="UseResourcePrincipal"/> and
        /// <see cref="UseOKEWorkloadIdentity"/>.
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
        /// <see cref="UseInstancePrincipal"/>,
        /// <see cref="UseResourcePrincipal"/>, <see cref="UseSessionToken"/>
        /// and <see cref="UseOKEWorkloadIdentity"/>.
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
        /// <see cref="ProfileName"/>, <see cref="CredentialsProvider"/>,
        /// <see cref="UseResourcePrincipal"/>, <see cref="UseSessionToken"/>
        /// and <see cref="UseOKEWorkloadIdentity"/>.
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
        /// This property can only be used only with instance principal. The
        /// delegation token allows the instance to assume the privileges of
        /// the user for which the token was created and act on behalf of that
        /// user.
        /// </para>
        /// <para>
        /// The delegation token provider delegate is called to obtain the
        /// delegation token each time the request signature is renewed.
        /// </para>
        /// </remarks>
        /// <value>
        /// The delegation token file path (absolute or relative to current
        /// directory). This property is exclusive with both
        /// <see cref="DelegationTokenFile"/> property and providing
        /// delegation token directly via
        /// <see cref="CreateWithInstancePrincipalForDelegation(string,string)"/>.
        /// </value>
        /// <seealso cref="CreateWithInstancePrincipalForDelegation(Func{CancellationToken,Task{string}},string)"/>
        public Func<CancellationToken, Task<string>> DelegationTokenProvider
        { get; set; }

        /// <summary>
        /// Gets or sets the delegation token file path.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property can only be used only with instance principal. The
        /// delegation token allows the instance to assume the privileges of
        /// the user for which the token was created and act on behalf of that
        /// user.
        /// </para>
        /// <para>
        /// The delegation token file is read each time the request signature
        /// is renewed. You may also use the property in JSON configuration
        /// file (see examples).
        /// </para>
        /// </remarks>
        /// <value>
        /// The delegation token file path (absolute or relative to current
        /// directory). This property is exclusive with both
        /// <see cref="DelegationTokenProvider"/> property and providing
        /// delegation token directly via
        /// <see cref="CreateWithInstancePrincipalForDelegation(string,string)"/>.
        /// </value>
        /// <seealso cref="CreateWithInstancePrincipalForDelegationFromFile(string,string)"/>
        public string DelegationTokenFile { get; set; }

        /// <summary>
        /// Gets or sets the value that determines whether to use a resource
        /// principal.
        /// </summary>
        /// <value>
        /// <c>true</c> to use a resource principal, otherwise <c>false</c>.
        /// The default is <c>false</c>.  The <c>true</c> value is exclusive
        /// with <see cref="Credentials"/>, <see cref="ConfigFile"/>,
        /// <see cref="ProfileName"/>, <see cref="CredentialsProvider"/>,
        /// <see cref="UseInstancePrincipal"/>, <see cref="UseSessionToken"/>
        /// and <see cref="UseOKEWorkloadIdentity"/>.
        /// </value>
        public bool UseResourcePrincipal { get; set; }

        /// <summary>
        /// Gets or sets a value that determines whether to use authorization
        /// for Oracle Container Engine for Kubernetes (OKE) workload
        /// identity. This authorization can only be used inside Kubernetes
        /// pods.
        /// </summary>
        /// <remarks>
        /// <para>
        /// For information on Container Engine for Kubernetes, see
        /// <see href="https://docs.oracle.com/en-us/iaas/Content/ContEng/Concepts/contengoverview.htm">
        /// Overview of Container Engine for Kubernetes
        /// </see>. Also see
        /// <see href="https://docs.oracle.com/en-us/iaas/Content/ContEng/Tasks/contenggrantingworkloadaccesstoresources.htm">
        /// Granting Workloads Access to OCI Resources
        /// </see> for more details on OKE workload identity.
        /// </para>
        /// <para>
        /// Using OKE workload identity requires service account token. By
        /// default, the provider will load service account token from the
        /// default file path
        /// <c>/var/run/secrets/kubernetes.io/serviceaccount/token</c>.
        /// You may override this and provide your own service account token
        /// by creating <see cref="IAMAuthorizationProvider"/> in 3 different
        /// ways:
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// By calling <see cref="CreateWithOKEWorkloadIdentity(string)"/> and
        /// passing service account token string.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// By calling <see cref="CreateWithOKEWorkloadIdentityAndTokenFile(string)"/>
        /// and passing a path to service account token file. Alternatively,
        /// you may set <see cref="ServiceAccountTokenFile"/> property. This
        /// file will be read every time the SDK needs to obtain security
        /// token from IAM.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// By calling
        /// <see cref="CreateWithOKEWorkloadIdentity(Func{CancellationToken,Task{string}})"/>
        /// and passing a custom provider delegate to load service account
        /// token. This delegate will be invoked every time the SDK needs to
        /// obtain security token from IAM.
        /// </description>
        /// </item>
        /// </list>
        /// </para>
        /// </remarks>
        /// <value>
        /// <c>true</c> to use an OKE workload identity, otherwise
        /// <c>false</c>. The default is <c>false</c>.  The <c>true</c> value
        /// is exclusive with <see cref="Credentials"/>, <see cref="ConfigFile"/>,
        /// <see cref="ProfileName"/>, <see cref="CredentialsProvider"/>,
        /// <see cref="UseResourcePrincipal"/>,
        /// <see cref="UseInstancePrincipal"/> and
        /// <see cref="UseSessionToken"/>.
        /// </value>
        public bool UseOKEWorkloadIdentity { get; set; }

        /// <summary>
        /// Gets or sets the service account token file path.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This property is only used with OKE workload identity. Use this
        /// property to provide a path to service account token file. Service
        /// account token will be reloaded from this file when obtaining IAM
        /// security token.
        /// </para>
        /// <para>
        /// You may also use this property in JSON configuration file (see
        /// examples). This property is exclusive with using service account
        /// token string or service account token provider with
        /// <see cref="M:Oracle.NoSQL.SDK.IAMAuthorizationProvider.CreateWithOKEWorkloadIdentity"/>.
        /// Alternative to this property, you may also create
        /// <see cref="IAMAuthorizationProvider"/> instance via
        /// <see cref="CreateWithOKEWorkloadIdentityAndTokenFile"/>
        /// </para>
        /// </remarks>
        /// <value>
        /// The service account token file path (absolute or relative to the
        /// current directory). Defaults to
        /// <c>/var/run/secrets/kubernetes.io/serviceaccount/token</c>.
        /// </value>
        /// <seealso cref="UseOKEWorkloadIdentity"/>
        /// <seealso cref="CreateWithOKEWorkloadIdentityAndTokenFile"/>
        public string ServiceAccountTokenFile { get; set; }

        /// <summary>
        /// Gets or sets the value that determines whether to use a session
        /// token.
        /// </summary>
        /// <remarks>
        /// Because this method uses OCI Configuration File, you may specify
        /// the path to the configuration file and the profile within the
        /// configuration file using one of the
        /// <see cref="M:Oracle.NoSQL.SDK.IAMAuthorizationProvider.CreateWithSessionToken*"/>
        /// methods or properties <see cref="ConfigFile"/> and
        /// <see cref="ProfileName"/>. The same defaults apply.
        /// </remarks>
        /// <value>
        /// <c>true</c> to use session token, otherwise <c>false</c>.
        /// The default is <c>false</c>.  The <c>true</c> value is exclusive
        /// with <see cref="Credentials"/>, <see cref="CredentialsProvider"/>,
        /// <see cref="UseInstancePrincipal"/>,
        /// <see cref="UseResourcePrincipal"/> and
        /// <see cref="UseOKEWorkloadIdentity"/>.
        /// </value>
        public bool UseSessionToken { get; set; }

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
        /// service only when using instance principal or OKE workload
        /// identity. The default value will suffice for most applications.
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
        /// to create the desired configuration.  Otherwise, the default
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
        /// configuration file. If <c>null</c>, default profile name will be
        /// used (see <see cref="ProfileName"/>).</param>
        /// <seealso cref="ConfigFile"/>
        /// <seealso cref="ProfileName"/>
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
                DelegationToken = delegationToken,
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
        /// Creates a new instance of <see cref="IAMAuthorizationProvider"/>
        /// using an instance principal with a delegation token using the
        /// specified delegation file.
        /// </summary>
        /// <remarks>
        /// The delegation token allows the instance to assume the privileges
        /// of the user for which the token was created.  The delegation token
        /// file will be read to obtain the delegation token each time the
        /// request signature is renewed.
        /// </remarks>
        /// <param name="delegationTokenFile">Path to the delegation token
        /// file.</param>
        /// <param name="federationEndpoint">(Optional) The federation
        /// endpoint.  If not specified, the federation endpoint is
        /// auto-detected.  Most applications do not need to specify this
        /// parameter.</param>
        /// <returns>A new instance of <see cref="IAMAuthorizationProvider"/>
        /// that uses the instance principal and the specified delegation
        /// token file.</returns>
        /// <seealso cref="DelegationTokenFile"/>
        public static IAMAuthorizationProvider
            CreateWithInstancePrincipalForDelegationFromFile(
                string delegationTokenFile,
                string federationEndpoint = null) =>
            new IAMAuthorizationProvider
            {
                UseInstancePrincipal = true,
                DelegationTokenFile = delegationTokenFile,
                FederationEndpoint = federationEndpoint
            };

        /// <summary>
        /// Creates a new instance of <see cref="IAMAuthorizationProvider"/>
        /// using OKE workload identity.
        /// </summary>
        /// <remarks>
        /// For more information see <see cref="UseOKEWorkloadIdentity"/>.
        /// This method allows you to specify optional service account token
        /// string. If not specified, the service account token will be read
        /// from the default file path
        /// <c>/var/run/secrets/kubernetes.io/serviceaccount/token</c>.
        /// </remarks>
        /// <param name="serviceAccountToken">(Optional) Service account token
        /// string.</param>
        /// <returns>A new instance of <see cref="IAMAuthorizationProvider"/>
        /// using OKE workload identity.</returns>
        /// <seealso cref="UseOKEWorkloadIdentity"/>
        public static IAMAuthorizationProvider CreateWithOKEWorkloadIdentity(
            string serviceAccountToken = null) =>
            new IAMAuthorizationProvider
            {
                UseOKEWorkloadIdentity = true,
                ServiceAccountToken = serviceAccountToken
            };

        /// <summary>
        /// Creates a new instance of <see cref="IAMAuthorizationProvider"/>
        /// using OKE workload identity and specified service account token
        /// provider delegate.
        /// </summary>
        /// <remarks>
        /// For more information see <see cref="UseOKEWorkloadIdentity"/>.
        /// This method allows you to specify service account token provider
        /// delegate used to obtain the service account token.
        /// </remarks>
        /// <param name="serviceAccountTokenProvider">The service Account
        /// token provider delegate.</param>
        /// <returns>A new instance of <see cref="IAMAuthorizationProvider"/>
        /// using OKE workload identity and specified service account token
        /// provider delegate.</returns>
        public static IAMAuthorizationProvider CreateWithOKEWorkloadIdentity(
            Func<CancellationToken, Task<string>>
                serviceAccountTokenProvider) =>
            new IAMAuthorizationProvider
            {
                UseOKEWorkloadIdentity = true,
                ServiceAccountTokenProvider = serviceAccountTokenProvider
            };

        /// <summary>
        /// Creates a new instance of <see cref="IAMAuthorizationProvider"/>
        /// using OKE workload identity and specified service account token
        /// file.
        /// </summary>
        /// <remarks>
        /// For more information see <see cref="UseOKEWorkloadIdentity"/>.
        /// This method allows you to specify a path to the service account
        /// token file.
        /// </remarks>
        /// <param name="serviceAccountTokenFile">Path to the service account
        /// token file.</param>
        /// <returns>A new instance of <see cref="IAMAuthorizationProvider"/>
        /// using OKE workload identity and specified service account token
        /// file.</returns>
        public static IAMAuthorizationProvider
            CreateWithOKEWorkloadIdentityAndTokenFile(
            string serviceAccountTokenFile) =>
            new IAMAuthorizationProvider
            {
                UseOKEWorkloadIdentity = true,
                ServiceAccountTokenFile = serviceAccountTokenFile
            };
        
        /// <summary>
        /// Creates a new instance of <see cref="IAMAuthorizationProvider"/>
        /// using session token-based authentication with the default OCI
        /// configuration file and the default profile name.
        /// </summary>
        /// <returns>A new instance of <see cref="IAMAuthorizationProvider"/>
        /// that uses session-token based authentication.</returns>
        /// <seealso cref="ConfigFile"/>
        /// <seealso cref="ProfileName"/>
        /// <seealso cref="UseSessionToken"/>
        public static IAMAuthorizationProvider CreateWithSessionToken() =>
            new IAMAuthorizationProvider
            {
                UseSessionToken = true
            };

        /// <summary>
        /// Creates a new instance of <see cref="IAMAuthorizationProvider"/>
        /// using session token-based authentication with the default OCI
        /// configuration file and the specified profile name.
        /// </summary>
        /// <param name="profileName">Name of the profile within the OCI
        /// configuration file.</param>
        /// <returns>A new instance of <see cref="IAMAuthorizationProvider"/>
        /// that uses session-token based authentication.</returns>
        /// <seealso cref="ConfigFile"/>
        /// <seealso cref="ProfileName"/>
        /// <seealso cref="UseSessionToken"/>
        public static IAMAuthorizationProvider CreateWithSessionToken(
            string profileName) =>
            new IAMAuthorizationProvider(profileName)
            {
                UseSessionToken = true
            };

        /// <summary>
        /// Creates a new instance of <see cref="IAMAuthorizationProvider"/>
        /// using session token-based authentication with the specified OCI
        /// configuration file and profile name.
        /// </summary>
        /// <param name="configFile">The path (absolute or relative) to the
        /// OCI configuration file.</param>
        /// <param name="profileName">Name of the profile within the OCI
        /// configuration file. If <c>null</c>, default profile name will be
        /// used (see <see cref="ProfileName"/>).</param>
        /// <returns>A new instance of <see cref="IAMAuthorizationProvider"/>
        /// that uses session-token based authentication.</returns>
        /// <seealso cref="ConfigFile"/>
        /// <seealso cref="ProfileName"/>
        /// <seealso cref="UseSessionToken"/>
        public static IAMAuthorizationProvider CreateWithSessionToken(
            string configFile, string profileName) =>
            new IAMAuthorizationProvider(configFile, profileName)
            {
                UseSessionToken = true
            };

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
        /// <param name="message">HTTP request message.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><see cref="Task"/> that completes when the required
        /// authorization headers are obtained and added to the
        /// request <paramref name="message"/> collection.</returns>
        /// <exception cref="AuthorizationException">If failed to generate
        /// the request signature.</exception>
        /// <seealso cref="IAuthorizationProvider.ApplyAuthorizationAsync"/>
        public async Task ApplyAuthorizationAsync(Request request,
            HttpRequestMessage message, CancellationToken cancellationToken)
        {
            var isInvalidAuth =
                request.LastException is InvalidAuthorizationException;
            var contentSigned = request.NeedsContentSigned;
            
            SignatureDetails signatureDetails;
            if (isInvalidAuth || contentSigned ||
                !profileProvider.IsProfileValid ||
                NeedSignatureRefresh(CachedSignatureDetails))
            {
                signatureDetails = await CreateSignatureDetailsAsync(
                    request, message, isInvalidAuth, cancellationToken);

                if (!contentSigned)
                {
                    CachedSignatureDetails = signatureDetails;
                    if (RefreshAhead != TimeSpan.Zero)
                    {
                        ScheduleRenew();
                    }
                }
            }
            else
            {
                signatureDetails = CachedSignatureDetails;
            }

            message.Headers.Add(Authorization,
                signatureDetails.Header);
            message.Headers.Add(Date, signatureDetails.DateStr);

            if (contentSigned)
            {
                Debug.Assert(signatureDetails.Digest != null);
                message.Headers.Add(ContentSHA256,
                    signatureDetails.Digest);
            }

            if (signatureDetails.DelegationToken != null)
            {
                message.Headers.Add(OBOTokenHeader,
                    signatureDetails.DelegationToken);
            }

            /*
             * If request doesn't have compartment id, set the tenant id
             * as the default compartment, which is the root compartment
             * in IAM if using user principal. If using an instance
             * principal this value is null.
             */
            var compartment = request.Compartment ?? signatureDetails.TenantId;

            if (compartment != null)
            {
                message.Headers.Add(CompartmentId, compartment);
            }
        }

        /// <inheritdoc cref="NoSQLClient.Dispose(bool)"/>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                lock (providerLock)
                {
                    renewCancelSource?.Cancel();
                }
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
