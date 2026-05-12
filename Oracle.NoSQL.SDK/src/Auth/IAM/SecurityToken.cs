/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;
    using System.Security.Cryptography;
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

            if (claims.TryGetProperty("jwk", out var jwkProp))
            {
                InitJWK(jwkProp);
            }
        }

        private static byte[] Base64UrlDecodeBytes(string value)
        {
            var asBase64 = value.Replace('_', '/').Replace('-', '+');
            if ((value.Length & 3) != 0)
            {
                asBase64 = asBase64.PadRight(
                    ((asBase64.Length >> 2) + 1) << 2, '=');
            }

            return Convert.FromBase64String(asBase64);
        }

        private static RSAParameters GetJWKParameters(JsonElement jwk)
        {
            if (!jwk.TryGetProperty("n", out var modulusProp) ||
                modulusProp.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    "Invalid JWK in JWT, missing modulus");
            }

            if (!jwk.TryGetProperty("e", out var exponentProp) ||
                exponentProp.ValueKind != JsonValueKind.String)
            {
                throw new InvalidOperationException(
                    "Invalid JWK in JWT, missing exponent");
            }

            return new RSAParameters
            {
                Modulus = Base64UrlDecodeBytes(modulusProp.GetString()),
                Exponent = Base64UrlDecodeBytes(exponentProp.GetString())
            };
        }

        private void InitJWK(JsonElement jwkProp)
        {
            switch (jwkProp.ValueKind)
            {
                case JsonValueKind.String:
                    using (var jwk = JsonDocument.Parse(
                        jwkProp.GetString()))
                    {
                        PublicKey = GetJWKParameters(jwk.RootElement);
                    }
                    break;
                case JsonValueKind.Object:
                    PublicKey = GetJWKParameters(jwkProp);
                    break;
                default:
                    throw new InvalidOperationException(
                        "Invalid JWK claim value in JWT: " +
                        jwkProp.GetRawText());
            }
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

        private RSAParameters? PublicKey { get; set; }

        internal void ValidatePublicKey(RSA expectedKey)
        {
            if (expectedKey == null)
            {
                throw new ArgumentNullException(nameof(expectedKey));
            }

            if (PublicKey == null)
            {
                throw new ArgumentException(
                    "Security token is missing JWK public key");
            }

            var expected = expectedKey.ExportParameters(false);
            var actual = PublicKey.Value;
            if (!CryptographicOperations.FixedTimeEquals(
                    actual.Modulus, expected.Modulus) ||
                !CryptographicOperations.FixedTimeEquals(
                    actual.Exponent, expected.Exponent))
            {
                throw new ArgumentException(
                    "Security token JWK public key does not match " +
                    "the configured private key");
            }
        }
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
