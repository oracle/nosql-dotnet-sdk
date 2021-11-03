/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
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
    using System.Reflection;

    internal static class Utils
    {
        internal static string[] GetOCIConfigLines(string profileName,
            IAMCredentials credentials, string region = null) => new string[]
        {
            $"[{profileName}]",
            $"tenancy={credentials.TenantId}",
            $"user={credentials.UserId}",
            $"fingerprint={credentials.Fingerprint}",
            $"key_file={credentials.PrivateKeyFile}",
            $"pass_phrase={credentials.Passphrase}",
            region != null ? $"region={region}" : ""
        };

    }

}
