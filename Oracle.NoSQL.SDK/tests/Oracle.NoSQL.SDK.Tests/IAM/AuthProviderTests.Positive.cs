/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests.IAM
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Net.Http;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static TestData;
    using static Utils;

    [TestClass]
    public partial class AuthProviderTests : TestBase
    {
        public class OCIConfigInfo
        {
            internal IEnumerable<string> Lines { get; }
            internal string PKFileData { get; }
            internal string ProfileName { get; }

            internal string SessTokenFileData { get; }

            // For now require that if profile specifies valid region, it must
            // be TestRegion (which host name is used for signature
            // verification).
            internal bool HasRegion { get; }

            internal OCIConfigInfo(IEnumerable<string> lines,
                string pkFileData = null, string profileName = null,
                string sessTokenFileData = null, bool hasRegion = false)
            {
                Lines = lines;
                PKFileData = pkFileData;
                ProfileName = profileName;
                SessTokenFileData = sessTokenFileData;
                HasRegion = hasRegion;
            }
        }

        public class IAMConfigInfo
        {
            internal IAMAuthorizationProvider Provider { get; }
            internal string PKFileData { get; }
            internal IEnumerable<string> OCIConfigLines { get; }
            internal string SessTokenFileData { get; }
            internal bool HasRegion { get; }

            internal IAMConfigInfo(IAMAuthorizationProvider provider,
                string pkFileData = null)
            {
                Provider = provider;
                PKFileData = pkFileData;
                OCIConfigLines = null;
                SessTokenFileData = null;
            }

            internal IAMConfigInfo(IAMAuthorizationProvider provider,
                OCIConfigInfo oci)
            {
                Provider = provider;
                PKFileData = oci.PKFileData;
                OCIConfigLines = oci.Lines;
                HasRegion = oci.HasRegion;
                SessTokenFileData = oci.SessTokenFileData;
            }

            internal IAMConfigInfo(OCIConfigInfo oci) : this(
                oci.SessTokenFileData != null
                    ? IAMAuthorizationProvider.CreateWithSessionToken(
                        OCIConfigFile, oci.ProfileName)
                    : new IAMAuthorizationProvider(OCIConfigFile,
                        oci.ProfileName),
                oci)
            {
            }
        }

        private static IAMConfigInfo[] GoodDirectIAMConfigs =>
            new[]
            {
                new IAMConfigInfo(new IAMAuthorizationProvider(CredentialsPK)),
                new IAMConfigInfo(
                    new IAMAuthorizationProvider(CredentialsPKPem)),
                new IAMConfigInfo(
                    new IAMAuthorizationProvider(CredentialsPKFile),
                    Keys.PrivatePKCS8PEM),
                new IAMConfigInfo(
                    new IAMAuthorizationProvider(CredentialsPKEncPem)),
                new IAMConfigInfo(
                    new IAMAuthorizationProvider(CredentialsPKEncFile),
                    Keys.PrivatePKCS8EncryptedPEM)
            };

        private static readonly OCIConfigInfo DefaultOCIConfig =
            new OCIConfigInfo(DefaultProfileStart.Concat(OCIConfigLines),
                Keys.PrivatePKCS8PEM);

        private static readonly OCIConfigInfo[] GoodOCIConfigs =
        {
            DefaultOCIConfig,
            new OCIConfigInfo(Enumerable.Empty<string>()
                    .Append("[test_profile]").Concat(OCIConfigLines)
                    .Append("# comment comment"), Keys.PrivatePKCS8PEM,
                "test_profile"),
            new OCIConfigInfo(DefaultProfileStart.Append("")
                    .Concat(OCIConfigLinesEncKey)
                    .Concat(new[] { "", "", "\n" }),
                Keys.PrivatePKCS8EncryptedPEM),
            new OCIConfigInfo(new[] { "[sample1]", "property 1 = 2" }
                    .Concat(DefaultProfileStart).Concat(OCIConfigLines).Append(
                        $"       fingerprint     = {Fingerprint}      "),
                Keys.PrivatePKCS8PEM),
            new OCIConfigInfo((from idx in Enumerable.Range(0, 100)
                    select Enumerable.Empty<string>().Append($"[profile{idx}]")
                        .Concat(OCIConfigLinesEncKey))
                .SelectMany(elem => elem),
                Keys.PrivatePKCS8EncryptedPEM, "profile70"),
            new OCIConfigInfo(DefaultProfileStart.Concat(
                    OCIConfigLinesSessToken), Keys.PrivatePKCS8PEM, null,
                SessionToken),
            new OCIConfigInfo(Enumerable.Empty<string>()
                    .Append("[test_profile]").Concat(OCIConfigLinesSessToken)
                    // Extra properties not needed for sess token auth
                    .Concat(
                        new[]
                        {
                            $"user={UserId}", $"fingerprint={Fingerprint}"
                        }),
                Keys.PrivatePKCS8PEM, "test_profile",
                SessionToken),
            new OCIConfigInfo(DefaultProfileStart.Concat(
                    OCIConfigLinesEncKeySessToken),
                Keys.PrivatePKCS8EncryptedPEM, null, SessionToken)
        };

        private static IEnumerable<IAMConfigInfo> GoodIAMConfigs =>
            GoodDirectIAMConfigs
                .Concat(
                    from oci in GoodOCIConfigs select new IAMConfigInfo(oci))
                .Concat(
                    from cfg in GoodDirectIAMConfigs
                    select new IAMConfigInfo(
                        new IAMAuthorizationProvider(
                            cancellationToken =>
                                Task.FromResult(
                                    CopyCredentials(
                                        cfg.Provider.Credentials))),
                        cfg.PKFileData))
                .Concat(
                    from cfg in GoodDirectIAMConfigs
                    select new IAMConfigInfo(
                        new IAMAuthorizationProvider(
                            async cancellationToken =>
                            {
                                await Task.Delay(25);
                                return CopyCredentials(
                                    cfg.Provider.Credentials);
                            }), cfg.PKFileData));

        private static IEnumerable<object[]> GoodIAMConfigsDataSource =>
            (from iam in GoodIAMConfigs
                select new object[] { iam, CompartmentId })
            .Concat(from iam in GoodIAMConfigs
                select new object[] { iam, null });

        private static NoSQLConfig MakeNoSQLConfig(IAMConfigInfo iam,
            string compartment = null) =>
            new NoSQLConfig
            {
                Region = iam.HasRegion ? null : TestRegion,
                AuthorizationProvider = iam.Provider,
                Compartment = compartment
            };

        private static string GetKeyId(IAMConfigInfo iam) =>
            iam.SessTokenFileData != null
                ? GetKeyIdFromToken(iam.SessTokenFileData)
                : CredentialsKeyId;

        private class TestCredentialsProfileProvider :
            CredentialsProfileProvider
        {
            private readonly TimeSpan profileTTL;

            internal TestCredentialsProfileProvider(IAMCredentials credentials,
                TimeSpan? profileTTL = null) : base(credentials)
            {
                this.profileTTL = profileTTL ?? TimeSpan.MaxValue;
            }

            internal override TimeSpan ProfileTTL => profileTTL;
        }

        private class TestMutableProfileProvider :
            AuthenticationProfileProvider
        {
            internal TestMutableProfileProvider(string keyId)
            {
                KeyId = keyId;
            }

            internal string KeyId { get; set; }

            internal bool Valid { get; set; } = true;

            internal override bool IsProfileValid => Valid;

            internal int ProfileLoadCount { get; private set; }

            internal override Task<AuthenticationProfile> GetProfileAsync(
                bool forceRefresh, CancellationToken cancellationToken)
            {
                ProfileLoadCount++;
                Valid = true;
                return Task.FromResult(new AuthenticationProfile(KeyId,
                    Keys.RSA, TenantId));
            }

            protected override void Dispose(bool disposing)
            {
            }
        }

        private static void UseTestProfileProvider(
            IAMAuthorizationProvider provider,
            AuthenticationProfileProvider profileProvider)
        {
            var profileProviderField =
                typeof(IAMAuthorizationProvider).GetField("profileProvider",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.IsNotNull(profileProviderField);

            (profileProviderField.GetValue(provider) as IDisposable)?.Dispose();
            profileProviderField.SetValue(provider, profileProvider);
        }

        private static void UseTestProfileProvider(
            IAMAuthorizationProvider provider, TimeSpan? profileTTL = null)
        {
            UseTestProfileProvider(provider,
                new TestCredentialsProfileProvider(CredentialsPK,
                    profileTTL));
        }

        private IAMConfigInfo currentIAMConfig;

        private void PrepareConfig(IAMConfigInfo iam)
        {
            currentIAMConfig = iam;

            if (iam == null)
            {
                return;
            }

            File.WriteAllText(PrivateKeyFile, iam.PKFileData ?? "");
            File.WriteAllLines(OCIConfigFile,
                iam.OCIConfigLines ?? Array.Empty<string>());
            File.WriteAllText(SessionTokenFile, iam.SessTokenFileData ?? "");
        }

        [ClassCleanup]
        public new static void ClassCleanup()
        {
            TestBase.ClassCleanup();
            RemoveTempFiles();
        }

        // TestCleanup cannot access dynamic data, so we have to manually save
        // the instance of IAMConfigInfo.

        [TestCleanup]
        public void TestCleanup()
        {
            currentIAMConfig?.Provider.Dispose();
            currentIAMConfig = null;
        }

        [DataTestMethod]
        [DynamicData(nameof(GoodIAMConfigsDataSource))]
        public async Task TestAuthProviderAsync(IAMConfigInfo iam,
            string compartment)
        {
            PrepareConfig(iam);

            var cfg = MakeNoSQLConfig(iam, compartment);
            // ConfigureAuthorization() will be called during NoSQLClient
            // constructor.
            var client = new NoSQLClient(cfg);

            // At this time it doesn't matter what subclass of Request we
            // provide.
            var request = new GetTableRequest(client, "table", null);
            var message = new HttpRequestMessage();

            await iam.Provider.ApplyAuthorizationAsync(request, message,
                CancellationToken.None);

            VerifyAuth(message.Headers, GetKeyId(iam), Keys.RSA,
                compartment ?? TenantId);
        }

        [TestMethod]
        public async Task TestAuthProviderSignsActualHttpMethodAsync()
        {
            var iam = GoodDirectIAMConfigs.First();
            PrepareConfig(iam);

            var cfg = MakeNoSQLConfig(iam, CompartmentId);
            var client = new NoSQLClient(cfg);
            var request = new GetTableRequest(client, "table", null);

            var postMessage = new HttpRequestMessage(HttpMethod.Post,
                new Uri(NoSQLDataPath, UriKind.Relative));
            await iam.Provider.ApplyAuthorizationAsync(request, postMessage,
                CancellationToken.None);
            VerifyAuth(postMessage.Headers, GetKeyId(iam), Keys.RSA,
                CompartmentId, requestTarget: $"post /{NoSQLDataPath}");

            var headMessage = new HttpRequestMessage(HttpMethod.Head,
                new Uri(NoSQLDataPath, UriKind.Relative));
            await iam.Provider.ApplyAuthorizationAsync(request, headMessage,
                CancellationToken.None);
            VerifyAuth(headMessage.Headers, GetKeyId(iam), Keys.RSA,
                CompartmentId, requestTarget: $"head /{NoSQLDataPath}");

            var cachedPostMessage = new HttpRequestMessage(HttpMethod.Post,
                new Uri(NoSQLDataPath, UriKind.Relative));
            await iam.Provider.ApplyAuthorizationAsync(request,
                cachedPostMessage, CancellationToken.None);
            VerifyAuthEqual(cachedPostMessage.Headers, postMessage.Headers,
                GetKeyId(iam), Keys.RSA, CompartmentId);
        }

        [TestMethod]
        public async Task TestNonCacheableProfileRefreshUpdatesPostCacheAsync()
        {
            var iam = GoodDirectIAMConfigs.First();
            PrepareConfig(iam);

            var cfg = MakeNoSQLConfig(iam, CompartmentId);
            var client = new NoSQLClient(cfg);
            var request = new GetTableRequest(client, "table", null);
            var profileProvider =
                new TestMutableProfileProvider("old-profile");
            UseTestProfileProvider(iam.Provider, profileProvider);

            var firstPostMessage = new HttpRequestMessage(HttpMethod.Post,
                new Uri(NoSQLDataPath, UriKind.Relative));
            await iam.Provider.ApplyAuthorizationAsync(request,
                firstPostMessage, CancellationToken.None);
            VerifyAuth(firstPostMessage.Headers, "old-profile", Keys.RSA,
                CompartmentId, requestTarget: $"post /{NoSQLDataPath}");

            profileProvider.KeyId = "new-profile";
            profileProvider.Valid = false;

            var headMessage = new HttpRequestMessage(HttpMethod.Head,
                new Uri(NoSQLDataPath, UriKind.Relative));
            await iam.Provider.ApplyAuthorizationAsync(request, headMessage,
                CancellationToken.None);
            VerifyAuth(headMessage.Headers, "new-profile", Keys.RSA,
                CompartmentId, requestTarget: $"head /{NoSQLDataPath}");

            var secondPostMessage = new HttpRequestMessage(HttpMethod.Post,
                new Uri(NoSQLDataPath, UriKind.Relative));
            await iam.Provider.ApplyAuthorizationAsync(request,
                secondPostMessage, CancellationToken.None);
            VerifyAuth(secondPostMessage.Headers, "new-profile", Keys.RSA,
                CompartmentId, requestTarget: $"post /{NoSQLDataPath}");
            Assert.AreEqual(3, profileProvider.ProfileLoadCount);
        }

        private static IEnumerable<object[]> CacheRefreshTestDataSource =>
            Enumerable.Empty<object[]>()
                // one of each type (credentials, OCI config file, credentials
                // provider)
                .Append(new object[] { GoodDirectIAMConfigs.First() })
                .Append(new object[]
                    { new IAMConfigInfo(GoodOCIConfigs.First()) })
                .Append(new object[]
                {
                    GoodIAMConfigs.First(iam =>
                        iam.Provider.CredentialsProvider != null)
                });

        [DataTestMethod]
        [DynamicData(nameof(CacheRefreshTestDataSource))]
        public async Task TestAuthProviderCacheAsync(IAMConfigInfo iam)
        {
            PrepareConfig(iam);

            iam.Provider.CacheDuration = TimeSpan.FromSeconds(2);
            iam.Provider.RefreshAhead = TimeSpan.Zero;

            var cfg = MakeNoSQLConfig(iam, CompartmentId);
            var client = new NoSQLClient(cfg);
            var request = new GetTableRequest(client, "table", null);
            
            var message1 = new HttpRequestMessage();
            var message2 = new HttpRequestMessage();

            await iam.Provider.ApplyAuthorizationAsync(request, message1,
                CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(1));

            await iam.Provider.ApplyAuthorizationAsync(request, message2,
                CancellationToken.None);
            // +1000 ms, the signature should still be valid
            VerifyAuthEqual(message2.Headers, message1.Headers, GetKeyId(iam),
                Keys.RSA, CompartmentId);
            
            await Task.Delay(TimeSpan.FromMilliseconds(1100));
            message2.Headers.Clear();
            await iam.Provider.ApplyAuthorizationAsync(request, message2,
                CancellationToken.None);
            // +2100 ms, now the signature should have expired, so the new
            // one should be generated.
            VerifyAuthLaterDate(message2.Headers, message1.Headers,
                GetKeyId(iam), Keys.RSA, CompartmentId);
        }

        [DataTestMethod]
        [DynamicData(nameof(CacheRefreshTestDataSource))]
        public async Task TestAuthProviderRefreshAsync(IAMConfigInfo iam)
        {
            PrepareConfig(iam);

            iam.Provider.CacheDuration = TimeSpan.FromSeconds(3);
            iam.Provider.RefreshAhead = TimeSpan.FromSeconds(1);

            var cfg = MakeNoSQLConfig(iam, CompartmentId);
            var client = new NoSQLClient(cfg);
            var request = new GetTableRequest(client, "table", null);
            
            var message1 = new HttpRequestMessage();
            var message2 = new HttpRequestMessage();

            await iam.Provider.ApplyAuthorizationAsync(request, message1,
                CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(1));
            
            await iam.Provider.ApplyAuthorizationAsync(request, message2,
                CancellationToken.None);
            // +1000 ms, no refresh yet
            VerifyAuthEqual(message2.Headers, message1.Headers, GetKeyId(iam),
                Keys.RSA, CompartmentId);
            await Task.Delay(TimeSpan.FromMilliseconds(1200));

            message2.Headers.Clear();
            await iam.Provider.ApplyAuthorizationAsync(request, message2,
                CancellationToken.None);
            // +2200 ms, automatic refresh should have happened
            VerifyAuthLaterDate(message2.Headers, message1.Headers,
                GetKeyId(iam), Keys.RSA, CompartmentId);
            await Task.Delay(TimeSpan.FromMilliseconds(1600));

            // Now will use headers2 as base and headers1 as new value.
            message1.Headers.Clear();
            await iam.Provider.ApplyAuthorizationAsync(request, message1,
                CancellationToken.None);
            // +3800 ms, shouldn't change again within 2s of last refresh
            VerifyAuthEqual(message1.Headers, message2.Headers, GetKeyId(iam),
                Keys.RSA, CompartmentId);
            await Task.Delay(TimeSpan.FromMilliseconds(400));

            message1.Headers.Clear();
            await iam.Provider.ApplyAuthorizationAsync(request, message1,
                CancellationToken.None);
            // +4200 ms, automatic refresh should have happened again
            VerifyAuthLaterDate(message1.Headers, message2.Headers,
                GetKeyId(iam), Keys.RSA, CompartmentId);
        }

        [TestMethod]
        public async Task TestStaticDelegationTokenCacheAsync()
        {
            var provider =
                IAMAuthorizationProvider.CreateWithInstancePrincipalForDelegation(
                    DelegationToken);
            provider.RefreshAhead = TimeSpan.Zero;

            var client = new NoSQLClient(new NoSQLConfig
            {
                Region = TestRegion,
                AuthorizationProvider = provider,
                Compartment = CompartmentId
            });
            UseTestProfileProvider(provider);

            var request = new GetTableRequest(client, "table", null);
            var message1 = new HttpRequestMessage();
            var message2 = new HttpRequestMessage();

            try
            {
                await provider.ApplyAuthorizationAsync(request, message1,
                    CancellationToken.None);
                VerifyAuth(message1.Headers, CredentialsKeyId, Keys.RSA,
                    CompartmentId, DelegationToken);

                await provider.ApplyAuthorizationAsync(request, message2,
                    CancellationToken.None);
                VerifyAuthEqual(message2.Headers, message1.Headers,
                    CredentialsKeyId, Keys.RSA, CompartmentId,
                    DelegationToken);
            }
            finally
            {
                provider.Dispose();
            }
        }

        [TestMethod]
        public async Task TestDynamicDelegationTokenProviderCalledWithValidCacheAsync()
        {
            var providerCallCount = 0;
            var provider =
                IAMAuthorizationProvider.CreateWithInstancePrincipalForDelegation(
                    cancellationToken =>
                    {
                        Interlocked.Increment(ref providerCallCount);
                        return Task.FromResult(DelegationToken);
                    });
            provider.RefreshAhead = TimeSpan.Zero;

            var client = new NoSQLClient(new NoSQLConfig
            {
                Region = TestRegion,
                AuthorizationProvider = provider,
                Compartment = CompartmentId
            });
            UseTestProfileProvider(provider);

            var request = new GetTableRequest(client, "table", null);
            var message1 = new HttpRequestMessage();
            var message2 = new HttpRequestMessage();

            try
            {
                await provider.ApplyAuthorizationAsync(request, message1,
                    CancellationToken.None);
                await provider.ApplyAuthorizationAsync(request, message2,
                    CancellationToken.None);

                Assert.AreEqual(2, providerCallCount);
                VerifyAuthEqual(message2.Headers, message1.Headers,
                    CredentialsKeyId, Keys.RSA, CompartmentId,
                    DelegationToken);
            }
            finally
            {
                provider.Dispose();
            }
        }

        [TestMethod]
        public async Task TestDynamicDelegationTokenProviderDefaultRefreshAheadAsync()
        {
            var currentDelegationToken = DelegationToken;
            var providerCallCount = 0;
            var provider =
                IAMAuthorizationProvider.CreateWithInstancePrincipalForDelegation(
                    cancellationToken =>
                    {
                        Interlocked.Increment(ref providerCallCount);
                        return Task.FromResult(currentDelegationToken);
                    });
            provider.CacheDuration = TimeSpan.FromSeconds(20);

            var client = new NoSQLClient(new NoSQLConfig
            {
                Region = TestRegion,
                AuthorizationProvider = provider,
                Compartment = CompartmentId
            });
            UseTestProfileProvider(provider, TimeSpan.FromSeconds(11));

            var request = new GetTableRequest(client, "table", null);
            var message1 = new HttpRequestMessage();
            var message2 = new HttpRequestMessage();
            var updatedDelegationToken = DelegationToken + "-updated";

            try
            {
                Assert.AreNotEqual(TimeSpan.Zero, provider.RefreshAhead);
                await provider.ApplyAuthorizationAsync(request, message1,
                    CancellationToken.None);
                VerifyAuth(message1.Headers, CredentialsKeyId, Keys.RSA,
                    CompartmentId, DelegationToken);

                currentDelegationToken = updatedDelegationToken;
                await Task.Delay(TimeSpan.FromMilliseconds(1500));
                Assert.AreEqual(1, providerCallCount);

                await provider.ApplyAuthorizationAsync(request, message2,
                    CancellationToken.None);
                Assert.AreEqual(2, providerCallCount);
                VerifyAuth(message2.Headers, CredentialsKeyId, Keys.RSA,
                    CompartmentId, updatedDelegationToken);
            }
            finally
            {
                provider.Dispose();
            }
        }

        [TestMethod]
        public async Task TestConcurrentDynamicDelegationTokensAsync()
        {
            var currentDelegationToken = new AsyncLocal<string>();
            var providerCallCount = 0;
            var providerCallsStarted = new TaskCompletionSource<bool>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            var provider =
                IAMAuthorizationProvider.CreateWithInstancePrincipalForDelegation(
                    async cancellationToken =>
                    {
                        if (Interlocked.Increment(ref providerCallCount) == 2)
                        {
                            providerCallsStarted.TrySetResult(true);
                        }

                        await providerCallsStarted.Task;
                        return currentDelegationToken.Value;
                    });
            provider.RefreshAhead = TimeSpan.Zero;

            var client = new NoSQLClient(new NoSQLConfig
            {
                Region = TestRegion,
                AuthorizationProvider = provider,
                Compartment = CompartmentId
            });
            UseTestProfileProvider(provider);

            var request = new GetTableRequest(client, "table", null);
            var aliceToken = DelegationToken + "-alice";
            var bobToken = DelegationToken + "-bob";

            async Task<HttpRequestMessage> ApplyWithToken(string token)
            {
                currentDelegationToken.Value = token;
                var message = new HttpRequestMessage();
                await provider.ApplyAuthorizationAsync(request, message,
                    CancellationToken.None);
                return message;
            }

            try
            {
                var aliceTask = Task.Run(() => ApplyWithToken(aliceToken));
                var bobTask = Task.Run(() => ApplyWithToken(bobToken));
                var allTasks = Task.WhenAll(aliceTask, bobTask);

                if (await Task.WhenAny(allTasks,
                    Task.Delay(TimeSpan.FromSeconds(5))) != allTasks)
                {
                    providerCallsStarted.TrySetResult(true);
                    Assert.Fail("Timed out waiting for concurrent " +
                                "delegation token requests");
                }

                var messages = await allTasks;
                Assert.AreEqual(2, providerCallCount);
                VerifyAuth(messages[0].Headers, CredentialsKeyId, Keys.RSA,
                    CompartmentId, aliceToken);
                VerifyAuth(messages[1].Headers, CredentialsKeyId, Keys.RSA,
                    CompartmentId, bobToken);
            }
            finally
            {
                providerCallsStarted.TrySetResult(true);
                provider.Dispose();
            }
        }

        [TestMethod]
        public async Task TestDynamicDelegationTokenProviderCacheAsync()
        {
            var currentDelegationToken = DelegationToken;
            var provider =
                IAMAuthorizationProvider.CreateWithInstancePrincipalForDelegation(
                    cancellationToken =>
                        Task.FromResult(currentDelegationToken));
            provider.RefreshAhead = TimeSpan.Zero;

            var client = new NoSQLClient(new NoSQLConfig
            {
                Region = TestRegion,
                AuthorizationProvider = provider,
                Compartment = CompartmentId
            });
            UseTestProfileProvider(provider);

            var request = new GetTableRequest(client, "table", null);
            var message1 = new HttpRequestMessage();
            var message2 = new HttpRequestMessage();
            var message3 = new HttpRequestMessage();
            var updatedDelegationToken = DelegationToken + "-updated";

            try
            {
                await provider.ApplyAuthorizationAsync(request, message1,
                    CancellationToken.None);
                VerifyAuth(message1.Headers, CredentialsKeyId, Keys.RSA,
                    CompartmentId, DelegationToken);

                currentDelegationToken = updatedDelegationToken;
                await provider.ApplyAuthorizationAsync(request, message2,
                    CancellationToken.None);
                VerifyAuth(message2.Headers, CredentialsKeyId, Keys.RSA,
                    CompartmentId, updatedDelegationToken);
                Assert.AreNotEqual(message1.Headers.Authorization?.ToString(),
                    message2.Headers.Authorization?.ToString());

                await provider.ApplyAuthorizationAsync(request, message3,
                    CancellationToken.None);
                VerifyAuthEqual(message3.Headers, message2.Headers,
                    CredentialsKeyId, Keys.RSA, CompartmentId,
                    updatedDelegationToken);
            }
            finally
            {
                provider.Dispose();
            }
        }

        [TestMethod]
        public async Task TestDynamicDelegationTokenFileCacheAsync()
        {
            var delegationTokenFile = Path.GetTempFileName();
            IAMAuthorizationProvider provider = null;
            try
            {
                File.WriteAllText(delegationTokenFile, DelegationToken);

                provider = IAMAuthorizationProvider
                    .CreateWithInstancePrincipalForDelegationFromFile(
                        delegationTokenFile);
                provider.RefreshAhead = TimeSpan.Zero;

                var client = new NoSQLClient(new NoSQLConfig
                {
                    Region = TestRegion,
                    AuthorizationProvider = provider,
                    Compartment = CompartmentId
                });
                UseTestProfileProvider(provider);

                var request = new GetTableRequest(client, "table", null);
                var message1 = new HttpRequestMessage();
                var message2 = new HttpRequestMessage();
                var message3 = new HttpRequestMessage();
                var updatedDelegationToken = DelegationToken + "-updated";

                await provider.ApplyAuthorizationAsync(request, message1,
                    CancellationToken.None);
                VerifyAuth(message1.Headers, CredentialsKeyId, Keys.RSA,
                    CompartmentId, DelegationToken);

                File.WriteAllText(delegationTokenFile, updatedDelegationToken);
                await provider.ApplyAuthorizationAsync(request, message2,
                    CancellationToken.None);
                VerifyAuth(message2.Headers, CredentialsKeyId, Keys.RSA,
                    CompartmentId, updatedDelegationToken);
                Assert.AreNotEqual(
                    message1.Headers.Authorization?.ToString(),
                    message2.Headers.Authorization?.ToString());

                await provider.ApplyAuthorizationAsync(request, message3,
                    CancellationToken.None);
                VerifyAuthEqual(message3.Headers, message2.Headers,
                    CredentialsKeyId, Keys.RSA, CompartmentId,
                    updatedDelegationToken);
            }
            finally
            {
                provider?.Dispose();
                File.Delete(delegationTokenFile);
            }
        }

    }

}
