/*-
 * Copyright (c) 2020, 2022 Oracle and/or its affiliates. All rights reserved.
 *
 * Licensed under the Universal Permissive License v 1.0 as shown at
 *  https://oss.oracle.com/licenses/upl/
 */

namespace Oracle.NoSQL.SDK {

    using System;
    using System.Collections.Generic;
    using System.Diagnostics;
    using System.IO;
    using System.Security.Cryptography;
    using System.Text;

    internal static partial class PemPrivateKeyUtils
    {
        private const string ProcTypeHeader = "Proc-Type";
        private const string DekInfoHeader = "DEK-Info";
        private const string EncProcType = "4,ENCRYPTED";
        private const int PKCS5SaltLength = 8;

        private static void ProcessPEMHeaders(
            Dictionary<string,string> headers,
            ref PEMResult result)
        {
            // We are interested in getting private key encryption info if
            // using PKCS1 encrypted private key.  It should look like the
            // following:
            // -----BEGIN RSA PRIVATE KEY-----
            // Proc-Type: 4,ENCRYPTED
            // DEK-Info: AES-128-CBC,3F17F5316E2BAC89
            //
            // ...base64 encoded data...
            // -----END RSA PRIVATE KEY----

            // The value of DEK-Info header is comma-separated encryption
            // algorithm name and initialization vector.

            if (!headers.TryGetValue(ProcTypeHeader, out var procType) ||
                procType.TrimEnd() != EncProcType ||
                !headers.TryGetValue(DekInfoHeader, out var dekInfo))
            {
                return;
            }

            result.IsEncrypted = true;
            var idx = dekInfo.IndexOf(',');
            if (idx != -1)
            {
                result.EncryptAlg = dekInfo.Substring(0, idx);
                result.EncryptParams = dekInfo.Substring(idx + 1).TrimEnd();
            }
            else
            {
                result.EncryptAlg = dekInfo;
            }
        }

        private static byte[] FromHexString(string s)
        {
#if NET5_0_OR_GREATER
            return Convert.FromHexString(s);
#else
            
            var bytes = new byte[s.Length >> 1];
            for (var i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(s.Substring(i << 1, 2), 16);
            }

            return bytes;
#endif
        }

        // Only PKCS5_SALT_LEN bytes of iv are used for salt, see
        // PEM_read_bio_PrivateKey().
        private static byte[] GetSaltFromIV(byte[] iv)
        {
            var salt = iv;
            if (iv.Length > PKCS5SaltLength)
            {
                salt = new byte[PKCS5SaltLength];
                Array.Copy(iv, salt, PKCS5SaltLength);
            }

            return salt;
        }

        // Equivalent to OpenSSL EVP_BytesToKey() with iteration count = 1.
        private static byte[] DeriveKeyBytes(int keySize, byte[] pwd,
            byte[] salt)
        {
            keySize >>= 3; // convert key size from bits to bytes
            using var digest = MD5.Create();

            var key = new byte[keySize];
            var offset = 0;
        
            for (;;)
            {
                digest.TransformBlock(pwd, 0, pwd.Length, null, 0);
                digest.TransformFinalBlock(salt, 0, salt.Length);
                var hash = digest.Hash;
                Debug.Assert(hash != null);

                var len = Math.Min(hash.Length, keySize - offset);
                Array.Copy(hash, 0, key, offset, len);

                offset += len;
                if (offset == keySize)
                {
                    break;
                }

                // If not enough bytes for the key, continue iterating, see
                // D_i = HASH^count(D_(i-1) || data || salt)
                // in EVP_BytesToKey().
                digest.Initialize();
                digest.TransformBlock(hash, 0, hash.Length, null, 0);
            }

            return key;
        }

        private static SymmetricAlgorithm CreateDekAlgorithm(string dekName)
        {
            // E.g. dekName = "AES-256-CBC"
            dekName = dekName.Trim();

            // We only support AES, since other algorithms are either too old or
            // or require 3rd party library support (e.g. Camellia, Aria, etc.).
            if (!dekName.StartsWith("AES",
                StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException(
                    "Unsupported DEK algorithm name, only AES 128/192/256 " +
                    "are supported ");
            }

            var mode = (CipherMode)(-1);
            var i = dekName.LastIndexOf('-');
            if (i != -1)
            {
                var modeName = dekName.Substring(i + 1);
                if (!Enum.TryParse(modeName, out mode))
                {
                    throw new ArgumentException(
                        "Unsupported cipher mode value for DEK algorithm");
                }

                dekName = dekName.Substring(0, i);
            }

            var keySize = -1;
            // This will also support names like "AES_256" and "AES256" in
            // addition to "AES-256".
            dekName = dekName.Replace("-", "").Replace("_", "");
            if (dekName.Length > 3)
            {
                if (!int.TryParse(dekName.Substring(3), out keySize))
                {
                    throw new ArgumentException(
                        "Invalid key size value for DEK algorithm");
                }
            }

            var alg = Aes.Create();

            // If mode or key size is not specified, will use default ones for
            // the algorithm.
            if (mode != (CipherMode)(-1))
            {
                alg.Mode = mode;
            }

            if (keySize != -1)
            {
                alg.KeySize = keySize;
            }

            alg.Padding =
                alg.Mode == CipherMode.CBC || alg.Mode == CipherMode.ECB
                    ? PaddingMode.PKCS7
                    : PaddingMode.None;

            return alg;
        }

        private static void DecryptData(SymmetricAlgorithm alg, byte[] key,
            byte[] iv, byte[] data, Stream output)
        {
            using var decryptor = alg.CreateDecryptor(key, iv);
            using var cryptoStream = new CryptoStream(output, decryptor,
                CryptoStreamMode.Write);
            cryptoStream.Write(data);
            cryptoStream.Flush();
        }

        // See OpenSSL PEM_read_bio_PrivateKey(), PEM encryption format.
        private static byte[] DecryptPKCS1(byte[] privateKeyBytes,
            char[] passphrase, string algName, string iv)
        {
            var pwdBytes = Encoding.UTF8.GetBytes(passphrase);
            byte[] keyBytes = null;
            var output = new MemoryStream();
            using var alg = CreateDekAlgorithm(algName);
            Debug.Assert(alg != null);
            try
            {
                var ivBytes = FromHexString(iv);
                keyBytes = DeriveKeyBytes(alg.KeySize, pwdBytes,
                    GetSaltFromIV(ivBytes));
                DecryptData(alg, keyBytes, ivBytes, privateKeyBytes,
                    output);
                return output.ToArray();
            }
            finally
            {
                Array.Clear(pwdBytes, 0, pwdBytes.Length);
                if (keyBytes != null)
                {
                    Array.Clear(keyBytes, 0, keyBytes.Length);
                }

                var buf = output.GetBuffer();
                Array.Clear(buf, 0, buf.Length);
            }
        }

    }

}
