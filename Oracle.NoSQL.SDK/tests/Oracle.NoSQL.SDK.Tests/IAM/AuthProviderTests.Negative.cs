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
    using System.Net.Http;
    using System.Net.Http.Headers;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static NegativeTestData;
    using static Tests.Utils;
    using static TestData;

    public partial class AuthProviderTests
    {
        private static readonly IEnumerable<string> BadOCIDs =
            BadNonEmptyStrings.Append("blah");

        private static readonly string[] BadPrivateKeyPems =
        {
            string.Empty,
            ".....",
            Path.GetFullPath("no_such_file"),
            Keys.PrivatePKCS8PEM[1..^1] // corrupted PEM key
        };

        // Immediate means it should fail before any requests are made, during
        // IAMAuthorizationProvider.ConfigureAuthorization().
        // Eventual means it should fail when request for authorization is
        // made (IAMAuthorizationProvider.ApplyAuthorizationAsync()).

        private static readonly IEnumerable<IAMCredentials>
            BadIAMCredentialsImmediate =
                (from ocid in BadOCIDs
                    select CombineProperties(CredentialsPK,
                        new { TenantId = ocid }))
                .Concat(from userId in BadOCIDs
                    select CombineProperties(
                        CredentialsPKPem, new { UserId = userId }))
                .Concat(from fingerprint in BadNonEmptyStrings
                    select CombineProperties(CredentialsPKPem,
                        new { Fingerprint = fingerprint }))
                // Missing any private key info
                .Append(CredentialsBase)
                // Cannot have both private key and private key pem
                .Append(CombineProperties(CredentialsPK,
                    new { PrivateKeyPEM = Keys.PrivatePKCS8PEM }))
                // Cannot have both private key and private key file
                .Append(CombineProperties(CredentialsPK,
                    new { PrivateKeyFile }))
                // Cannot have both private key pem and private key file
                .Append(CombineProperties(CredentialsPKPem,
                    new { PrivateKeyFile }))
                // Cannot have empty passphrase
                .Append(CombineProperties(CredentialsPKFile,
                    new { Passphrase = "".ToCharArray() }));

        private static readonly IEnumerable<IAMCredentials>
            BadIAMCredentialsEventual =
                // Bad private key data
                (from pem in BadPrivateKeyPems
                    select CombineProperties(CredentialsBase,
                        new { PrivateKeyPEM = pem }))
                // Missing passphrase for encrypted key
                .Append(CombineProperties(CredentialsBase,
                    new { PrivateKeyPEM = Keys.PrivatePKCS8EncryptedPEM }))
                // Wrong passphrase for encrypted key
                .Append(CombineProperties(CredentialsBase, new
                {
                    PrivateKeyPEM = Keys.PrivatePKCS8EncryptedPEM,
                    Passphrase = "oracle1".ToCharArray()
                }))
                .Append(CombineProperties(CredentialsBase,
                    new { PrivateKeyFile = "no_such_file" }));

        private static readonly IEnumerable<IAMCredentials>
            BadIAMCredentials = BadIAMCredentialsImmediate.Concat(
                BadIAMCredentialsEventual);

        private static readonly IEnumerable<OCIConfigInfo>
            BadOCIConfigsImmediate = Enumerable.Empty<OCIConfigInfo>()
                // Empty OCI config file
                .Append(new OCIConfigInfo(Enumerable.Empty<string>()))
                // Invalid string in profile
                .Append(new OCIConfigInfo(
                    DefaultProfileStart.Append("blah blah")
                        .Concat(OCIConfigLines)))
                // Missing profile John
                .Append(new OCIConfigInfo(
                    DefaultProfileStart.Append("#comment")
                        .Concat(OCIConfigLinesEncKey).Append("")
                        .Append("#comment2"),
                    Keys.PrivatePKCS8EncryptedPEM, "John"))
                // Missing one of the required properties in the profile
                .Concat(
                    from idx in Enumerable.Range(0,
                        OCIConfigLines.Count())
                    select new OCIConfigInfo(DefaultProfileStart.Concat(
                        // delete element at index idx 
                        OCIConfigLines.Take(idx).Skip(1)
                            .Take(OCIConfigLines.Count() - idx - 1))))
                // Same as above for session token auth
                .Concat(
                    from idx in Enumerable.Range(0,
                        OCIConfigLinesSessToken.Count())
                    select new OCIConfigInfo(DefaultProfileStart.Concat(
                            // delete element at index idx 
                            OCIConfigLinesSessToken.Take(idx).Skip(1)
                                .Take(
                                    OCIConfigLinesSessToken.Count() - idx -
                                    1)),
                        null, null, SessionToken))
                // Invalid tenant id
                .Append(new OCIConfigInfo(DefaultProfileStart.Concat(
                    OCIConfigLines.Select(line =>
                        line.StartsWith("tenancy")
                            ? "tenancy=tenancy1"
                            : line))))
                // Invalid tenant id for session token auth
                .Append(new OCIConfigInfo(DefaultProfileStart.Concat(
                    OCIConfigLinesSessToken.Select(line =>
                        line.StartsWith("tenancy")
                            ? "tenancy=tenancy1"
                            : line)), null, null, SessionToken))
                // Invalid user id
                .Append(new OCIConfigInfo(DefaultProfileStart.Concat(
                    OCIConfigLines.Select(line =>
                        line.StartsWith("user")
                            ? "user=user1"
                            : line))))
                // Empty fingerprint
                .Append(new OCIConfigInfo(DefaultProfileStart.Concat(
                    OCIConfigLines.Select(line =>
                        line.StartsWith("fingerprint")
                            ? "fingerprint="
                            : line))));

        private static readonly IEnumerable<OCIConfigInfo>
            BadOCIConfigsEventual = Enumerable.Empty<OCIConfigInfo>()
                // Missing passphrase for encrypted key
                .Append(new OCIConfigInfo(
                    DefaultProfileStart.Concat(OCIConfigLines).Append(""),
                    Keys.PrivatePKCS8EncryptedPEM))
                // Bad private key data
                .Concat(
                    from pem in BadPrivateKeyPems
                    select new OCIConfigInfo(
                        DefaultProfileStart.Concat(OCIConfigLines), pem))
                // Same for session token auth
                .Concat(
                    from pem in BadPrivateKeyPems
                    select new OCIConfigInfo(
                        DefaultProfileStart.Concat(OCIConfigLinesSessToken),
                        pem, null, SessionToken))
                // Invalid session token file
                .Append(new OCIConfigInfo(DefaultProfileStart.Concat(
                        OCIConfigLinesSessToken.Select(line =>
                            line.StartsWith("security_token_file")
                                ? "security_token_file=no_such_file"
                                : line)), Keys.PrivatePKCS8PEM, null,
                    SessionToken))
                // Empty session token
                .Append(new OCIConfigInfo(
                    DefaultProfileStart.Concat(OCIConfigLinesSessToken),
                    Keys.PrivatePKCS8PEM, null, string.Empty));

        private static readonly
            IEnumerable<Func<CancellationToken, Task<IAMCredentials>>>
            BadCredentialsProviders =
                (from creds in BadIAMCredentials
                    select new Func<CancellationToken, Task<IAMCredentials>>(
                        cancellationToken => Task.FromResult(creds)))
                .Append((cancellationToken) =>
                    Task.FromResult<IAMCredentials>(null))
                .Append(cancellationToken =>
                    throw new Exception("provider error"))
                .Append(async cancellationToken =>
                {
                    await Task.Delay(10);
                    throw new TaskCanceledException(
                        "provider operation cancelled");
                });

        // IAMAuthorizationProvider is not supposed to be reused once
        // Dispose() is called. We use properties instead of fields below so
        // that new instances are created each time DynamicData is enumerated.
        // This will prevent potential sharing of IAMAuthorizationProvider
        // instances.

        // Providers with mutually exclusive properties specified.
        private static IAMAuthorizationProvider[] InvalidAuthProviders =>
            new[]
            {
                new IAMAuthorizationProvider
                {
                    UseResourcePrincipal = true,
                    UseInstancePrincipal = true
                },
                new IAMAuthorizationProvider
                {
                    UseResourcePrincipal = true,
                    UseSessionToken = true
                },
                new IAMAuthorizationProvider
                {
                    UseResourcePrincipal = true,
                    Credentials = CredentialsPKFile
                },
                new IAMAuthorizationProvider
                {
                    UseResourcePrincipal = true,
                    ConfigFile = OCIConfigFile
                },
                new IAMAuthorizationProvider
                {
                    UseResourcePrincipal = true,
                    ProfileName = "DEFAULT"
                },
                new IAMAuthorizationProvider
                {
                    UseResourcePrincipal = true,
                    CredentialsProvider =
                        cancellationToken => Task.FromResult(CredentialsPKPem)
                },
                new IAMAuthorizationProvider
                {
                    UseInstancePrincipal = true,
                    UseSessionToken = true
                },
                new IAMAuthorizationProvider
                {
                    UseInstancePrincipal = true,
                    Credentials = CredentialsPKFile
                },
                new IAMAuthorizationProvider
                {
                    UseInstancePrincipal = true,
                    ConfigFile = OCIConfigFile
                },
                new IAMAuthorizationProvider
                {
                    UseInstancePrincipal = true,
                    ProfileName = "DEFAULT"
                },
                new IAMAuthorizationProvider
                {
                    UseInstancePrincipal = true,
                    CredentialsProvider =
                        cancellationToken => Task.FromResult(CredentialsPKPem)
                },
                new IAMAuthorizationProvider
                {
                    UseSessionToken = true,
                    Credentials = CredentialsPKPem
                },
                new IAMAuthorizationProvider
                {
                    UseSessionToken = true,
                    ConfigFile = OCIConfigFile
                },
                new IAMAuthorizationProvider
                {
                    UseSessionToken = true,
                    ProfileName = "DEFAULT"
                },
                new IAMAuthorizationProvider(CredentialsPK)
                {
                    ConfigFile = OCIConfigFile
                },
                new IAMAuthorizationProvider(CredentialsPKPem)
                {
                    ProfileName = "DEFAULT"
                },
                new IAMAuthorizationProvider(CredentialsPKPem)
                {
                    CredentialsProvider =
                        cancellationToken => Task.FromResult(
                            CredentialsPKFile)
                },
                new IAMAuthorizationProvider("DEFAULT")
                {
                    CredentialsProvider =
                        cancellationToken => Task.FromResult(
                            CredentialsPKFile)
                },
                new IAMAuthorizationProvider(OCIConfigFile, "DEFAULT")
                {
                    CredentialsProvider =
                        cancellationToken => Task.FromResult(CredentialsPK)
                },
                new IAMAuthorizationProvider
                {
                    ConfigFile = OCIConfigFile,
                    CredentialsProvider =
                        cancellationToken => Task.FromResult(CredentialsPK)
                },
                // Cannot specify delegation token-related properties without
                // using instance principal.
                new IAMAuthorizationProvider(CredentialsPK)
                {
                    DelegationTokenProvider = cancellationToken =>
                        Task.FromResult(DelegationToken)
                },
                new IAMAuthorizationProvider
                {
                    UseResourcePrincipal = true,
                    DelegationTokenFile = DelegationTokenFile
                },
                // Delegation token, delegation token provider and delegation
                // token file are mutually exclusive.
                new IAMAuthorizationProvider
                {
                    DelegationTokenProvider = cancellationToken =>
                        Task.FromResult(DelegationToken),
                    DelegationTokenFile = DelegationTokenFile
                },
                new Func<IAMAuthorizationProvider>(() =>
                {
                    var provider =
                        IAMAuthorizationProvider
                            .CreateWithInstancePrincipalForDelegation(
                                DelegationToken);
                    provider.DelegationTokenProvider = cancellationToken =>
                        Task.FromResult(DelegationToken);
                    return provider;
                })(),
                new Func<IAMAuthorizationProvider>(() =>
                {
                    var provider =
                        IAMAuthorizationProvider
                            .CreateWithInstancePrincipalForDelegation(
                                DelegationToken);
                    provider.DelegationTokenFile = DelegationTokenFile;
                    return provider;
                })()
            };

        private static IEnumerable<IAMConfigInfo> BadIAMConfigsImmediate =>
            (from creds in BadIAMCredentialsImmediate
                select new IAMConfigInfo(
                    new IAMAuthorizationProvider(creds)))
            .Concat(
                from oci in BadOCIConfigsImmediate
                select new IAMConfigInfo(oci))
            .Concat(
                from provider in InvalidAuthProviders
                // InvalidAuthProviders use only default OCI config
                // properties. We use DefaultOCIConfig to make sure the
                // error we get is not due to missing/invalid OCI config.
                select new IAMConfigInfo(provider, DefaultOCIConfig));

        private static IEnumerable<object[]>
            BadIAMConfigsImmediateDataSource =>
            from iam in BadIAMConfigsImmediate
            select new object[] { iam };

        private static IEnumerable<IAMConfigInfo> BadIAMConfigsEventual =>
            (from creds in BadIAMCredentialsEventual
                select new IAMConfigInfo(
                    new IAMAuthorizationProvider(creds)))
            // Bad private key data in file
            .Concat(
                from pem in BadPrivateKeyPems
                select new IAMConfigInfo(
                    new IAMAuthorizationProvider(CredentialsPKFile), pem))
            // Missing passphrase for encrypted key in file
            .Append(new IAMConfigInfo(new IAMAuthorizationProvider(
                CredentialsPKFile), Keys.PrivatePKCS8EncryptedPEM))
            // Wrong passphrase for encrypted key in file
            .Append(new IAMConfigInfo(new IAMAuthorizationProvider(
                    CombineProperties(CredentialsPKFile,
                        new { Passphrase = "oracle1".ToCharArray() })),
                Keys.PrivatePKCS8EncryptedPEM))
            .Concat(
                from oci in BadOCIConfigsEventual
                select new IAMConfigInfo(oci))
            .Concat(
                from provider in BadCredentialsProviders
                select new IAMConfigInfo(
                    new IAMAuthorizationProvider(provider)))
            // Bad private key data in file with credentials provider.
            .Concat(
                from pem in BadPrivateKeyPems
                select new IAMConfigInfo(
                    new IAMAuthorizationProvider(cancellationToken =>
                        Task.FromResult(CredentialsPKFile)), pem))
            // Missing passphrase for encrypted key in file with
            // credentials provider.
            .Append(new IAMConfigInfo(new IAMAuthorizationProvider(
                    cancellationToken =>
                        Task.FromResult(CredentialsPKFile)),
                Keys.PrivatePKCS8EncryptedPEM))
            // Wrong passphrase for encrypted key in file with
            // credentials provider.
            .Append(new IAMConfigInfo(new IAMAuthorizationProvider(
                    cancellationToken => Task.FromResult(CombineProperties(
                        CredentialsPKFile,
                        new { Passphrase = "oracle1".ToCharArray() }))),
                Keys.PrivatePKCS8EncryptedPEM));

        private static IEnumerable<object[]>
            BadIAMConfigsEventualDataSource =>
            from iam in BadIAMConfigsEventual select new object[] { iam };

        [DataTestMethod]
        [DynamicData(nameof(BadIAMConfigsImmediateDataSource))]
        public void TestAuthProviderNegativeImmediate(IAMConfigInfo iam)
        {
            PrepareConfig(iam);

            var cfg = MakeNoSQLConfig(iam);
            // Needed because Uri is checked during ConfigureAuthorization().
            cfg.InitUri();

            AssertThrowsDerived<ArgumentException>(() =>
                iam.Provider.ConfigureAuthorization(cfg));
        }

        [DataTestMethod] [DynamicData(nameof(BadIAMConfigsEventualDataSource))]
        public async Task TestAuthProviderNegativeEventualAsync(
            IAMConfigInfo iam)
        {
            PrepareConfig(iam);

            var cfg = MakeNoSQLConfig(iam);
            // ConfigureAuthorization() will be called during NoSQLClient
            // constructor.
            var client = new NoSQLClient(cfg);

            // At this time it doesn't matter what subclass of Request we
            // provide.
            var request = new GetTableRequest(client, "table", null);
            var message = new HttpRequestMessage();

            await AssertThrowsDerivedAsync<ArgumentException>(() =>
                iam.Provider.ApplyAuthorizationAsync(request, message,
                    CancellationToken.None));
        }

    }

}
