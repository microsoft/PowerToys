// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO;
using System.Security.Cryptography;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Core;

namespace MouseWithoutBorders.UnitTests.Core;

[TestClass]
public sealed class EncryptionTests
{
    // Must be at least 16 characters to be accepted as a key.
    private const string TestKey = "MwbEncryptionTestKey1234";

    // The cleartext salt (16 bytes) + IV (16 bytes) header that precedes the cipher text.
    private const int HeaderLength = Encryption.SymAlBlockSize * 2;

    [TestInitialize]
    public void TestInitialize()
    {
        Encryption.InitEncryption();
        Encryption.MyKey = TestKey;
    }

    [TestMethod]
    public void EncryptThenDecryptShouldRoundTripPlainText()
    {
        // 48 bytes = 3 AES blocks, so the (zeros) padding adds no trailing-byte ambiguity.
        var plainText = new byte[48];
        for (var i = 0; i < plainText.Length; i++)
        {
            plainText[i] = (byte)(i + 1);
        }

        var wire = Encrypt(plainText);
        var decrypted = Decrypt(wire, plainText.Length);

        CollectionAssert.AreEqual(plainText, decrypted);
    }

    [TestMethod]
    public void EncryptingSamePlainTextTwiceShouldProduceDifferentBytesOnTheWire()
    {
        var plainText = new byte[48];

        var wire1 = Encrypt(plainText);
        var wire2 = Encrypt(plainText);

        // A fresh random salt + IV is generated for every stream, so identical plaintext
        // must never produce identical bytes on the wire. This guards against regressing
        // to a fixed salt / IV (MSRC 118042).
        CollectionAssert.AreNotEqual(wire1, wire2);
    }

    [TestMethod]
    public void EachEncryptedStreamShouldEmitAUniqueHeader()
    {
        var plainText = new byte[48];

        var header1 = Encrypt(plainText)[..HeaderLength];
        var header2 = Encrypt(plainText)[..HeaderLength];

        CollectionAssert.AreNotEqual(header1, header2);
    }

    private static byte[] Encrypt(byte[] plainText)
    {
        using var transport = new MemoryStream();
        var encryptStream = (CryptoStream)Encryption.GetEncryptedStream(transport);
        try
        {
            encryptStream.Write(plainText, 0, plainText.Length);
            encryptStream.FlushFinalBlock();
            return transport.ToArray();
        }
        finally
        {
            encryptStream.Dispose();
        }
    }

    private static byte[] Decrypt(byte[] wire, int plainTextLength)
    {
        using var transport = new MemoryStream(wire);
        var decryptStream = Encryption.GetDecryptedStream(transport);
        try
        {
            var buffer = new byte[plainTextLength];
            decryptStream.ReadExactly(buffer);
            return buffer;
        }
        finally
        {
            decryptStream.Dispose();
        }
    }
}
