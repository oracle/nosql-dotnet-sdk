/*-
 * Copyright (c) 2026 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 * https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests
{
    using System;
    using Microsoft.VisualStudio.TestTools.UnitTesting;

    internal static class QueryTestCapabilities
    {
        // UNION is a KV 26.1 / QUERY_VERSION_19 (V39) feature. An upgraded
        // store must also have the corresponding SCV enabled. Do not swallow
        // preparation failures on an ostensibly supported server.
        private static readonly Version UnionVersion = new Version(26, 1);

        internal static void CheckUnionSupported(ServiceType serviceType,
            Version kvVersion)
        {
            if (serviceType != ServiceType.KVStore)
            {
                Assert.Inconclusive("UNION tests require on-prem kvstore");
            }
            // Like the existing protocol guards, an omitted kvVersion means
            // the test environment is expected to support current features.
            if (kvVersion != null && kvVersion < UnionVersion)
            {
                Assert.Inconclusive("UNION tests require KV 26.1 or later; " +
                    $"configured kvVersion is {kvVersion}");
            }
        }

        internal static void CheckUnionQueryVersion(short queryVersion)
        {
            if (queryVersion < QueryRequestBase.QueryV6)
            {
                Assert.Inconclusive("UNION tests require proxy query protocol " +
                    $"V6; negotiated V{queryVersion}");
            }
        }
    }
}
