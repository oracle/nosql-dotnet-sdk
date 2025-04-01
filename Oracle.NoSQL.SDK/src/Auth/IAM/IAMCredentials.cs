/*-
 * Copyright (c) 2020, 2025 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK
{

    using System;
    using System.Security.Cryptography;
    using static Utils;

    /// <summary>
    /// Represents the credentials used by
    /// <see cref="IAMAuthorizationProvider"/> when authenticating using the
    /// specific user identity.
    /// </summary>
    /// <remarks>
    /// See
    /// <see href = "https://docs.cloud.oracle.com/iaas/Content/API/Concepts/apisigningkey.htm">
    /// Required Keys and OCIDs
    /// </see>
    /// on how to obtained the required credentials.  For private key, supply
    /// only one of the properties <see cref="PrivateKey"/>,
    /// <see cref="PrivateKeyPEM"/> or <see cref="PrivateKeyFile"/>.  Note
    /// that if the private key is encrypted, it must be in PKCS#8 format.
    /// See <see cref="IAMAuthorizationProvider"/> for details.
    /// </remarks>
    /// <seealso cref="IAMAuthorizationProvider"/>
    public class IAMCredentials
    {
        /// <summary>
        /// Gets or sets the tenancy OCID.
        /// </summary>
        /// <value>
        /// Tenancy OCID.
        /// </value>
        public string TenantId { get; set; }

        /// <summary>
        /// Gets or sets the user OCID.
        /// </summary>
        /// <value>
        /// User OCID.
        /// </value>
        public string UserId { get; set; }

        /// <summary>
        /// Gets or sets the public key fingerprint.
        /// </summary>
        /// <value>
        /// Public key fingerprint.
        /// </value>
        public string Fingerprint { get; set; }

        /// <summary>
        /// Gets or sets the private key as an <see cref="RSA"/> object.
        /// </summary>
        /// <remarks>
        /// This property is for advanced usage.  Most applications should use
        /// <see cref="PrivateKeyPEM"/> or <see cref="PrivateKeyFile"/>.  Note
        /// that the driver will not call <see cref="IDisposable.Dispose"/> on
        /// the <see cref="RSA"/> object.  The application should dispose of
        /// the <see cref="RSA"/> object after the <see cref="NoSQLClient"/>
        /// instance is disposed.
        /// </remarks>
        /// <value>
        /// The value of the private key as loaded into an <see cref="RSA"/>
        /// algorithm object.  This property is exclusive with
        /// <see cref="PrivateKeyPEM"/> and <see cref="PrivateKeyFile"/>.
        /// </value>
        public RSA PrivateKey { get; set; }

        /// <summary>
        /// Gets or sets the private key in PEM format.
        /// </summary>
        /// <value>
        /// Private key in PEM format.  This property is exclusive with
        /// <see cref="PrivateKey"/> and <see cref="PrivateKeyFile"/>.
        /// </value>
        public string PrivateKeyPEM { get; set; }

        /// <summary>
        /// Gets or sets the path to the PEM private key file.
        /// </summary>
        /// <value>
        /// The path (absolute or relative) to the file containing the private
        /// key in PEM format.  This property is exclusive with
        /// <see cref="PrivateKey"/> and <see cref="PrivateKeyPEM"/>.
        /// </value>
        public string PrivateKeyFile { get; set; }

        //It seems that using SecureString is discouraged by Microsoft

        /// <summary>
        /// Gets or sets the private key passphrase if private key is
        /// encrypted.
        /// </summary>
        /// <remarks>
        /// For added security, the application may erase the passphrase from
        /// memory after <see cref="NoSQLClient"/> instance is disposed of.
        /// This erase is done by the driver for credentials returned as the
        /// result of
        /// <see cref="IAMAuthorizationProvider.CredentialsProvider"/>
        /// delegate.
        /// </remarks>
        /// <value>
        /// The private key passphrase if the private key is encrypted,
        /// otherwise <c>null</c>.
        /// </value>
        public char[] Passphrase { get; set; }

        internal void Validate()
        {
            if (TenantId == null)
            {
                throw new ArgumentNullException(nameof(TenantId),
                    "Tenant id may not be null");
            }

            if (!IsValidOCID(TenantId))
            {
                throw new ArgumentException(
                    $"Tenant id is not a valid OCID: {TenantId}",
                    nameof(TenantId));
            }

            if (UserId == null)
            {
                throw new ArgumentNullException(nameof(UserId),
                    "User id may not be null");
            }

            if (!IsValidOCID(UserId))
            {
                throw new ArgumentException(
                    $"User id is not a valid OCID: {UserId}",
                    nameof(TenantId));
            }

            if (string.IsNullOrEmpty(Fingerprint))
            {
                throw new ArgumentException(
                    "Fingerprint cannot be null or empty");
            }

            if (PrivateKey == null && PrivateKeyFile == null &&
                PrivateKeyPEM == null)
            {
                throw new ArgumentException(
                    "Must specify one of PrivateKey, PrivateKeyFile or " +
                    "PrivateKeyPEM");
            }

            if (PrivateKeyFile != null &&
                (PrivateKey != null || PrivateKeyPEM != null) ||
                PrivateKey != null && PrivateKeyPEM != null)
            {
                throw new ArgumentException(
                    "Only one of PrivateKey, PrivateKeyFile or " +
                    "PrivateKeyPEM may be specified");
            }

            if (Passphrase != null && Passphrase.Length == 0)
            {
                throw new ArgumentException("Passphrase cannot be empty");
            }
        }

    }

}
