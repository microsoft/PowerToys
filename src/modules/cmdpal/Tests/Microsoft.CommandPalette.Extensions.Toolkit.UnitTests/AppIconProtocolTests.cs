// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class AppIconProtocolTests
{
    [TestMethod]
    public void StandardRequestRoundTripsOrderedCandidatesWithoutReservedCharacters()
    {
        const string primary = "C:\\Windows\\System32\\shell32.dll,1";
        const string fallback = "steam://run/123|variant";

        var value = AppIconProtocol.Create(primary, fallback);
        var parsed = AppIconProtocol.TryParse(value, out var candidates, out var jumbo);

        Assert.IsTrue(AppIconProtocol.IsProtocol(value));
        Assert.IsTrue(parsed);
        Assert.IsFalse(jumbo);
        CollectionAssert.AreEqual(new[] { primary, fallback }, candidates);
    }

    [TestMethod]
    public void JumboRequestPreservesThreeUnicodeCandidates()
    {
        const string primary = "C:\\Icons\\🪄.ico";
        const string fallback = "C:\\Program Files\\Example\\app.exe";
        const string finalFallback = "https://example.test/icon|large";

        var value = AppIconProtocol.CreateJumbo(primary, fallback, finalFallback);
        var parsed = AppIconProtocol.TryParse(value, out var candidates, out var jumbo);

        Assert.IsTrue(parsed);
        Assert.IsTrue(jumbo);
        CollectionAssert.AreEqual(new[] { primary, fallback, finalFallback }, candidates);
    }

    [DataTestMethod]
    [DataRow("😀", "|AppIcon|v1;2:😀")]
    [DataRow("👩‍💻", "|AppIcon|v1;5:👩‍💻")]
    [DataRow("😀👩‍💻❤️", "|AppIcon|v1;9:😀👩‍💻❤️")]
    public void EmojiCandidatesUseUtf16LengthsAndRoundTrip(string candidate, string expectedValue)
    {
        var value = AppIconProtocol.Create(candidate);

        Assert.AreEqual(expectedValue, value);
        Assert.IsTrue(AppIconProtocol.TryParse(value, out var candidates, out var jumbo));
        Assert.IsFalse(jumbo);
        CollectionAssert.AreEqual(new[] { candidate }, candidates);
    }

    [TestMethod]
    public void EncoderOmitsEmptyAndDuplicateFallbacks()
    {
        const string primary = "C:\\Windows\\notepad.exe";

        var value = AppIconProtocol.CreateJumbo(primary, string.Empty, primary);

        Assert.IsTrue(AppIconProtocol.TryParse(value, out var candidates, out var jumbo));
        Assert.IsTrue(jumbo);
        CollectionAssert.AreEqual(new[] { primary }, candidates);
    }

    [TestMethod]
    public void ProtocolStringPassesThroughExistingIconTypes()
    {
        var value = AppIconProtocol.Create("C:\\Windows\\notepad.exe");
        var data = new IconData(value);
        var info = new IconInfo(data);

        Assert.AreEqual(value, data.Icon);
        Assert.AreSame(data, info.Light);
        Assert.AreSame(data, info.Dark);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("|AppIcon|")]
    [DataRow("|AppIcon|C:\\Windows\\notepad.exe")]
    [DataRow("|AppIcon|v2;1:a")]
    [DataRow("|AppIcon|v1;")]
    [DataRow("|AppIcon|v1;0:")]
    [DataRow("|AppIcon|v1;-1:a")]
    [DataRow("|AppIcon|v1;5:abc")]
    [DataRow("|AppIcon|v1;1:a1:a")]
    [DataRow("|AppIcon|v1;1:a1:b1:c1:d1:e1:f1:g1:h1:i")]
    public void InvalidOrUnsupportedPayloadIsRejected(string? value)
    {
        var parsed = AppIconProtocol.TryParse(value, out var candidates, out var jumbo);

        Assert.IsFalse(parsed);
        Assert.AreEqual(0, candidates.Length);
        Assert.IsFalse(jumbo);
    }

    [DataTestMethod]
    [DataRow("|AppIcon|")]
    [DataRow("|JumboAppIcon|")]
    public void MalformedPayloadIsStillClaimedByProtocol(string value)
    {
        Assert.IsTrue(AppIconProtocol.IsProtocol(value));
        Assert.IsFalse(AppIconProtocol.TryParse(value, out _, out _));
    }

    [TestMethod]
    public void EncoderRequiresPrimaryCandidate()
    {
        Assert.ThrowsException<ArgumentException>(() => AppIconProtocol.Create(string.Empty));
        Assert.ThrowsException<ArgumentNullException>(() => AppIconProtocol.CreateJumbo(null!));
    }
}
