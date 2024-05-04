/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;
    using System.Text.Json;
    using static Utils;
    using static DateTimeUtils;

    internal class SecurityToken
    {
        private protected SecurityToken(string value)
        {
            Value = value;
        }

        private protected virtual void InitClaims(JsonElement claims)
        {
            if (!claims.TryGetProperty("exp", out var expProp))
            {
                return;
            }

            long exp = 0;
            switch (expProp.ValueKind)
            {
                case JsonValueKind.String:
                    long.TryParse(expProp.GetString(), out exp);
                    break;
                case JsonValueKind.Number:
                    expProp.TryGetInt64(out exp);
                    break;
            }

            if (exp == 0)
            {
                throw new InvalidOperationException(
                    "Invalid expiration time claim value in JWT: " +
                    expProp.GetRawText());
            }

            ExpirationTime = UnixMillisToDateTime(exp * 1000);
        }

        private protected void Init(string tokenName)
        {
            if (string.IsNullOrEmpty(Value))
            {
                throw new ArgumentException(
                    $"{tokenName} is missing or invalid");
            }

            var parts = Value.Split('.');
            if (parts.Length < 3)
            {
                throw new ArgumentException(
                    $"Invalid {tokenName}, number of parts is " +
                    $"{parts.Length}, should be >= 3");
            }

            try
            {
                using var claims = JsonDocument.Parse(
                    Base64UrlDecode(parts[1]));
                InitClaims(claims.RootElement);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    $"Error parsing {tokenName}: {ex.Message}", ex);
            }
        }

        // tokenName parameter is for error reporting purpose
        internal static SecurityToken Create(string value,
            string tokenName = "security token")
        {
            var token = new SecurityToken(value);
            token.Init(tokenName);
            return token;
        }

        internal string Value { get; }

        internal DateTime ExpirationTime { get; private set; } =
            DateTime.MinValue;

        internal bool IsValid(TimeSpan expireBefore = default) =>
            ExpirationTime - expireBefore > DateTime.UtcNow;
    }

    internal class ResourcePrincipalSecurityToken : SecurityToken
    {
        private protected ResourcePrincipalSecurityToken(string value) :
            base(value)
        {
        }

        private protected override void InitClaims(JsonElement claims)
        {
            base.InitClaims(claims);

            if (claims.TryGetProperty("res_tenant", out var prop) &&
                prop.ValueKind == JsonValueKind.String)
            {
                ResourceTenantId = prop.GetString();
            }

            if (claims.TryGetProperty("res_compartment", out prop) &&
                prop.ValueKind == JsonValueKind.String)
            {
                ResourceCompartmentId = prop.GetString();
            }
        }

        internal string ResourceTenantId { get; private set; }

        internal string ResourceCompartmentId { get; private set; }

        internal static ResourcePrincipalSecurityToken Create(string value)
        {
            var token = new ResourcePrincipalSecurityToken(value);
            token.Init("Resource Principal security token");
            return token;
        }

    }

}
