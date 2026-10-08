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

    [TestClass]
    public class CreationTimeCapabilityTests
    {
        [DataTestMethod]
        [DataRow("25.3.21", false)]
        [DataRow("26.0.99", false)]
        [DataRow("26.1", true)]
        [DataRow("26.1.14", true)]
        [DataRow("26.3.6", true)]
        [DataRow(null, true)]
        public void TestCreationTimeSupportBoundary(string version,
            bool supported)
        {
            var parsed = version == null ? null : Version.Parse(version);
            Assert.AreEqual(supported,
                CreationTimeTestCapabilities.IsSupported(parsed));
            if (supported)
            {
                CreationTimeTestCapabilities.CheckSupported(parsed);
            }
            else
            {
                Assert.ThrowsException<AssertInconclusiveException>(() =>
                    CreationTimeTestCapabilities.CheckSupported(parsed));
            }
        }
    }
}
