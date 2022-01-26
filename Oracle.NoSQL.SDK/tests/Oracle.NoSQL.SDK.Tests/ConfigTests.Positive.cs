/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
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
    using System.Reflection;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Utils;

    public partial class ConfigTests
    {
        private static string VerifyRegion(Region region)
        {
            Assert.IsFalse(string.IsNullOrEmpty(region.RegionId));
            Assert.IsFalse(string.IsNullOrEmpty(region.RegionCode));
            Assert.IsFalse(string.IsNullOrEmpty(region.SecondLevelDomain));

            // Check that region is the value of corresponding public static
            // field in the Region class.
            var fieldName = region.RegionId.Replace('-', '_').ToUpper();
            var propInfo = typeof(Region).GetField(fieldName,
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(propInfo);
            //Assert.AreEqual(propInfo.GetValue(null), region);

            var expectedEndpoint =
                $"https://nosql.{region.RegionId}.oci.{region.SecondLevelDomain}";
            Assert.AreEqual(expectedEndpoint, region.Endpoint);
            return region.Endpoint;
        }

        private static void VerifyEndpoint(string endpoint, Uri uri)
        {
            string scheme = null;
            string host;
            var port = -1;

            var idx = endpoint.IndexOf("://", StringComparison.Ordinal);
            if (idx != -1)
            {
                scheme = endpoint.Substring(0, idx).ToLower();
                host = endpoint.Substring(idx + 3);
            }
            else
            {
                host = endpoint;
            }

            idx = host.IndexOf(':');
            if (idx != -1)
            {
                Assert.IsTrue(int.TryParse(host.Substring(idx + 1),
                    out port));
                host = host.Substring(0, idx);
            }

            // Slightly different logic than in EndpointToUri() to cross-check
            if (port == -1)
            {
                scheme ??= "https";
                port = scheme == "https" ? 443 : 8080;
            }
            else if (scheme == null)
            {
                scheme = port == 443 ? "https" : "http";
            }

            Assert.AreEqual(scheme, uri.Scheme);
            Assert.AreEqual(host, uri.Host);
            Assert.AreEqual(port, uri.Port);
        }

        // clientConfig is resulting NoSQLConfig within NoSQLClient instance
        private static void VerifyConfig(NoSQLConfig clientConfig,
            NoSQLConfig config)
        {
            Assert.IsNotNull(clientConfig.Uri);
            if (config.Region != null)
            {
                Assert.AreEqual(config.Region, clientConfig.Region);
                VerifyEndpoint(VerifyRegion(config.Region), clientConfig.Uri);
            }
            else if (config.Endpoint != null)
            {
                Assert.AreEqual(config.Endpoint, clientConfig.Endpoint);
                VerifyEndpoint(config.Endpoint, clientConfig.Uri);
            }
            else // a case of region in oci config file
            {
                Assert.IsNotNull(clientConfig.Region);
                var endpointFromRegion = VerifyRegion(clientConfig.Region);
                Assert.AreEqual(endpointFromRegion, clientConfig.Endpoint);
                VerifyEndpoint(clientConfig.Endpoint, clientConfig.Uri);
            }

            Assert.AreEqual(config.Timeout, clientConfig.Timeout);
            Assert.AreEqual(config.TableDDLTimeout,
                clientConfig.TableDDLTimeout);
            Assert.AreEqual(config.AdminTimeout,
                clientConfig.AdminTimeout);
            Assert.AreEqual(config.TablePollTimeout,
                clientConfig.TablePollTimeout);
            Assert.AreEqual(config.AdminPollTimeout,
                clientConfig.AdminPollTimeout);
            Assert.AreEqual(config.TablePollDelay,
                clientConfig.TablePollDelay);
            Assert.AreEqual(config.AdminPollDelay,
                clientConfig.AdminPollDelay);
            Assert.AreEqual(config.Consistency, clientConfig.Consistency);
            Assert.AreEqual(config.MaxMemoryMB, clientConfig.MaxMemoryMB);
            Assert.AreEqual(config.Compartment, clientConfig.Compartment);

            AssertDeepEqual(config.RetryHandler ?? new NoSQLRetryHandler(),
                clientConfig.RetryHandler);

            if (config.AuthorizationProvider != null)
            {
                AssertDeepEqual(config.AuthorizationProvider,
                    clientConfig.AuthorizationProvider);

                if (config.AuthorizationProvider is
                    KVStoreAuthorizationProvider)
                {
                    Assert.AreEqual(ServiceType.KVStore,
                        clientConfig.ServiceType);
                }
                else if (config.AuthorizationProvider is
                    IAMAuthorizationProvider)
                {
                    Assert.AreEqual(ServiceType.Cloud,
                        clientConfig.ServiceType);
                }
                else
                {
                    Assert.AreEqual(ServiceType.Unspecified,
                        clientConfig.ServiceType);
                }
            }
            else
            {
                if (config.ServiceType == ServiceType.Unspecified)
                {
                    Assert.AreEqual(config.Region != null ?
                        ServiceType.Cloud : ServiceType.CloudSim,
                        clientConfig.ServiceType);
                }
                else
                {
                    Assert.AreEqual(config.ServiceType,
                        clientConfig.ServiceType);

                    if (config.ServiceType == ServiceType.Cloud)
                    {
                        // must be default IAM provider with default
                        // OCI config file
                        AssertDeepEqual(new IAMAuthorizationProvider(),
                            clientConfig.AuthorizationProvider);
                    }
                    else if (config.ServiceType == ServiceType.KVStore)
                    {
                        // must be non-secure kvstore
                        Assert.IsNull(clientConfig.AuthorizationProvider);
                    }
                }
            }
        }

        private void ClearConfigObj(object obj)
        {
            if (obj == null)
            {
                return;
            }

            var driverAssembly = Assembly.GetAssembly(typeof(NoSQLClient));
            foreach (var propInfo in obj.GetType().GetProperties(
                BindingFlags.Public | BindingFlags.Instance))
            {
                //TODO:
                //Determine how deeply the config object should be cloned
                //when NoSQLClient instance is created.

                //if (propInfo.PropertyType.Assembly == driverAssembly)
                {
                    //    ClearConfigObj(propInfo.GetValue(obj));
                    //}
                    //else
                    if (propInfo.CanWrite)
                    {
                        propInfo.SetValue(obj, null);
                    }
                }
            }
        }

        // Data for positive tests.

        private static readonly string[] GoodEndpoints =
        {
            "localhost:8080",
            "http://localhost",
            "https://hostname",
            "hostname:80",
            "hostname",
            "hostname:443",
            "http://localhost:8080",
            "http://localhost:80",
            "https://hostname:443",
            "https://hostname:8181",
            "HTTP://localhost",
            "hTTps://hostname:8181",
            "HtTpS://hostname"
        };

        private static readonly KVStoreAuthorizationProvider[]
            GoodKVStoreAuthProviders =
            {
                new KVStoreAuthorizationProvider(StoreCredentials),
                new KVStoreAuthorizationProvider(StoreCredentials.UserName,
                    StoreCredentials.Password),
                new KVStoreAuthorizationProvider(StoreCredentialsFile),
                new KVStoreAuthorizationProvider(LoadStoreCredentials),
            };

        private static readonly IAMCredentials[]
            GoodIAMCredentialsNotEncrypted =
        {
            CloudCredentials,
            CombineProperties(CloudCredentialsBase, new
            {
                PrivateKey = CloudPrivateKey
            }),
            CombineProperties(CloudCredentialsBase, new
            {
                PrivateKeyFile = "private.pem"
            })
        };

        private static readonly IEnumerable<IAMCredentials>
            GoodIAMCredentials = GoodIAMCredentialsNotEncrypted.Concat(
                from credentials in GoodIAMCredentialsNotEncrypted
                select CombineProperties(credentials, new
                {
                    Passphrase = "oracle".ToCharArray()
                }));

        private static readonly IEnumerable<IAMAuthorizationProvider>
            GoodIAMAuthProviders =
                (from credentials in GoodIAMCredentials
                    select new IAMAuthorizationProvider(credentials))
                .Append(new IAMAuthorizationProvider(OCIConfigFile,
                    OCIConfigProfile))
                .Append(new IAMAuthorizationProvider(LoadCloudCredentials))
                .Append(IAMAuthorizationProvider
                    .CreateWithInstancePrincipal());

        private static readonly IEnumerable<NoSQLConfig> GoodConfigs =
            (from endpoint in GoodEndpoints
                select new NoSQLConfig
                {
                    Endpoint = endpoint
                })
            .Concat(from region in Region.Values
                select new NoSQLConfig
                {
                    Region = region,
                    AuthorizationProvider = CloudAuthProvider
                })
            .Append(new NoSQLConfig
            {
                Endpoint = CloudSimEndpoint,
                Timeout = TimeSpan.FromSeconds(20),
                RetryHandler = new NoSQLRetryHandler
                {
                    BaseDelay = TimeSpan.FromSeconds(20)
                }
            })
            .Append(new NoSQLConfig
            {
                Endpoint = CloudSimEndpoint,
                TableDDLTimeout = TimeSpan.FromSeconds(5),
                TablePollDelay = TimeSpan.FromMilliseconds(500),
                Consistency = Consistency.Absolute
            })
            .Append(new NoSQLConfig
            {
                Endpoint = "localhost:8080",
                TablePollTimeout = TimeSpan.FromSeconds(30),
                RetryHandler = new NoSQLRetryHandler
                {
                    ControlOperationBaseDelay = TimeSpan.FromSeconds(120)
                }
            })
            .Append(new NoSQLConfig
            {
                Endpoint = "localhost:2000",
                RetryHandler = NoSQLConfig.NoRetries
            })
            .Concat(
                from provider in GoodIAMAuthProviders
                select new NoSQLConfig
                {
                    Region = Region.AP_SEOUL_1,
                    // Make a copy so that EraseConfig() does not affect
                    // the original.
                    AuthorizationProvider = DeepCopy(provider)
                })
            .Append(new NoSQLConfig
            {
                // Region in OCI config file
                AuthorizationProvider = CloudAuthProviderWithOCIConfigRegion
            })
            .Append(new NoSQLConfig
            {
                // specify both service type and region
                ServiceType = ServiceType.Cloud,
                Region = Region.AP_CHUNCHEON_1,
                AuthorizationProvider = CloudAuthProvider
            })
            .Append(new NoSQLConfig
            {
                // specify both service type and endpoint
                ServiceType = ServiceType.Cloud,
                Endpoint = "https://nosql.us-phoenix-1.oci.oraclecloud.com",
                AuthorizationProvider = CloudAuthProvider
            })
            .Append(new NoSQLConfig
            {
                // specify different HTTPS service port
                Endpoint =
                    "https://nosql.us-phoenix-1.oci.oraclecloud.com:8181",
                AuthorizationProvider = CloudAuthProvider
            })
            .Append(new NoSQLConfig
            {
                // non-secure kvstore
                ServiceType = ServiceType.KVStore,
                Endpoint = "localhost:8080"
            })
            .Append(new NoSQLConfig
            {
                // secure kvstore
                // scheme should default to https for port 443
                Endpoint = "localhost:443",
                AuthorizationProvider = StoreAuthProvider
            })
            .Append(new NoSQLConfig
            {
                // secure kvstore with service type
                ServiceType = ServiceType.KVStore,
                Endpoint = "https://localhost:8181",
                AuthorizationProvider = StoreAuthProvider
            })
            .Concat(
                from provider in GoodKVStoreAuthProviders
                select new NoSQLConfig
                {
                    Endpoint = "https://localhost:8181",
                    AuthorizationProvider = DeepCopy(provider)
                });

        private static IEnumerable<object[]> PositiveDataSource =>
            from config in GoodConfigs select new object[] { config };

        [DataTestMethod]
        [DynamicData(nameof(PositiveDataSource))]
        public void TestPositive(NoSQLConfig config)
        {
            var configCopy = DeepCopy(config);
            NoSQLClient client;
            try
            {
                client = new NoSQLClient(configCopy);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }

            // Verify that creation of NoSQLClient instance did not affect
            // any public properties of provided NoSQLConfig object.
            AssertDeepEqual(config, configCopy);

            VerifyConfig(client.Config, configCopy);

            // verify that changes to config public properties did not affect
            // NoSQLClient instance
            var clientConfigCopy = DeepCopy(client.Config);
            ClearConfigObj(configCopy);

            AssertDeepEqual(clientConfigCopy, client.Config, true);
        }

    }
}
