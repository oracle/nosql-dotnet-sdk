/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using SDK;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static NegativeTestData;
    using static Utils;

    [TestClass]
    public partial class ConfigTests : ConfigTestBase
    {
        // Data for negative tests

        private static readonly IEnumerable<NoSQLRetryHandler>
            BadRetryHandlers = Enumerable.Empty<NoSQLRetryHandler>()
                .Append(new NoSQLRetryHandler
                 {
                     // invalid MaxRetryAttempts
                     MaxRetryAttempts = -10
                 })
                .Concat(
                    from val in BadTimeSpans
                    select new NoSQLRetryHandler
                    {
                        BaseDelay = val
                    })
                .Concat(
                    from val in BadTimeSpans
                    select new NoSQLRetryHandler
                    {
                        ControlOperationBaseDelay = val
                    });

        private static readonly KVStoreCredentials[] BadKVStoreCredentials =
        {
            new KVStoreCredentials(),
            new KVStoreCredentials(StoreCredentials.UserName, null),
            new KVStoreCredentials(null, StoreCredentials.Password),
            new KVStoreCredentials(string.Empty, StoreCredentials.Password)
        };

        private static readonly IEnumerable<KVStoreAuthorizationProvider>
            BadKVStoreAuthProviders =
                (from val in BadKVStoreCredentials
                 select new KVStoreAuthorizationProvider
                 {
                     Credentials = val
                 })
                // no credentials setting
                .Append(new KVStoreAuthorizationProvider())
                // cannot set both credentials and credentials file
                .Append(new KVStoreAuthorizationProvider
                {
                    Credentials = StoreCredentials,
                    CredentialsFile = StoreCredentialsFile
                })
                // cannot set both credentials anc credentials provider
                .Append(new KVStoreAuthorizationProvider
                {
                    Credentials = StoreCredentials,
                    CredentialsProvider = LoadStoreCredentials
                })
                // cannot set both credentials file and credentials provider
                .Append(new KVStoreAuthorizationProvider
                {
                    CredentialsFile = StoreCredentialsFile,
                    CredentialsProvider = LoadStoreCredentials
                })
                .Concat(
                    from val in BadTimeSpans
                    select new KVStoreAuthorizationProvider
                    {
                        Credentials = StoreCredentials,
                        RequestTimeout = val
                    });

        private static readonly IAMCredentials[] BadIAMCredentials =
        {
            CombineProperties(CloudCredentials,
                new {TenantId = (string)null}),
            CombineProperties(CloudCredentials,
                // not a valid OCID
                new {TenantId = "blah_blah"}),
            CombineProperties(CloudCredentials,
                new {UserId = (string)null}),
            CombineProperties(CloudCredentials,
                // not a valid OCID
                new {UserId = "blah_blah"}),
            CombineProperties(CloudCredentials,
                new {Fingerprint = (string)null}),
            // Cannot have both private key and private key pem
            CombineProperties(CloudCredentials, new
            {
                PrivateKey = CloudPrivateKey,
                CloudCredentials.PrivateKeyPEM
            }),
            // Cannot have both private key and private key file
            CombineProperties(CloudCredentials, new
            {
                PrivateKey = CloudPrivateKey,
                PrivateKeyFile = "private.pem"
            }),
            // Cannot have both private key pem and private key file
            CombineProperties(CloudCredentials, new
            {
                CloudCredentials.PrivateKeyPEM,
                PrivateKeyFile = "private.pem"
            })
        };

        private static readonly IEnumerable<IAMAuthorizationProvider>
            BadIAMAuthProviders =
                (from val in BadIAMCredentials
                 select new IAMAuthorizationProvider(val))
                // cannot have both credentials and config file
                .Append(new IAMAuthorizationProvider
                {
                    Credentials = CloudCredentials,
                    ConfigFile = OCIConfigFile
                })
                // cannot have both credentials and credentials provider
                .Append(new IAMAuthorizationProvider
                {
                    Credentials = CloudCredentials,
                    CredentialsProvider = LoadCloudCredentials
                })
                // cannot have both config file and credentials provider
                .Append(new IAMAuthorizationProvider
                {
                    ConfigFile = OCIConfigFile,
                    CredentialsProvider = LoadCloudCredentials
                })
                // cannot have both credentials and profile name
                .Append(new IAMAuthorizationProvider
                {
                    Credentials = CloudCredentials,
                    ProfileName = OCIConfigProfile
                })
                // cannot have both profile name and credentials provider
                .Append(new IAMAuthorizationProvider
                {
                    ProfileName = OCIConfigProfile,
                    CredentialsProvider = LoadCloudCredentials
                });

        private static readonly string[] BadEndpoints =
        {
            "", // cannot be empty string
            "a:x", // invalid port
            "a:-1", // invalid port
            "a:/abcde", // no path allowed
            "https://a:/abcde", // no path allowed
            "https://a:443/abcde",
            "locahost/", // no / at the end allowed
            "https://a:-1", // invalid port
            "http://a:12.123", // invalid port
            "ftp://foo", // protocol must be http or https,
        };

        private static readonly double[] BadRateLimiterPercentages =
            { -1, 0, 101, 100.01 };

        private static readonly IEnumerable<NoSQLConfig> BadConfigs =
            Enumerable.Empty<NoSQLConfig>()
                // invalid service type
                .Append(new NoSQLConfig
                {
                    ServiceType = (ServiceType)(-1),
                    Endpoint = CloudSimEndpoint
                })
                // cannot have both endpoint and region
                .Append(new NoSQLConfig
                {
                    Endpoint = CloudSimEndpoint,
                    Region = Region.US_PHOENIX_1
                })
                .Concat(from endpoint in BadEndpoints
                    select new NoSQLConfig
                    {
                        Endpoint = endpoint
                    })
                .Concat(from timeout in BadTimeSpans
                    select new NoSQLConfig
                    {
                        Endpoint = CloudSimEndpoint,
                        Timeout = timeout
                    })
                .Concat(from timeout in BadTimeSpans
                    select new NoSQLConfig
                    {
                        Endpoint = CloudSimEndpoint,
                        TableDDLTimeout = timeout
                    })
                .Concat(from timeout in BadTimeSpans
                    select new NoSQLConfig
                    {
                        Endpoint = CloudSimEndpoint,
                        AdminTimeout = timeout
                    })
                .Concat(from timeout in BadTimeSpans
                    select new NoSQLConfig
                    {
                        Endpoint = CloudSimEndpoint,
                        TablePollTimeout = timeout
                    })
                .Concat(from timeout in BadTimeSpans
                    select new NoSQLConfig
                    {
                        Endpoint = CloudSimEndpoint,
                        AdminPollTimeout = timeout
                    })
                .Concat(from delay in BadTimeSpans
                    select new NoSQLConfig
                    {
                        Endpoint = CloudSimEndpoint,
                        TablePollDelay = delay
                    })
                .Concat(from delay in BadTimeSpans
                    select new NoSQLConfig
                    {
                        Endpoint = CloudSimEndpoint,
                        AdminPollDelay = delay
                    })
                .Concat(from val in BadPositiveInt32
                    select new NoSQLConfig
                    {
                        Endpoint = CloudSimEndpoint,
                        MaxMemoryMB = val
                    })
                .Concat(from val in BadRateLimiterPercentages
                    select new NoSQLConfig
                    {
                        Endpoint = CloudSimEndpoint,
                        RateLimiterPercent = val
                    })
                // TablePollDelay must be <= TablePollTimeout
                .Append(new NoSQLConfig
                {
                    Endpoint = CloudSimEndpoint,
                    TablePollTimeout = TimeSpan.FromMilliseconds(10000),
                    TablePollDelay = TimeSpan.FromMilliseconds(10001)
                })
                // AdminPollDelay must be <= TablePollTimeout
                .Append(new NoSQLConfig
                {
                    Endpoint = CloudSimEndpoint,
                    AdminPollTimeout = TimeSpan.FromMilliseconds(10000),
                    AdminPollDelay = TimeSpan.FromMilliseconds(10001)
                })
                // invalid consistency
                .Append(new NoSQLConfig
                {
                    Endpoint = CloudSimEndpoint,
                    Consistency = (Consistency)(-1)
                })
                .Concat(from retryHandler in BadRetryHandlers
                    select new NoSQLConfig
                    {
                        Endpoint = CloudSimEndpoint,
                        RetryHandler = retryHandler
                    })
                .Concat(from authProvider in BadKVStoreAuthProviders
                    select new NoSQLConfig
                    {
                        Endpoint = SecureStoreEndpoint,
                        AuthorizationProvider = authProvider
                    })
                .Concat(from authProvider in BadIAMAuthProviders
                    select new NoSQLConfig
                    {
                        Region = Region.US_PHOENIX_1,
                        AuthorizationProvider = authProvider
                    })
                // non-secure endpoint with secure kvstore
                .Append(new NoSQLConfig
                {
                    Endpoint = CloudSimEndpoint,
                    AuthorizationProvider = StoreAuthProvider
                })
                // cannot use KVStoreAuthorizationProvider with cloud
                .Append(new NoSQLConfig
                {
                    Endpoint = SecureStoreEndpoint,
                    ServiceType = ServiceType.Cloud,
                    AuthorizationProvider = StoreAuthProvider
                })
                // same as above, region implies cloud
                .Append(new NoSQLConfig
                {
                    Region = Region.AP_MUMBAI_1,
                    AuthorizationProvider = StoreAuthProvider
                })
                // cannot use IAMAuthorizationProvider with KVStore
                .Append(new NoSQLConfig
                {
                    Endpoint = SecureStoreEndpoint,
                    ServiceType = ServiceType.KVStore,
                    AuthorizationProvider = CloudAuthProvider
                })
                // cannot use IAMAuthorizationProvider with CloudSim
                .Append(new NoSQLConfig
                {
                    Endpoint = Region.US_PHOENIX_1.Endpoint,
                    ServiceType = ServiceType.CloudSim,
                    AuthorizationProvider = CloudAuthProvider
                })
                // cannot use KVStoreAuthorizationProvider with CloudSim
                .Append(new NoSQLConfig
                {
                    Endpoint = SecureStoreEndpoint,
                    ServiceType = ServiceType.CloudSim,
                    AuthorizationProvider = StoreAuthProvider
                });

        private static IEnumerable<object[]> NegativeDataSource =>
            from config in BadConfigs select new object[] { config };

        [ClassInitialize]
        public new static void ClassInitialize(TestContext testContext)
        {
            ConfigTestBase.ClassInitialize(testContext);
        }

        [ClassCleanup]
        public new static void ClassCleanup()
        {
            ConfigTestBase.ClassCleanup();
        }

        [DataTestMethod]
        [DynamicData(nameof(NegativeDataSource))]
        public void TestNegative(NoSQLConfig config)
        {
            AssertThrowsDerived<ArgumentException>(() =>
            {
                var noSQLClient = new NoSQLClient(config);
            });
        }

    }
}
