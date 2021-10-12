/*-
 * Copyright (c) 2020, 2021 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.Driver
{

    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography.X509Certificates;
    using static X509Utils;

    /// <summary>
    /// Represents options for network connections to Oracle NoSQL Database.
    /// </summary>
    /// <remarks>
    /// These options customize how the driver establishes network connections
    /// to Oracle NoSQL Database.  They include HTTP and secure
    /// transport-related options. Set these options as
    /// <see cref="NoSQLConfig.ConnectionOptions"/> in the configuration used
    /// to create <see cref="NoSQLClient"/> instance.
    /// </remarks>
    /// <example>
    /// Setting trusted root certificates.
    /// <code>
    /// var client = new NoSQLClient(new NoSQLConfig(
    /// {
    ///     Endpoint = "...",
    ///     AuthorizationProvider = new KVStoreAuthorizationProvider(...),
    ///     ConnectionOptions = new ConnectionOptions
    ///     {
    ///         TrustedRootCertificateFile = "path/to/trusted/certificate.pem"
    ///     }
    /// };
    /// </code>
    /// </example>
    /// <seealso cref="NoSQLConfig"/>
    public class ConnectionOptions
    {
        /// <summary>
        /// On-premise only.
        /// Gets or sets a collection of trusted root certificates used to
        /// validate server SSL certificate for connections to secure kvstore.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Set this property when your server certificate for the Oracle
        /// NoSQL database proxy is not issued by a trusted root certificate
        /// authority (CA), such as when the certificate is issued by a
        /// private CA or is a self-signed certificate.
        /// </para>
        /// <para>
        /// Alternatively, use <see cref="TrustedRootCertificateFile"/>
        /// property.  This property is exclusive with
        /// <see cref="TrustedRootCertificateFile"/> property.
        /// </para>
        /// </remarks>
        /// <value>
        /// The collection of trusted root certificates.  If not set, the
        /// validation of the server certificate is based on trusted root CAs
        /// in the system certificate store.
        /// </value>
        public X509Certificate2Collection TrustedRootCertificates { get; set; }

        /// <summary>
        /// On-premise only.
        /// Gets or sets a file path containing one or more trusted root
        /// certificates used to validate server SSL certificate for
        /// connections to secure kvstore.
        /// </summary>
        /// <remarks>
        /// <para>
        /// The file must contain one or more certificates in PEM format.
        /// </para>
        /// <para>
        /// Set this property when your server certificate for the Oracle
        /// NoSQL database proxy is not issued by a trusted root certificate
        /// authority (CA), such as when the certificate is issued by a
        /// private CA or is a self-signed certificate.
        /// </para>
        /// <para>
        /// Alternatively, use <see cref="TrustedRootCertificates"/> property.
        /// This property is exclusive with
        /// <see cref="TrustedRootCertificates"/> property.
        /// </para>
        /// </remarks>
        /// <value>
        /// The collection of trusted root certificates.  If not set, the
        /// validation of the server certificate is based on trusted root CAs
        /// in the system certificate store.
        /// </value>
        public string TrustedRootCertificateFile { get; set; }

        // TODO:
        // This class will include all other HTTP and HTTPS related options
        // such as proxy setting, connection pool settings, SSL-related
        // settings, etc.

        internal void Validate()
        {
            if (TrustedRootCertificates != null &&
                TrustedRootCertificateFile != null)
            {
                throw new ArgumentException(
                    $"Cannot specify {nameof(TrustedRootCertificateFile)} " +
                    "property together with " +
                    $"{nameof(TrustedRootCertificates)} property");
            }
        }

        internal void Init()
        {
            if (TrustedRootCertificateFile != null)
            {
                try
                {
                    TrustedRootCertificates = GetCertificatesFromPEM(
                        File.ReadAllText(TrustedRootCertificateFile));
                }
                catch (Exception ex)
                {
                    throw new ArgumentException(
                        "Error reading trusted certificates from file " +
                        $"{TrustedRootCertificateFile}: {ex.Message}", ex);
                }
            }
        }

        internal void ReleaseResources()
        {
            // Dispose only if we own the certificate collection
            if (TrustedRootCertificateFile != null)
            {
                Debug.Assert(TrustedRootCertificates != null);
                DisposeCertificates(TrustedRootCertificates);
            }
        }

    }
}
