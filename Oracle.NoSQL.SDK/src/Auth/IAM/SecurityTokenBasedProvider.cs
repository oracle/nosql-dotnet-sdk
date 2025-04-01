/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{
    using System;
    using System.Diagnostics;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    internal abstract class SecurityTokenBasedProvider :
        AuthenticationProfileProvider
    {
        private readonly TimeSpan expireBefore;
        private protected SecurityToken securityToken;

        internal SecurityTokenBasedProvider(TimeSpan expireBefore)
        {
            this.expireBefore = expireBefore;
        }

        private protected abstract Task<string> RefreshSecurityTokenAsync(
            CancellationToken cancellationToken);

        private protected abstract RSA PrivateKey { get; }

        private protected virtual SecurityToken
            CreateSecurityToken(string val) => SecurityToken.Create(val);

        internal override bool IsProfileValid =>
            securityToken?.IsValid(expireBefore) ?? false;

        internal override TimeSpan ProfileTTL =>
            securityToken != null
            ? securityToken.ExpirationTime - expireBefore - DateTime.UtcNow
            : TimeSpan.Zero;

        internal override async Task<AuthenticationProfile> GetProfileAsync(
            bool forceRefresh, CancellationToken cancellationToken)
        {
            if (forceRefresh || !IsProfileValid)
            {
                securityToken = CreateSecurityToken(
                    await RefreshSecurityTokenAsync(cancellationToken));
            }

            return new AuthenticationProfile("ST$" + securityToken.Value,
                PrivateKey);
        }

    }
}
