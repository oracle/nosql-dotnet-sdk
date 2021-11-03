/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {
    using System;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Represents a provider used to authorize requests to the Oracle NoSQL
    /// Database service.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The authorization provider is set as
    /// <see cref="NoSQLConfig.AuthorizationProvider"/> in the initial
    /// configuration used to create <see cref="NoSQLClient"/> instance.  The
    /// authorization is required when using Oracle NoSQL Cloud Service as
    /// well as on-premise Oracle NoSQL Database in secure mode.  Most
    /// applications do not need to implement this interface and can use
    /// one of the supplied authorization providers,
    /// <see cref="IAMAuthorizationProvider"/> for the Cloud Service or
    /// <see cref="KVStoreAuthorizationProvider"/> for on-premise NoSQL
    /// Database in secure mode.  Alternatively, you may also create your own
    /// authorization provider by creating a class that implements this
    /// interface and setting its instance as
    /// <see cref="NoSQLConfig.AuthorizationProvider"/>.
    /// </para>
    /// <para>
    /// The authorization is supplied as part of HTTP headers in the request
    /// which includes the header names and values.  In general, multiple
    /// headers may be required.  For a common case where authorization is
    /// supplied via a single <em>Authorization</em> HTTP header, you may
    /// choose to extend <see cref="AuthorizationStringProvider"/> class.
    /// </para>
    /// <para>
    /// If you are implementing a custom authorization provider that uses disposable
    /// resources, you can also implement the <see cref="IDisposable"/>
    /// interface.  The driver will call <see cref="IDisposable.Dispose()"/>
    /// of the provider when <see cref="NoSQLClient"/> instance is disposed.
    /// </para>
    /// </remarks>
    /// <seealso cref="NoSQLConfig.AuthorizationProvider"/>
    /// <seealso cref="AuthorizationStringProvider"/>
    /// <seealso cref="IAMAuthorizationProvider"/>
    /// <seealso cref="KVStoreAuthorizationProvider"/>
    public interface IAuthorizationProvider
    {
        /// <summary>
        /// Configures the provider.
        /// </summary>
        /// <remarks>
        /// This method allows you to configure the provider by optionally
        /// using the information provided in the initial configuration used
        /// to create <see cref="NoSQLClient"/> instance such as the service
        /// endpoint, region, etc.  It is called by the driver only once when
        /// the <see cref="NoSQLClient"/> instance is created.
        /// </remarks>
        /// <param name="config">The initial configuration.</param>
        /// <exception cref="ArgumentException">If the initialization fails
        /// because any of the required information is missing or invalid.
        /// </exception>
        void ConfigureAuthorization(NoSQLConfig config);

        /// <summary>
        /// Obtains and supplies the authorization information as the required
        /// HTTP headers.
        /// </summary>
        /// <remarks>
        /// Add the required headers to the provided
        /// <see cref="System.Net.Http.Headers.HttpRequestHeaders"/>
        /// collection.  Obtaining the required authorization information
        /// may be an asynchronous operation, thus this method is
        /// asynchronous.
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
        /// <exception cref="AuthorizationException">If failed to obtain
        /// the required authorization headers.  Use this exception to
        /// wrap any provider-specific exception.</exception>
        Task ApplyAuthorizationAsync(Request request,
            HttpRequestHeaders headers, CancellationToken cancellationToken);
    }

    /// <summary>
    /// Implementation of <see cref="IAuthorizationProvider"/> that provides
    /// the authorization string.
    /// </summary>
    /// <remarks>
    /// Use this class only if you need to implement your own authorization
    /// provider and the authorization needs only <em>Authorization</em> HTTP
    /// header.  In this case, extend this class by implementing methods
    /// <see cref="GetAuthorizationStringAsync"/> and optionally
    /// <see cref="ConfigureAuthorization"/>.
    /// </remarks>
    /// <seealso cref="IAuthorizationProvider"/>
    public abstract class AuthorizationStringProvider : IAuthorizationProvider
    {
        /// <summary>
        /// Default implementation of
        /// <see cref="IAuthorizationProvider.ConfigureAuthorization"/> which
        /// is a no-op.
        /// </summary>
        /// <param name="config">The initial configuration.</param>
        public virtual void ConfigureAuthorization(NoSQLConfig config)
        {
        }

        /// <summary>
        /// Gets the authorization string.
        /// </summary>
        /// <remarks>
        /// Subclasses must override this method.  The returned authorization
        /// string will be set as a value of <em>Authorization</em> HTTP
        /// header.
        /// </remarks>
        /// <param name="request">The <see cref="Request"/> object
        /// representing the running operation.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><see cref="Task"/> that returning the authorization
        /// string.</returns>
        /// <exception cref="AuthorizationException">If failed to obtain
        /// the authorization string. Use this exception to wrap any
        /// provider-specific exception.</exception>
        public abstract Task<string> GetAuthorizationStringAsync(
            Request request, CancellationToken cancellationToken);

        /// <summary>
        /// Obtains and supplies the authorization information as the required
        /// HTTP headers.
        /// </summary>
        /// <remarks>
        /// This method will call <see cref="GetAuthorizationStringAsync"/>
        /// and use the returned value to supply the <em>Authorization</em>
        /// HTTP header.  You do not need to call or override this method.
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
        /// <exception cref="AuthorizationException">If failed to obtain
        /// the required authorization headers.  Use this exception to
        /// wrap any provider-specific exception.</exception>
        public async Task ApplyAuthorizationAsync(Request request,
            HttpRequestHeaders headers, CancellationToken cancellationToken)
        {
            var authString = await GetAuthorizationStringAsync(request,
                cancellationToken);
            headers.Add(HttpConstants.Authorization, authString);
        }

    }

    internal class CloudSimAuthorizationProvider : AuthorizationStringProvider
    {
        public override Task<string> GetAuthorizationStringAsync(Request request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult("Bearer TestTenant");
        }
    }

}
