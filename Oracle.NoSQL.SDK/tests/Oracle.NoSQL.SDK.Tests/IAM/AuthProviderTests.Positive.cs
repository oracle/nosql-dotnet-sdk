/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
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
    using System.Threading;
    using System.Threading.Tasks;
    using System.Net.Http;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static Tests.Utils;
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
            var headers = new HttpRequestMessage().Headers;

            await iam.Provider.ApplyAuthorizationAsync(request, headers,
                CancellationToken.None);

            VerifyAuth(headers, GetKeyId(iam), Keys.RSA,
                compartment ?? TenantId);
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
            
            var headers1 = new HttpRequestMessage().Headers;
            var headers2 = new HttpRequestMessage().Headers;

            await iam.Provider.ApplyAuthorizationAsync(request, headers1,
                CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(1));

            await iam.Provider.ApplyAuthorizationAsync(request, headers2,
                CancellationToken.None);
            // +1000 ms, the signature should still be valid
            VerifyAuthEqual(headers2, headers1, GetKeyId(iam), Keys.RSA,
                CompartmentId);
            
            await Task.Delay(TimeSpan.FromMilliseconds(1100));
            headers2.Clear();
            await iam.Provider.ApplyAuthorizationAsync(request, headers2,
                CancellationToken.None);
            // +2100 ms, now the signature should have expired, so the new
            // one should be generated.
            VerifyAuthLaterDate(headers2, headers1, GetKeyId(iam), Keys.RSA,
                CompartmentId);
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
            
            var headers1 = new HttpRequestMessage().Headers;
            var headers2 = new HttpRequestMessage().Headers;

            await iam.Provider.ApplyAuthorizationAsync(request, headers1,
                CancellationToken.None);
            await Task.Delay(TimeSpan.FromSeconds(1));
            
            await iam.Provider.ApplyAuthorizationAsync(request, headers2,
                CancellationToken.None);
            // +1000 ms, no refresh yet
            VerifyAuthEqual(headers2, headers1, GetKeyId(iam), Keys.RSA,
                CompartmentId);
            await Task.Delay(TimeSpan.FromMilliseconds(1200));

            headers2.Clear();
            await iam.Provider.ApplyAuthorizationAsync(request, headers2,
                CancellationToken.None);
            // +2200 ms, automatic refresh should have happened
            VerifyAuthLaterDate(headers2, headers1, GetKeyId(iam), Keys.RSA,
                CompartmentId);
            await Task.Delay(TimeSpan.FromMilliseconds(1600));

            // Now will use headers2 as base and headers1 as new value.
            headers1.Clear();
            await iam.Provider.ApplyAuthorizationAsync(request, headers1,
                CancellationToken.None);
            // +3800 ms, shouldn't change again within 2s of last refresh
            VerifyAuthEqual(headers1, headers2, GetKeyId(iam), Keys.RSA,
                CompartmentId);
            await Task.Delay(TimeSpan.FromMilliseconds(400));

            headers1.Clear();
            await iam.Provider.ApplyAuthorizationAsync(request, headers1,
                CancellationToken.None);
            // +4200 ms, automatic refresh should have happened again
            VerifyAuthLaterDate(headers1, headers2, GetKeyId(iam), Keys.RSA,
                CompartmentId);
        }

    }

}
