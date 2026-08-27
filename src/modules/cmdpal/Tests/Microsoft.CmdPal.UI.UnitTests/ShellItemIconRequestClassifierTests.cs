// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class ShellItemIconRequestClassifierTests
{
    [DataTestMethod]
    [DataRow("C:\\Files\\report.txt")]
    [DataRow("C:\\Files\\README")]
    [DataRow("C:\\Files\\Folder")]
    [DataRow("C:\\Files\\folder,with-comma\\report.txt")]
    [DataRow("\\\\server\\share\\report.txt")]
    public void LegacyFilesystemItemsUseShellIdentity(string value)
    {
        Assert.IsTrue(ShellItemIconRequestClassifier.TryClassify(value, out var request));
        Assert.AreEqual(value, request.ItemPath);
        Assert.IsFalse(request.Jumbo);
    }

    [DataTestMethod]
    [DataRow("Assets\\icon.png")]
    [DataRow("C:\\Files\\image.png")]
    [DataRow("file:///C:/Files/image.png")]
    [DataRow("C:\\Files\\image.avif")]
    [DataRow("C:\\Files\\image.heic")]
    [DataRow("C:\\Files\\image.jfif")]
    [DataRow("C:\\Files\\vector.svg")]
    [DataRow("C:\\Files\\app.exe")]
    [DataRow("C:\\Files\\APP.EXE,0")]
    [DataRow("C:\\Files\\App.Dll,-1")]
    [DataRow("C:\\Files\\icons.dll,1")]
    [DataRow("C:\\Files\\shortcut.lnk")]
    [DataRow("\uE700")]
    [DataRow("|ShellItemIcon|")]
    public void ExistingFastPathsAndMalformedProtocolsArePreserved(string value)
    {
        Assert.IsFalse(ShellItemIconRequestClassifier.TryClassify(value, out _));
    }

    [TestMethod]
    public void ExplicitProtocolCanRequestImageThumbnailAndJumboIcon()
    {
        const string ItemPath = "C:\\Files\\image.png";

        Assert.IsTrue(
            ShellItemIconRequestClassifier.TryClassify(
                ShellItemIconProtocol.CreateJumbo(ItemPath),
                out var request));
        Assert.AreEqual(ItemPath, request.ItemPath);
        Assert.IsTrue(request.Jumbo);
    }

    [TestMethod]
    public void LegacyFileUriUsesItsLocalShellItemPath()
    {
        Assert.IsTrue(
            ShellItemIconRequestClassifier.TryClassify(
                "file:///C:/Files/report.txt",
                out var request));
        Assert.AreEqual("C:\\Files\\report.txt", request.ItemPath);
        Assert.IsFalse(request.Jumbo);
    }
}
