/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK.Tests.IAM
{
    using System;
    using System.Security.Cryptography;
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using static TestData;
    using static Utils;

    [TestClass]
    public class SecurityTokenTests
    {
        [TestMethod]
        public void TestSecurityTokenValidatesMatchingPublicKey()
        {
            var token = SecurityToken.Create(CreateSecurityToken(Keys.RSA));

            token.ValidatePublicKey(Keys.RSA);
        }

        [TestMethod]
        public void TestSecurityTokenRejectsMismatchedPublicKey()
        {
            using var otherKey = RSA.Create(2048);
            var token = SecurityToken.Create(CreateSecurityToken(Keys.RSA));

            Assert.ThrowsException<ArgumentException>(() =>
                token.ValidatePublicKey(otherKey));
        }

        [TestMethod]
        public void TestSecurityTokenRejectsMissingJWK()
        {
            var token = SecurityToken.Create(CreateSecurityToken(Keys.RSA,
                includeJWK: false));

            Assert.ThrowsException<ArgumentException>(() =>
                token.ValidatePublicKey(Keys.RSA));
        }

        [TestMethod]
        public void TestSecurityTokenDetectsExpiredToken()
        {
            var token = SecurityToken.Create(CreateSecurityToken(Keys.RSA,
                TimeSpan.FromMinutes(-1)));

            Assert.IsFalse(token.IsValid());
        }
    }
}
