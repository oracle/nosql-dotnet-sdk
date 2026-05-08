/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Net.Http;
    using System.Threading;
    using System.Threading.Tasks;
    using static ValidateUtils;

    /// <summary>
    /// Cloud Service only. A class that allows to retrieve OCI instance
    /// metadata.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This class is used internally by
    /// <see cref="IAMAuthorizationProvider"/> to retrieve OCI instance
    /// metadata when using Instance Principal authentication.
    /// </para>
    /// <para>
    /// You can use this class to retrieve the region of the running instance
    /// when using Instance Principal or OKE workload authentication and when
    /// you want to use the region of the running instance to connect to the
    /// NoSQL service.
    /// </para>
    /// </remarks>
    public sealed class InstanceMetadataClient : IDisposable
    {
        // OCI instance metadata service v2 base URL. IMDS is exposed by OCI
        // as a link-local HTTP endpoint and requires Authorization: Bearer
        // Oracle for v2 requests.
        private const string MetadataServiceBaseUrl =
            "http://169.254.169.254/opc/v2/";

        // The authorization header to send to metadata service v2.
        private const string AuthorizationHeaderValue = "Bearer Oracle";

        private readonly AuthHttpClient httpClient;
        private string metadataUrl;
        private readonly bool ownsHttpClient;

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="InstanceMetadataClient"/>.
        /// </summary>
        /// <remarks>
        /// This constructor will initialize
        /// <see cref="InstanceMetadataClient"/> with default request timeout
        /// of 120 seconds (2 minutes).
        /// </remarks>
        public InstanceMetadataClient() :
            this(IAMAuthorizationProvider.DefaultRequestTimeout)
        {
        }

        /// <summary>
        /// Initializes a new instance of <see cref="InstanceMetadataClient"/>
        /// with specified request timeout.
        /// </summary>
        /// <param name="requestTimeout">Request timeout.</param>
        public InstanceMetadataClient(TimeSpan requestTimeout)
        {
            CheckPositiveTimeSpan(requestTimeout,
                "Request timeout in instance metadata client");
            httpClient = new AuthHttpClient(requestTimeout);
            ownsHttpClient = true;
        }

        internal InstanceMetadataClient(AuthHttpClient httpClient)
        {
            this.httpClient = httpClient;
        }

        // HttpClient cannot resend the same HttpRequestMessage twice, new
        // message instance has to be created each time.
        private Task<string> GetValueInternalAsync(string path,
            CancellationToken cancellationToken)
        {
            var request = new HttpRequestMessage(HttpMethod.Get,
                metadataUrl + path);
            request.Headers.Add(HttpConstants.Authorization,
                AuthorizationHeaderValue);
            return httpClient.ExecuteRequestAsync(request, cancellationToken);
        }

        internal async Task<string> GetValueAsync(string path,
            CancellationToken cancellationToken)
        {
            if (metadataUrl == null)
            {
                metadataUrl = MetadataServiceBaseUrl;
            }

            try
            {
                return await GetValueInternalAsync(path, cancellationToken);
            }
            catch (Exception ex)
            {
                throw new AuthorizationException(
                    $"Unable to get resource {path} from instance metadata " +
                    $"{metadataUrl}, error: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Gets the region of the running instance.
        /// </summary>
        /// <remarks>
        /// When using Instance Principal or OKE workload authentication, you
        /// can provide the return region as <see cref="NoSQLConfig.Region"/>
        /// property of <see cref="NoSQLConfig"/> to create
        /// <see cref="NoSQLClient"/> connected to the same region.
        /// </remarks>
        /// <param name="cancellationToken">(Optional) Cancellation token.
        /// </param>
        /// <returns>Task returning the region of the OCI instance.</returns>
        /// <exception cref="AuthorizationException">If failed to retrieve the
        /// region metadata.</exception>
        /// <exception cref="ArgumentException">If the retrieved region code
        /// does not match any region defined in the SDK.</exception>
        public async Task<Region> GetRegionAsync(
            CancellationToken cancellationToken = default) =>
            Region.FromRegionCodeOrId(await GetValueAsync(
                "instance/region", cancellationToken));

        /// <summary>
        /// Releases resources held by this
        /// <see cref="InstanceMetadataClient"/> instance.
        /// </summary>
        public void Dispose()
        {
            if (ownsHttpClient)
            {
                httpClient.Dispose();
            }
        }

    }
}
