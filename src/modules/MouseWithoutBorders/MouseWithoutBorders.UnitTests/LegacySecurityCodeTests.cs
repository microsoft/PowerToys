// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace MouseWithoutBorders.UnitTests;

[TestClass]
public sealed class LegacySecurityCodeTests
{
    [DataTestMethod]
    [DataRow("")]
    [DataRow("                ")]
    [DataRow(" \t\r\n             ")]
    [DataRow("12345678 1234567")]
    [DataRow("mwb-dpapi:v1:TEST")]
    [DataRow("MWB-DPAPI:v2:TEST")]
    [DataRow("mwb- dpapi:v1:TEST")]
    [DataRow("Pairing setup required")]
    [DataRow("Pairingsetuprequired")]
    public void InvalidNormalizedCodeIsRejected(string value)
    {
        Assert.IsFalse(SetupPage2a.TryGetSecurityCode(value, out _));
    }

    [DataTestMethod]
    [DataRow("1234567812345678", "1234567812345678")]
    [DataRow("12345678 12345678", "1234567812345678")]
    [DataRow(" TEST1234\tKEYS5678\r\n ", "TEST1234KEYS5678")]
    [DataRow("TEST-1234-KEYS-5678", "TEST-1234-KEYS-5678")]
    public void ValidCodePreservesExistingNormalization(string value, string expected)
    {
        Assert.IsTrue(SetupPage2a.TryGetSecurityCode(value, out string normalized));
        Assert.AreEqual(expected, normalized);
    }
}
