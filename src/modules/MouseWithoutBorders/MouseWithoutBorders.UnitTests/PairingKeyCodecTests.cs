// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;

using Microsoft.VisualStudio.TestTools.UnitTesting;
using MouseWithoutBorders.Class;
using Windows.Security.Cryptography;
using Windows.Security.Cryptography.DataProtection;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class PairingKeyCodecTests
{
    private static DpapiPairingKeyCodec CreateCodec()
    {
        using var identity = WindowsIdentity.GetCurrent();
        return new(identity.User!);
    }

    [DataTestMethod]
    [DataRow("mwb-dpapi:v1:TEST", true)]
    [DataRow("mwb-dpapi:v1:", true)]
    [DataRow("mwb-dpapi:v1:!", true)]
    [DataRow("mwb-dpapi:v2:TEST", true)]
    [DataRow("mwb-dpapi:", true)]
    [DataRow("mwb-dpapi:v99:TEST", true)]
    [DataRow("mwb-dpapi", false)]
    [DataRow("MWB-DPAPI:v1:TEST", true)]
    [DataRow("mwb-dpapi:v1:A QID", true)]
    [DataRow("unencrypted-key", false)]
    [DataRow(null, false)]
    public void ProvidersRecognizeReservedFamilyWithoutValidatingPayload(string value, bool reserved)
    {
        Assert.AreEqual(reserved, CreateCodec().IsEncrypted(value));
        Assert.AreEqual(reserved, new PassthroughPairingKeyCodec().IsEncrypted(value));
    }

    [TestMethod]
    public void CurrentTestUserDpapiRoundTrip()
    {
        var codec = CreateCodec();
        var encrypted = codec.Encrypt("TEST-KEY");
        StringAssert.StartsWith(encrypted, PairingKeyCodec.EncryptedPrefix + PairingKeyCodec.EncryptedVersion);
        Assert.AreEqual("TEST-KEY", codec.Decrypt(encrypted));
        Assert.IsTrue(codec.IsEncrypted(encrypted));
        Assert.ThrowsException<ArgumentException>(() => codec.Encrypt(encrypted));
        var passthrough = new PassthroughPairingKeyCodec();
        Assert.AreEqual(encrypted, passthrough.Encrypt(encrypted));
        Assert.AreEqual(encrypted, passthrough.Decrypt(encrypted));
    }

    [TestMethod]
    public void DecryptAcceptsBase64WhitespaceInProtectedPayload()
    {
        var codec = CreateCodec();
        var encrypted = codec.Encrypt("TEST-KEY");
        var withWhitespace = encrypted.Insert(PairingKeyCodec.EncryptedPrefix.Length + PairingKeyCodec.EncryptedVersion.Length + 4, " \r\n");
        Assert.AreEqual("TEST-KEY", codec.Decrypt(withWhitespace));
    }

    [TestMethod]
    public void FactoryUsesActualExecutingIdentity()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var codec = DpapiPairingKeyCodec.CreateForCurrentIdentity(identity.User!);
        Assert.AreEqual(identity.IsSystem, codec is DpapiPairingKeyCodec);
        Assert.AreEqual(!identity.IsSystem, codec is PassthroughPairingKeyCodec);
    }

    [TestMethod]
    public void DifferentInteractiveUserPayloadBindingIsRejected()
    {
        var encrypted = CreateCodec().Encrypt("TEST-KEY");
        var other = new DpapiPairingKeyCodec(new SecurityIdentifier("S-1-5-21-1-2-3-1001"));
        Assert.ThrowsException<CryptographicException>(() => other.Decrypt(encrypted));
    }

    [TestMethod]
    public void WrongScopeIsRejected()
    {
        var payload = ProtectWithWindowsProvider("LOCAL=machine", CreatePayload("TEST-KEY"));
        Assert.ThrowsException<CryptographicException>(() => CreateCodec().Decrypt(payload));
    }

    [TestMethod]
    public void ClassicLocalMachineScopeIsRejected()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User!;
        var entropy = Encoding.UTF8.GetBytes("PowerToys.MouseWithoutBorders.Pairing.v1:" + sid.Value);
        var payload = ProtectedData.Protect(Encoding.UTF8.GetBytes("TEST-KEY"), entropy, DataProtectionScope.LocalMachine);
        Assert.ThrowsException<CryptographicException>(() => CreateCodec().Decrypt(PairingKeyCodec.EncryptedPrefix + PairingKeyCodec.EncryptedVersion + Convert.ToBase64String(payload)));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("unencrypted-key")]
    [DataRow("mwb-dpapi:v1:")]
    [DataRow("mwb-dpapi:v1:!")]
    [DataRow("mwb-dpapi:v1:TEST")]
    [DataRow("mwb-dpapi:v2:TEST")]
    [DataRow("MWB-DPAPI:v1:TEST")]
    [DataRow("mwb-dpapi:v1:A QID")]
    public void MissingMalformedOrInvalidProtectedDataIsRejected(string value)
    {
        Assert.ThrowsException<CryptographicException>(() => CreateCodec().Decrypt(value));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow(" ")]
    [DataRow("mwb-dpapi:v1:TEST")]
    [DataRow("mwb-dpapi:v2:invalid")]
    [DataRow("MWB-DPAPI:v1:TEST")]
    public void NonKeysAreRejectedByProtectedCodec(string value)
    {
        Assert.ThrowsException<ArgumentException>(() => CreateCodec().Encrypt(value));
    }

    [TestMethod]
    public void NullKeysAreRejectedByProtectedCodec()
    {
        Assert.ThrowsException<ArgumentNullException>(() => CreateCodec().Encrypt(null));
    }

    [DataTestMethod]
    [DataRow(" Original-Key ")]
    [DataRow("mwb-dpapi:v1:TEST")]
    [DataRow("mwb-dpapi:v2:invalid")]
    [DataRow("MWB-DPAPI:v1:TEST")]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" \t\r\n")]
    public void PassthroughPreservesEveryValueUnchanged(string value)
    {
        var codec = new PassthroughPairingKeyCodec();
        Assert.AreEqual(value, codec.Encrypt(value));
        Assert.AreEqual(value, codec.Decrypt(value));
    }

    [DataTestMethod]
    [DataRow("")]
    [DataRow("mwb-dpapi:v1:TEST")]
    public void ProtectedNonKeyPlaintextIsRejected(string value)
    {
        var payload = ProtectWithWindowsProvider("LOCAL=user", CreatePayload(value));
        Assert.ThrowsException<CryptographicException>(() => CreateCodec().Decrypt(payload));
    }

    [TestMethod]
    public void IndependentUserPolicyPayloadRoundTrips()
    {
        var payload = ProtectWithWindowsProvider("LOCAL=user", CreatePayload("TEST-KEY"));
        Assert.AreEqual("TEST-KEY", CreateCodec().Decrypt(payload));
    }

    [TestMethod]
    public void InvalidProtectedPayloadLayoutIsRejected()
    {
        foreach (var plaintext in new[] { new byte[] { 0 }, new byte[] { 255, 1, 2 }, Encoding.UTF8.GetBytes("TEST-KEY") })
        {
            var payload = ProtectWithWindowsProvider("LOCAL=user", plaintext);
            Assert.ThrowsException<CryptographicException>(() => CreateCodec().Decrypt(payload));
        }
    }

    [TestMethod]
    public void InvalidProtectedUtf8IsRejected()
    {
        var plaintext = CreatePayload("X");
        plaintext[^1] = 0xff;
        var payload = ProtectWithWindowsProvider("LOCAL=user", plaintext);
        Assert.ThrowsException<CryptographicException>(() => CreateCodec().Decrypt(payload));
    }

    private static byte[] CreatePayload(string value)
    {
        using var identity = WindowsIdentity.GetCurrent();
        var sid = identity.User!;
        var plaintext = new byte[1 + sid.BinaryLength + Encoding.UTF8.GetByteCount(value)];
        plaintext[0] = checked((byte)sid.BinaryLength);
        sid.GetBinaryForm(plaintext, 1);
        Encoding.UTF8.GetBytes(value, plaintext.AsSpan(1 + sid.BinaryLength));
        return plaintext;
    }

    private static string ProtectWithWindowsProvider(string descriptor, byte[] plaintext)
    {
        try
        {
            var provider = new DataProtectionProvider(descriptor);
            var protectedBuffer = provider.ProtectAsync(CryptographicBuffer.CreateFromByteArray(plaintext)).AsTask().GetAwaiter().GetResult();
            CryptographicBuffer.CopyToByteArray(protectedBuffer, out var encrypted);
            return PairingKeyCodec.EncryptedPrefix + PairingKeyCodec.EncryptedVersion + Convert.ToBase64String(encrypted);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(plaintext);
        }
    }
}
