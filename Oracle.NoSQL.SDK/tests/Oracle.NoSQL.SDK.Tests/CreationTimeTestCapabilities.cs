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

    internal static class CreationTimeTestCapabilities
    {
        // Row.getCreationTime and the KV 26.1 release notes require SCV 26.1.
        // 25.3 has wire fields, but creation-time generation is disabled.
        // As with the protocol guards, an omitted version expects a current,
        // fully enabled service. Do not silently skip a misconfigured 26.1+.
        internal static bool IsSupported(Version version) =>
            version == null || version >= new Version(26, 1);

        internal static void CheckSupported(Version version)
        {
            if (!IsSupported(version))
            {
                Assert.Inconclusive("Creation-time generation requires " +
                    "KV 26.1 or later with SCV 26.1 enabled; " +
                    $"configured kvVersion is {version}");
            }
        }
    }
}
