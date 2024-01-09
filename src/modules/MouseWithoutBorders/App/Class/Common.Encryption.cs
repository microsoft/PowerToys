// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// <summary>
//     Encrypt/decrypt implementation.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace MouseWithoutBorders
{
    internal partial class Common
    {
#pragma warning disable SYSLIB0021
        private static AesCryptoServiceProvider symAl;
#pragma warning restore SYSLIB0021
        private static string myKey;
        private static uint magicNumber;
        private static Random ran = new(); // Used for non encryption related functionality.
        internal const int SymAlBlockSize = 16;

        /// <summary>
        /// This is used for the first encryption block, the following blocks will be combined with the cipher text of the previous block.
        /// Thus identical blocks in the socket stream would be encrypted to different cipher text blocks.
        /// The first block is a handshake one containing random data.
        /// Related Unit Test: TestEncryptDecrypt
        /// </summary>
        internal static readonly string InitialIV = ulong.MaxValue.ToString(CultureInfo.InvariantCulture);

        internal static Random Ran
        {
            get => Common.ran ??= new Random();
            set => Common.ran = value;
        }

        internal static uint MagicNumber
        {
            get => Common.magicNumber;
            set => Common.magicNumber = value;
        }

        internal static string MyKey
        {
            get => Common.myKey;

            set
            {
                if (Common.myKey != value)
                {
                    Common.myKey = value;
                    _ = Task.Factory.StartNew(
                        () => Common.GenLegalKey(),
                        System.Threading.CancellationToken.None,
                        TaskCreationOptions.None,
                        TaskScheduler.Default); // Cache the key to improve UX.
                }
            }
        }

        internal static string KeyDisplayedText(string key)
        {
            string displayedValue = string.Empty;
            int i = 0;

            do
            {
                int length = Math.Min(4, key.Length - i);
                displayedValue += string.Concat(key.AsSpan(i, length), "  ");
                i += 4;
            }
            while (i < key.Length - 1);

            return displayedValue.Trim();
        }

        internal static bool GeneratedKey { get; set; }

        internal static bool KeyCorrupted { get; set; }

        internal static void InitEncryption()
        {
            try
            {
                if (symAl == null)
                {
#pragma warning disable SYSLIB0021 // No proper replacement for now
                    symAl = new AesCryptoServiceProvider();
#pragma warning restore SYSLIB0021
                    symAl.KeySize = 256;
                    symAl.BlockSize = SymAlBlockSize * 8;
                    symAl.Padding = PaddingMode.Zeros;
                    symAl.Mode = CipherMode.CBC;
                    symAl.GenerateIV();
                }
            }
            catch (Exception e)
            {
                Log(e);
            }
        }

        private static readonly ConcurrentDictionary<string, byte[]> LegalKeyDictionary = new(StringComparer.OrdinalIgnoreCase);

        internal static byte[] GenLegalKey()
        {
            byte[] rv;
            string myKey = Common.MyKey;

            if (!LegalKeyDictionary.TryGetValue(myKey, out byte[] value))
            {
                Rfc2898DeriveBytes key = new(
                    myKey,
                    Common.GetBytesU(InitialIV),
                    50000,
                    HashAlgorithmName.SHA512);
                rv = key.GetBytes(32);
                _ = LegalKeyDictionary.AddOrUpdate(myKey, rv, (k, v) => rv);
            }
            else
            {
                rv = value;
            }

            return rv;
        }

        private static byte[] GenLegalIV()
        {
            string st = InitialIV;
            int ivLength = symAl.IV.Length;
            if (st.Length > ivLength)
            {
                st = st[..ivLength];
            }
            else if (st.Length < ivLength)
            {
                st = st.PadRight(ivLength, ' ');
            }

            return GetBytes(st);
        }

        internal static Stream GetEncryptedStream(Stream encryptedStream)
        {
            ICryptoTransform encryptor;
            encryptor = symAl.CreateEncryptor(GenLegalKey(), GenLegalIV());
            return new CryptoStream(encryptedStream, encryptor, CryptoStreamMode.Write);
        }

        internal static Stream GetDecryptedStream(Stream encryptedStream)
        {
            ICryptoTransform decryptor;
            decryptor = symAl.CreateDecryptor(GenLegalKey(), GenLegalIV());
            return new CryptoStream(encryptedStream, decryptor, CryptoStreamMode.Read);
        }

        internal static uint Get24BitHash(string st)
        {
            if (string.IsNullOrEmpty(st))
            {
                return 0;
            }

            byte[] bytes = new byte[PACKAGE_SIZE];
            for (int i = 0; i < PACKAGE_SIZE; i++)
            {
                if (i < st.Length)
                {
                    bytes[i] = (byte)st[i];
                }
            }

            var hash = SHA512.Create();
            byte[] hashValue = hash.ComputeHash(bytes);

            for (int i = 0; i < 50000; i++)
            {
                hashValue = hash.ComputeHash(hashValue);
            }

            Common.LogDebug(string.Format(CultureInfo.CurrentCulture, "magic: {0},{1},{2}", hashValue[0], hashValue[1], hashValue[^1]));
            hash.Clear();
            return (uint)((hashValue[0] << 23) + (hashValue[1] << 16) + (hashValue[^1] << 8) + hashValue[2]);
        }

        internal static string GetDebugInfo(string st)
        {
            return string.IsNullOrEmpty(st) ? st : ((byte)(Common.GetBytesU(st).Sum(value => value) % 256)).ToString(CultureInfo.InvariantCulture);
        }

        internal static string CreateDefaultKey()
        {
            return CreateRandomKey();
        }

        private const int PW_LENGTH = 16;

        public static string CreateRandomKey()
        {
            // Not including characters like "'`O0& since they are confusing to users.
            string[] chars = new[] { "abcdefghjkmnpqrstuvxyz", "ABCDEFGHJKMNPQRSTUVXYZ", "123456789", "~!@#$%^*()_-+=:;<,>.?/\\|[]" };
            char[][] charactersUsedForKey = chars.Select(charset => Enumerable.Range(0, charset.Length - 1).Select(i => charset[i]).ToArray()).ToArray();
            byte[] randomData = new byte[1];
            string key = string.Empty;

            do
            {
                foreach (string set in chars)
                {
                    randomData = RandomNumberGenerator.GetBytes(1);
                    key += set[randomData[0] % set.Length];

                    if (key.Length >= PW_LENGTH)
                    {
                        break;
                    }
                }
            }
            while (key.Length < PW_LENGTH);

            return key;
        }

        internal static bool IsKeyValid(string key, out string error)
        {
            error = string.IsNullOrEmpty(key) || key.Length < 16
                ? "Key must have at least 16 characters in length (spaces are discarded). Key must be auto generated in one of the machines."
                : null;

            return error == null;
        }
    }
}
