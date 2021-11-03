/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Text.Json;
    using System.Threading;
    using System.Threading.Tasks;

    /// <summary>
    /// Authorization provider for the secure On-Premise Oracle NoSQL
    /// Database.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Applications should use this provider to authenticate against the
    /// secure on-premise kvstore.  The instance of this class should be set
    /// as <see cref="NoSQLConfig.AuthorizationProvider"/> in the initial
    /// configuration.
    /// </para>
    /// <para>
    /// The authorization for the database operations requires the kvstore
    /// login token.  The authorization provider is used to perform the
    /// following functions:
    /// <list type="number">
    /// <item>
    /// <description>
    /// The driver will log in to kvstore using the provided credentials and
    /// obtain the login token.  The login is only performed the first time
    /// the authorization is needed (for the first database operation) and may
    /// also be performed after the login token expires.  In the latter case
    /// the operation will throw <see cref="InvalidAuthorizationException"/>
    /// and will be automatically retried by the default retry handler, which
    /// will involve obtaining the new login token.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// If the <see cref="AutoRenew"/> property is set to <c>true</c> (which
    /// is the default), then after the initial login, the driver will renew
    /// the login token when it reaches half of its lifetime (half-point
    /// between acquisition and expiration).  Renew request requires only the
    /// existing login token and does not require user credentials.
    /// If the renew request fails, the token will eventually expire and the
    /// driver will perform another login as described above.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// The driver will log out of the kvstore using the existing login token
    /// when the <see cref="NoSQLClient"/> instance is disposed.
    /// </description>
    /// </item>
    /// </list>
    /// </para>
    /// <para>
    /// To obtain the login token the driver needs user name and password of
    /// existing kvstore user that has required permissions to perform needed
    /// database operations.  These credentials are only used for the login
    /// operation as described above and are not needed for every database
    /// operation.  The are 3 ways in which you may set these credentials, in
    /// order of increased security:
    /// <list type="number">
    /// <item>
    /// <description>
    /// You may provide credentials explicitly as <see cref="Credentials"/>
    /// property.  The user name and password are provided as
    /// <see cref="KVStoreCredentials"/> object.  This option is not very
    /// secure because the password is stored in main memory in plain text.
    /// You can erase the password after you finish using
    /// <see cref="NoSQLClient"/> instance and the password is no longer
    /// needed.<br/>
    /// Note that it is also possible to provide clear text credentials if you
    /// are storing <see cref="NoSQLConfig"/> in JSON file, in which case
    /// credentials may be stored as the sub-field within the value of
    /// <em>AuthorizationProvider</em> field.  This latter option is not
    /// secure and should only be used during development/testing.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// You may store the user name and password in separate JSON file and set
    /// the path to this file as <see cref="CredentialsFile"/> property.  This
    /// file should contain JSON representation of
    /// <see cref="KVStoreCredentials"/> object in the same format as
    /// <see cref="NoSQLConfig"/> is stored in JSON as discussed in the
    /// remarks section of <see cref="NoSQLConfig"/>.  This option is more
    /// secure because the credentials file may be restricted by the
    /// appropriate permissions.  The driver will load the credentials from
    /// this file on demand every time they are needed and it will erase the
    /// password from the main memory every time after the credentials are
    /// used.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// You can define your own credentials provider delegate and set it as
    /// <see cref="CredentialsProvider"/>.  This is the most secure option
    /// because you can choose where the credentials are stored and how
    /// they are accessed.  The credentials provider delegate will be called
    /// every time the credentials are needed and the password will be erased
    /// from the main memory every time after the credentials are used.
    /// </description>
    /// </item>
    /// </list>
    /// Instead of specifying one of the properties described above you may
    /// also use one of <see cref="KVStoreAuthorizationProvider"/>
    /// constructors that initialize one of these properties, as shown in the
    /// examples.
    /// </para>
    /// <para>
    /// Note that secure on-premise NoSQL Database uses the same endpoint for
    /// the authentication and to perform the database operations.  This
    /// endpoint must be using HTTPS protocol. See
    /// <see cref="NoSQLConfig.Endpoint"/>.
    /// </para>
    /// </remarks>
    /// <example>
    /// Creating <see cref="KVStoreAuthorizationProvider"/> with the explicitly
    /// provided credentials.
    /// <code>
    /// var config = new NoSQLConfig
    /// {
    ///     Endpoint = "https://myhost:8888",
    ///     AuthorizationProvider = new KVStoreAuthorizationProvider(
    ///         userName, password)
    ///     {
    ///         RequestTimeout = TimeSpan.FromSeconds(15)
    ///     }
    /// };
    /// </code>
    /// </example>
    /// <example>
    /// Credentials in a JSON file <em>~/my_app/credentials.json</em>.
    /// <code>
    /// {
    ///     "UserName": "John",
    ///     "Password": "..."
    /// }
    /// </code>
    /// </example>
    /// <example>
    /// Initial configuration in JSON file using the
    /// <see cref="KVStoreAuthorizationProvider"/> with the credentials file.
    /// <code>
    /// {
    ///     "Endpoint": "https://myhost:8888",
    ///     "AuthorizationProvider": {
    ///         "AuthorizationType": "KVStore",
    ///         "CredentialsFile": "~/my_app/credentials.json"
    ///     }
    /// }
    /// </code>
    /// </example>
    /// <example>
    /// Using the credentials provider delegate.
    /// <code>
    /// ...
    /// public static async Task&lt;KVStoreCredentials&gt; LoadCredentialsAsync(
    ///     CancellationToken cancellationToken)
    /// {
    ///     // Load user name and password from a secure location.
    ///     ...
    ///     return new KVStoreCredentials(userName, password);
    /// }
    /// ...
    /// var config = new NoSQLConfig
    /// {
    ///     Endpoint = "https://myhost:8888",
    ///     AuthorizationProvider = new KVStoreAuthorizationProvider(
    ///         LoadCredentialsAsync);
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="IAuthorizationProvider"/>
    /// <seealso cref="AuthorizationStringProvider"/>
    /// <seealso cref="KVStoreCredentials"/>
    public class KVStoreAuthorizationProvider : AuthorizationStringProvider,
        IDisposable
    {
        private KVStoreTokenProvider tokenProvider;
        private KVStoreTokenProvider.TokenResult tokenResult;
        private string authString;
        private CancellationTokenSource renewCancelSource;
        private readonly object providerLock = new object();
        private bool disposed;

        /// <summary>
        /// Gets or sets user credentials.
        /// </summary>
        /// <value>
        /// The credentials consisting of the user name and password.  This
        /// property is exclusive with <see cref="CredentialsFile"/> and
        /// <see cref="CredentialsProvider"/> properties.
        /// </value>
        public KVStoreCredentials Credentials { get; set; }

        /// <summary>
        /// Gets or sets the path to the JSON credentials file.
        /// </summary>
        /// <value>
        /// The path (absolute or relative) to a file used to store the user
        /// credentials in JSON format.  This property is exclusive with
        /// <see cref="Credentials"/> and <see cref="CredentialsProvider"/>
        /// properties.
        /// </value>
        public string CredentialsFile { get; set; }

        /// <summary>
        /// Gets or sets the credentials provider delegate.
        /// </summary>
        /// <value>
        /// <remarks>
        /// The same cancellation token passed to the method of
        /// <see cref="NoSQLClient"/> performing the operation will be passed
        /// to this delegate.
        /// </remarks>
        /// Asynchronous delegate used to obtain the user credentials.  It
        /// returns the credentials as a <see cref="Task"/> of
        /// <see cref="KVStoreCredentials"/>.  This property is exclusive with
        /// <see cref="Credentials"/> and <see cref="CredentialsFile"/>
        /// properties.
        /// </value>
        public Func<CancellationToken, Task<KVStoreCredentials>>
            CredentialsProvider { get; set; }

        /// <summary>
        /// Gets or sets the timeout for login, renew and logout requests.
        /// </summary>
        /// <value>
        /// The request timeout used for login, renew and logout operations.
        /// The default is 30 seconds.
        /// </value>
        public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Gets or sets the value that determines whether the login token
        /// should be automatically renewed.
        /// </summary>
        /// <value>
        /// <c>true</c> to automatically renew the login token when it reaches
        /// half of its life-time, otherwise <c>false</c>.  The default is
        /// <c>true</c>.
        /// </value>
        public bool AutoRenew { get; set; } = true;

        private static async Task<KVStoreCredentials>
            GetCredentialsFromFileAsync(
            string credentialsFile,
            CancellationToken cancellationToken)
        {
            try
            {
                using (var fileStream = File.OpenRead(credentialsFile))
                {
                    return await JsonSerializer
                        .DeserializeAsync<KVStoreCredentials>(
                        fileStream, NoSQLConfig.JsonSerializerOptions,
                        cancellationToken);
                }
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "Authorization: failed to read credentials from file " +
                    $"{credentialsFile}: {ex.Message}", ex);
            }
        }

        private static void CheckCredentials(KVStoreCredentials credentials)
        {
            if (credentials == null)
            {
                throw new ArgumentException(
                    "Authentication: got null credentials from file or "+
                    "credentials provider");
            }
            if (string.IsNullOrEmpty(credentials.UserName))
            {
                throw new ArgumentException(
                    "Authentication: user name cannot be null or empty");
            }
            if (credentials.Password == null)
            {
                throw new ArgumentException(
                    "Authentication: password cannot be null");
            }
        }

        private void SetAuthResult(
            KVStoreTokenProvider.TokenResult result)
        {
            lock (providerLock)
            {
                tokenResult = result;
                authString = "Bearer " + result.Token;
            }
        }

        private void ScheduleRenew()
        {
            renewCancelSource?.Cancel();
            renewCancelSource = null;

            /*
             * If it is 10 seconds before expiration, don't do further renew
             * to avoid too many renew requests in the last few seconds.
             */
            var expireMillis = (tokenResult.ExpireAt - DateTime.UtcNow)
                .TotalMilliseconds;
            if (expireMillis <= 10000)
            {
                return;
            }

            renewCancelSource = new CancellationTokenSource();
            Task.Run(async () =>
            {
                await Task.Delay((int)(expireMillis / 2),
                    renewCancelSource.Token);
                try
                {
                    SetAuthResult(await tokenProvider.RenewAsync(
                        tokenResult.Token, renewCancelSource.Token));
                }
                catch (Exception)
                {
                    //TODO: log the error
                    return;
                }
                ScheduleRenew();
            });
        }

        private void Logout()
        {
            if (tokenResult == null)
            {
                return;
            }

            Task.Run(() => tokenProvider.LogoutAsync(tokenResult.Token))
                .Wait();
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="KVStoreAuthorizationProvider"/>.
        /// </summary>
        /// <remarks>
        /// You must set one of <see cref="Credentials"/>,
        /// <see cref="CredentialsFile"/> or <see cref="CredentialsProvider"/>
        /// properties.
        /// </remarks>
        public KVStoreAuthorizationProvider()
        {
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="KVStoreAuthorizationProvider"/> with the specified user
        /// credentials.
        /// </summary>
        /// <param name="userName">User name of the kvstore user.</param>
        /// <param name="password">Password of the kvstore user.</param>
        public KVStoreAuthorizationProvider(string userName, char[] password)
        {
            Credentials = new KVStoreCredentials(userName, password);
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="KVStoreAuthorizationProvider"/> with the specified user
        /// credentials.
        /// </summary>
        /// <param name="credentials">The credentials of the kvstore user.
        /// </param>
        public KVStoreAuthorizationProvider(KVStoreCredentials credentials)
        {
            Credentials = credentials;
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="KVStoreAuthorizationProvider"/> with the specified path
        /// to the JSON credentials file.
        /// </summary>
        /// <param name="credentialsFile">The path (absolute or relative) to
        /// the JSON credentials file.</param>
        public KVStoreAuthorizationProvider(string credentialsFile)
        {
            CredentialsFile = credentialsFile;
        }

        /// <summary>
        /// Initializes a new instance of
        /// <see cref="KVStoreAuthorizationProvider"/> with the specified
        /// credentials provider delegate.
        /// </summary>
        /// <param name="credentialsProvider">Credentials provider delegate.
        /// </param>
        public KVStoreAuthorizationProvider(
            Func<CancellationToken, Task<KVStoreCredentials>>
                credentialsProvider)
        {
            CredentialsProvider = credentialsProvider;
        }

        /// <summary>
        /// Validates and configures the authorization provider.
        /// </summary>
        /// <remarks>
        /// This method will be called when <see cref="NoSQLClient"/> instance
        /// is created.  You do not need to call this method.
        /// </remarks>
        /// <param name="config">The initial configuration.</param>
        /// <exception cref="ArgumentException">If neither of the properties
        /// <see cref="Credentials"/>, <see cref="CredentialsFile"/> and
        /// <see cref="CredentialsProvider"/> is set or more then one of them
        /// is set, or if <see cref="Credentials"/> are provided and either
        /// user name or password is <c>null</c> or empty.</exception>
        public override void ConfigureAuthorization(NoSQLConfig config)
        {
            ValidateUtils.CheckTimeout(RequestTimeout);

            // Should be called only once
            if (Credentials != null)
            {
                if (CredentialsProvider != null || CredentialsFile != null)
                {
                    throw new ArgumentException(
                        "Cannot specify CredentialsFile or " +
                        "CredentialsProvider properties together with " +
                        "Credentials property");
                }
                CheckCredentials(Credentials);
            }
            else if (CredentialsProvider != null)
            {
                if (CredentialsFile != null)
                {
                    throw new ArgumentException(
                        "Cannot specify CredentialsFile property together  " +
                        "with CredentialsProvider property");
                }
            }
            else if (CredentialsFile == null)
            {
                throw new ArgumentException(
                    "Must specify one of Credentials, CredentialsProvider " +
                    "or CredentialsFile properties");
            }

            Debug.Assert(tokenProvider == null);
            tokenProvider = new KVStoreTokenProvider(this, config.Uri,
                config.ConnectionOptions);
        }

        /// <summary>
        /// Gets the authorization string.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This method is called by the driver to obtain the authorization
        /// information.  You do not need to call this method.
        /// </para>
        /// <para>
        /// This method will perform the login operation for the first time
        /// and also if the previous operation failed with
        /// <see cref="InvalidAuthorizationException"/>.  After login, a renew
        /// operation will be optionally scheduled, which will update the
        /// login token in the background.  In all other cases, this method
        /// will return the existing authorization string.
        /// </para>
        /// </remarks>
        /// <param name="request">The <see cref="Request"/> object
        /// representing the running operation.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        /// <returns><see cref="Task"/> that returning the authorization
        /// string.</returns>
        /// <exception cref="AuthorizationException">If the login failed.
        /// </exception>
        public override async Task<string> GetAuthorizationStringAsync(
            Request request, CancellationToken cancellationToken)
        {
            lock (providerLock)
            {
                if (authString != null && !(request.LastException is
                        InvalidAuthorizationException))
                {
                    return authString;
                }
            }

            KVStoreCredentials loginCredentials;
            if (Credentials != null)
            {
                loginCredentials = Credentials;
            }
            else if (CredentialsProvider != null)
            {
                loginCredentials =
                    await CredentialsProvider(cancellationToken);
                CheckCredentials(loginCredentials);
            }
            else
            {
                Debug.Assert(CredentialsFile != null);
                loginCredentials = await GetCredentialsFromFileAsync(
                    CredentialsFile, cancellationToken);
                CheckCredentials(loginCredentials);
            }

            try
            {
                SetAuthResult(await tokenProvider.LoginAsync(loginCredentials,
                    cancellationToken));

                lock (providerLock)
                {
                    if (AutoRenew)
                    {
                        ScheduleRenew();
                    }
                    return authString;
                }
            }
            finally
            {
                if (Credentials == null)
                {
                    Array.Clear(loginCredentials.Password, 0,
                        loginCredentials.Password.Length);
                }
            }
        }

        /// <inheritdoc cref="NoSQLClient.Dispose(bool)"/>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !disposed)
            {
                Logout();
                renewCancelSource?.Cancel();
                tokenProvider?.Dispose();
                disposed = true;
            }
        }

        /// <summary>
        /// Releases resources used by this
        /// <see cref="KVStoreAuthorizationProvider"/> instance.
        /// </summary>
        /// <remarks>
        /// Applications should not call this method.  The driver will call
        /// this method when <see cref="NoSQLClient"/> instance is disposed.
        /// If the provider has the login token, this method will also log out
        /// of the kvstore.
        /// </remarks>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }
    }

}
