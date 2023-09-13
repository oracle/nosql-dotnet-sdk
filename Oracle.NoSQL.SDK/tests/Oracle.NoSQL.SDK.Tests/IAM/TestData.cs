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
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Security.Cryptography;
    using System.Text;
    using static Tests.Utils;

    internal static class TestData
    {
        private static TempFileCache TempFileCache { get; } =
            new TempFileCache();

        internal const string CompartmentId = "ocid1.user.oc1..compartment";
        
        internal const string TenantId = "ocid1.tenancy.oc1..tenancy";
        internal const string UserId = "ocid1.user.oc1..user";
        internal const string Fingerprint = "fingerprint";
        
        internal const string SessionToken =
            "token-header.token-payload.token-sig";

        internal const string DelegationToken =
            "token-header.token-payload.token-sig";

        internal const string CompartmentIdHeader = "x-nosql-compartment-id";
        internal const string OBOTokenHeader = "opc-obo-token";
        internal const string DateHeader = "date";
        internal const string HostHeader = "host";
        internal const string RequestTargetHeader = "(request-target)";
        internal const string NoSQLDataPath = "V2/nosql/data";

        internal static readonly Region TestRegion = Region.US_ASHBURN_1;

        internal static readonly string TestRegionHost =
            new Uri(TestRegion.Endpoint).Host;

        internal static readonly string[] DefaultProfileStart  =
            { "[DEFAULT]" };

        internal static readonly string PrivateKeyFile =
            TempFileCache.GetTempFile();

        internal static readonly string SessionTokenFile =
            TempFileCache.GetTempFile();

        internal static readonly string OCIConfigFile =
            TempFileCache.GetTempFile();

        internal static readonly string DelegationTokenFile =
            TempFileCache.GetTempFile();

        internal static readonly RSAKeys Keys = new RSAKeys();

        internal static readonly IAMCredentials CredentialsBase =
            new IAMCredentials
            {
                TenantId = TenantId,
                UserId = UserId,
                Fingerprint = Fingerprint
            };

        internal static readonly string CredentialsKeyId =
            $"{TenantId}/{UserId}/{Fingerprint}";

        internal static readonly IAMCredentials CredentialsPK =
            CombineProperties(CredentialsBase, new { PrivateKey = Keys.RSA });

        internal static readonly IAMCredentials CredentialsPKPem =
            CombineProperties(CredentialsBase,
                new { PrivateKeyPEM = Keys.PrivatePKCS8PEM });

        internal static readonly IAMCredentials CredentialsPKFile =
            CombineProperties(CredentialsBase, new { PrivateKeyFile });

        internal static readonly IAMCredentials CredentialsPKEncPem =
            CombineProperties(CredentialsBase, new
            {
                PrivateKeyPEM = Keys.PrivatePKCS8EncryptedPEM,
                Passphrase = RSAKeys.Passphrase.ToCharArray()
            });

        internal static readonly IAMCredentials CredentialsPKEncFile =
            CombineProperties(CredentialsBase, new
            {
                PrivateKeyFile,
                Passphrase = RSAKeys.Passphrase.ToCharArray()
            });

        internal static readonly IEnumerable<string> OCIConfigLines =
            new[]
        {
            $"tenancy={TenantId}",
            $"user={UserId}",
            $"fingerprint={Fingerprint}",
            $"key_file={PrivateKeyFile}"
        };

        internal static readonly IEnumerable<string> OCIConfigLinesSessToken =
            new []
        {
            $"tenancy={TenantId}",
            $"security_token_file={SessionTokenFile}",
            $"key_file={PrivateKeyFile}"
        };

        internal static readonly IEnumerable<string> OCIConfigLinesEncKey =
            OCIConfigLines.Append($"pass_phrase={RSAKeys.Passphrase}");

        internal static readonly IEnumerable<string>
            OCIConfigLinesEncKeySessToken = OCIConfigLinesSessToken.Append(
                $"pass_phrase={RSAKeys.Passphrase}");

        internal static void RemoveTempFiles()
        {
            TempFileCache.Clear();
        }

    }

}
