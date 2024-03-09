/*-
 * Copyright (c) 2020, 2024 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Security.Cryptography;
    using System.Threading;
    using System.Threading.Tasks;

    internal static partial class PemPrivateKeyUtils
    {
        private const string PKCS8PEMPrefix = "-----BEGIN PRIVATE KEY-----";
        private const string PKCS8EncPEMPrefix =
            "-----BEGIN ENCRYPTED PRIVATE KEY-----";
        private const string PKCS1PEMPrefix =
            "-----BEGIN RSA PRIVATE KEY-----";
        private const string PKCS8PEMPostfix = "-----END PRIVATE KEY-----";
        private const string PKCS8EncPEMPostfix =
            "-----END ENCRYPTED PRIVATE KEY-----";
        private const string PKCS1PEMPostfix =
            "-----END RSA PRIVATE KEY-----";

        private const string InvalidPEMError = "Invalid PEM private key: ";

        private ref struct PEMResult
        {
            internal string Data { get; set; }
            internal bool IsPKCS1 { get; set; }
            internal bool IsEncrypted { get; set; }
            internal string EncryptAlg { get; set; }
            internal string EncryptParams { get; set; }
        }

        private static void ParsePEM(string[] pemLines,
            out PEMResult result)
        {
            var startIndex = -1;
            var endIndex = -1;
            var checkHeaders = false;
            Dictionary<string, string> headers = null;

            result = new PEMResult();

            for(var i = 0; i < pemLines.Length; i++)
            {
                var line = pemLines[i].Trim();
                switch (line)
                {
                    case PKCS8PEMPrefix:
                        startIndex = i + 1;
                        break;
                    case PKCS8EncPEMPrefix:
                        startIndex = i + 1;
                        result.IsEncrypted = true;
                        break;
                    case PKCS1PEMPrefix:
                        result.IsPKCS1 = true;
                        checkHeaders = true;
                        break;
                    case PKCS8PEMPostfix:
                        if (startIndex == -1 || result.IsPKCS1 ||
                            result.IsEncrypted)
                        {
                            throw new ArgumentException(
                                InvalidPEMError +
                                $"Ends with {PKCS8PEMPostfix} but does not " +
                                $"start with {PKCS8PEMPrefix}");
                        }

                        endIndex = i;
                        break;
                    case PKCS8EncPEMPostfix:
                        if (startIndex == -1 || result.IsPKCS1 ||
                            !result.IsEncrypted)
                        {
                            throw new ArgumentException(
                                InvalidPEMError +
                                $"Ends with {PKCS8EncPEMPostfix} but does not " +
                                $"end with {PKCS8EncPEMPrefix}");
                        }

                        endIndex = i;
                        break;
                    case PKCS1PEMPostfix:
                        if (startIndex == -1 || !result.IsPKCS1)
                        {
                            throw new ArgumentException(
                                InvalidPEMError +
                                $"Ends with {PKCS1PEMPostfix} but does not " +
                                $"end with {PKCS1PEMPrefix}");
                        }

                        endIndex = i;
                        break;
                    default:
                        if (checkHeaders)
                        {
                            var idx = line.IndexOf(':');
                            if (idx == -1)
                            {
                                checkHeaders = false;
                                startIndex = i;
                            }
                            else
                            {
                                headers ??= new Dictionary<string, string>();
                                var key = line.Substring(0, idx);
                                var val = line.Substring(idx + 2).TrimStart();
                                headers.Add(key, val);
                            }
                        }

                        break;
                }
            }

            if (startIndex == -1 || endIndex == -1)
            {
                throw new ArgumentException(
                    InvalidPEMError +
                    "Missing -----" +
                    (startIndex == -1 ? "BEGIN prefix " : "END postfix ") +
                    "line");
            }

            if (headers != null)
            {
                ProcessPEMHeaders(headers, ref result);
            }

            result.Data = string.Join("", pemLines, startIndex,
                endIndex - startIndex);
        }

        private static RSA GetFromPEM(string[] pemLines, char[] passphrase)
        {
            ParsePEM(pemLines, out var result);
            var keyBytes = Convert.FromBase64String(result.Data);

            try
            {
                if (result.EncryptAlg != null)
                {
                    keyBytes = DecryptPKCS1(keyBytes, passphrase,
                        result.EncryptAlg, result.EncryptParams);
                }

                var rsa = RSA.Create();

                if (result.IsPKCS1)
                {
                    rsa.ImportRSAPrivateKey(keyBytes, out _);
                }
                else
                {
                    if (result.IsEncrypted)
                    {
                        rsa.ImportEncryptedPkcs8PrivateKey(passphrase,
                            keyBytes, out _);
                    }
                    else
                    {
                        rsa.ImportPkcs8PrivateKey(keyBytes, out _);
                    }
                }

                return rsa;
            }
            finally
            {
                Array.Clear(keyBytes, 0, keyBytes.Length);
            }
        }

        internal static RSA GetFromString(string pem,
            char[] passphrase = null)
        {
            try
            {
                return GetFromPEM(
                    pem.Split(new[] { "\r\n", "\r", "\n" },
                        StringSplitOptions.None), passphrase);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "Failed to create private key from PEM string: " +
                    ex.Message, ex);
            }
        }

        internal static async Task<RSA> GetFromFileAsync(
            string pemFile, char[] passphrase,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return GetFromPEM(
                    await File.ReadAllLinesAsync(pemFile, cancellationToken),
                    passphrase);
            }
            catch (Exception ex)
            {
                throw new ArgumentException(
                    "Failed to create private key from PEM file " +
                    $"{pemFile}: {ex.Message}", ex);
            }
        }

    }

}
