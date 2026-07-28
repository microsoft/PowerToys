// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Security.Cryptography;

// <summary>
//     Encrypt/decrypt implementation.
// </summary>
// <history>
//     2008 created by Truong Do (ductdo).
//     2009-... modified by Truong Do (TruongDo).
//     2023- Included in PowerToys.
// </history>
namespace MouseWithoutBorders.Core;

internal static class Encryption
{
#pragma warning disable SYSLIB0021
    private static AesCryptoServiceProvider symAl;
#pragma warning restore SYSLIB0021
#pragma warning disable SA1307 // Accessible fields should begin with upper-case letter
    internal static string myKey;
#pragma warning restore SA1307
    private static uint magicNumber;
    private static Random ran = new(); // Used for non encryption related functionality.
    internal const int SymAlBlockSize = 16;

    // Size (in bytes) of the random, per-connection PBKDF2 salt that is exchanged in the
    // clear at the start of every encrypted stream. A unique random salt prevents an
    // attacker from pre-computing a single brute-force/rainbow table that could be reused
    // against every captured connection.
    private const int SaltSize = 16;

    // Number of PBKDF2 iterations used to derive the symmetric key from the shared secret.
    private const int KeyDerivationIterations = 50000;

    // Length (in bytes) of the derived AES-256 key.
    private const int DerivedKeyLength = 32;

    internal static Random Ran
    {
        get => Encryption.ran ??= new Random();
        set => Encryption.ran = value;
    }

    internal static uint MagicNumber
    {
        get => Encryption.magicNumber;
        set => Encryption.magicNumber = value;
    }

    internal static string MyKey
    {
        get => Encryption.myKey;
        set => Encryption.myKey = value;
    }

    private static string KeyDisplayedText(string key)
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
            Logger.Log(e);
        }
    }

    private static byte[] GenLegalKey(byte[] salt)
    {
        return Rfc2898DeriveBytes.Pbkdf2(
            Encryption.MyKey,
            salt,
            KeyDerivationIterations,
            HashAlgorithmName.SHA512,
            DerivedKeyLength);
    }

    internal static Stream GetEncryptedStream(Stream encryptedStream)
    {
        // A fresh random salt and IV are generated for every connection and sent in the
        // clear ahead of the cipher text. Neither value is secret: deriving the symmetric
        // key still requires the shared secret. The random salt prevents an attacker from
        // pre-computing a brute-force/rainbow table that could be reused against every
        // captured connection, and the random IV avoids reusing a fixed IV across sessions.
        byte[] header = new byte[SaltSize + SymAlBlockSize];
        RandomNumberGenerator.Fill(header);
        ExchangeEncryptionHeader(encryptedStream, header, send: true);

        byte[] salt = header[..SaltSize];
        byte[] iv = header[SaltSize..];

        ICryptoTransform encryptor = symAl.CreateEncryptor(GenLegalKey(salt), iv);
        return new CryptoStream(encryptedStream, encryptor, CryptoStreamMode.Write);
    }

    internal static Stream GetDecryptedStream(Stream encryptedStream)
    {
        byte[] header = new byte[SaltSize + SymAlBlockSize];
        ExchangeEncryptionHeader(encryptedStream, header, send: false);

        byte[] salt = header[..SaltSize];
        byte[] iv = header[SaltSize..];

        ICryptoTransform decryptor = symAl.CreateDecryptor(GenLegalKey(salt), iv);
        return new CryptoStream(encryptedStream, decryptor, CryptoStreamMode.Read);
    }

    // Sends or receives the cleartext salt + IV header. Mirrors the tolerant socket-close
    // handling used elsewhere so an expected remote disconnect does not surface as an error.
    private static void ExchangeEncryptionHeader(Stream stream, byte[] header, bool send)
    {
        try
        {
            if (send)
            {
                stream.Write(header, 0, header.Length);
            }
            else
            {
                stream.ReadExactly(header);
            }
        }
        catch (IOException e)
        {
            Logger.Log($"{nameof(ExchangeEncryptionHeader)}: Exception {(send ? "writing" : "reading")} the encryption header to the socket stream: {e.InnerException?.GetType()}/{e.Message}. (This is expected when the remote machine closes the connection during desktop switch or reconnection.)");

            if (e is not EndOfStreamException && e.InnerException is not (SocketException or ObjectDisposedException))
            {
                throw;
            }
        }
    }

    internal static uint Get24BitHash(string st)
    {
        if (string.IsNullOrEmpty(st))
        {
            return 0;
        }

        byte[] bytes = new byte[Package.PACKAGE_SIZE];
        for (int i = 0; i < Package.PACKAGE_SIZE; i++)
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

        Logger.LogDebug(string.Format(CultureInfo.CurrentCulture, "magic: {0},{1},{2}", hashValue[0], hashValue[1], hashValue[^1]));
        hash.Clear();
        return (uint)((hashValue[0] << 23) + (hashValue[1] << 16) + (hashValue[^1] << 8) + hashValue[2]);
    }

    internal static string CreateDefaultKey()
    {
        return CreateRandomKey();
    }

    private const int PW_LENGTH = 16;

    internal static string CreateRandomKey()
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
