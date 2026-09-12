// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CommandPalette.Extensions.Toolkit.UnitTests;

[TestClass]
public class ShellItemIconProtocolTests
{
    [DataTestMethod]
    [DataRow("C:\\Files\\report.txt")]
    [DataRow("C:\\Files\\name|with:separators.txt")]
    [DataRow("C:\\Files\\😀.txt")]
    [DataRow("C:\\Files\\👩‍💻.txt")]
    public void StandardRequestRoundTripsAnyUtf16Path(string itemPath)
    {
        var value = ShellItemIconProtocol.Create(itemPath);

        Assert.IsTrue(ShellItemIconProtocol.IsProtocol(value));
        Assert.IsTrue(ShellItemIconProtocol.TryParse(value, out var parsedPath, out var jumbo));
        Assert.AreEqual(itemPath, parsedPath);
        Assert.IsFalse(jumbo);
    }

    [TestMethod]
    public void JumboRequestRoundTrips()
    {
        const string ItemPath = "C:\\Files\\sample.lnk";

        var value = ShellItemIconProtocol.CreateJumbo(ItemPath);

        Assert.IsTrue(ShellItemIconProtocol.TryParse(value, out var parsedPath, out var jumbo));
        Assert.AreEqual(ItemPath, parsedPath);
        Assert.IsTrue(jumbo);
    }

    [TestMethod]
    public void ProtocolStringPassesThroughExistingIconTypes()
    {
        var value = ShellItemIconProtocol.Create("C:\\Files\\report.txt");
        var data = new IconData(value);
        var info = new IconInfo(data);

        Assert.AreEqual(value, data.Icon);
        Assert.AreSame(data, info.Light);
        Assert.AreSame(data, info.Dark);
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("|ShellItemIcon|")]
    [DataRow("|JumboShellItemIcon|")]
    [DataRow("|ShellItemIcon|v2;1:a")]
    [DataRow("|JumboShellItemIcon|v2;1:a")]
    [DataRow("|ShellItemIcon|v1;")]
    [DataRow("|JumboShellItemIcon|v1;")]
    [DataRow("|ShellItemIcon|v1;0:")]
    [DataRow("|JumboShellItemIcon|v1;0:")]
    [DataRow("|ShellItemIcon|v1;-1:a")]
    [DataRow("|JumboShellItemIcon|v1;-1:a")]
    [DataRow("|ShellItemIcon|v1;5:abc")]
    [DataRow("|JumboShellItemIcon|v1;5:abc")]
    [DataRow("|ShellItemIcon|v1;1:ab")]
    [DataRow("|JumboShellItemIcon|v1;1:ab")]
    public void InvalidOrUnsupportedPayloadIsRejected(string? value)
    {
        Assert.IsFalse(ShellItemIconProtocol.TryParse(value, out var itemPath, out var jumbo));
        Assert.AreEqual(string.Empty, itemPath);
        Assert.IsFalse(jumbo);
    }

    [DataTestMethod]
    [DataRow("|ShellItemIcon|")]
    [DataRow("|JumboShellItemIcon|")]
    public void MalformedPayloadIsStillClaimedByProtocol(string value)
    {
        Assert.IsTrue(ShellItemIconProtocol.IsProtocol(value));
        Assert.IsFalse(ShellItemIconProtocol.TryParse(value, out _, out _));
    }

    [TestMethod]
    public void EncoderRequiresItemPath()
    {
        Assert.ThrowsException<ArgumentException>(() => ShellItemIconProtocol.Create(string.Empty));
        Assert.ThrowsException<ArgumentNullException>(() => ShellItemIconProtocol.CreateJumbo(null!));
    }
}
