// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class CmdPalProtocolActivationTests
{
    [TestMethod]
    public void Parse_SettingsUri_ReturnsGeneralSettingsRoute()
    {
        var result = CmdPalProtocolActivation.Parse(new Uri("x-cmdpal://settings"));

        Assert.IsInstanceOfType<CmdPalProtocolRoute.OpenSettings>(result);
        var message = ((CmdPalProtocolRoute.OpenSettings)result).Message;
        Assert.AreEqual(string.Empty, message.SettingsPageTag);
        Assert.IsNull(message.ExtensionGalleryId);
    }

    [TestMethod]
    public void Parse_GalleryUri_ReturnsGallerySettingsRoute()
    {
        var result = CmdPalProtocolActivation.Parse(new Uri("x-cmdpal://extensions/gallery"));

        Assert.IsInstanceOfType<CmdPalProtocolRoute.OpenSettings>(result);
        var message = ((CmdPalProtocolRoute.OpenSettings)result).Message;
        Assert.AreEqual("Gallery", message.SettingsPageTag);
        Assert.IsNull(message.ExtensionGalleryId);
    }

    [TestMethod]
    public void Parse_GalleryExtensionUri_ReturnsDecodedExtensionId()
    {
        var result = CmdPalProtocolActivation.Parse(new Uri("X-CMDPAL://EXTENSIONS/GALLERY/sample%20extension?source=web"));

        Assert.IsInstanceOfType<CmdPalProtocolRoute.OpenSettings>(result);
        var message = ((CmdPalProtocolRoute.OpenSettings)result).Message;
        Assert.AreEqual("Gallery", message.SettingsPageTag);
        Assert.AreEqual("sample extension", message.ExtensionGalleryId);
    }

    [TestMethod]
    public void TryParseExtensionId_PreservesValidIdExactly()
    {
        Assert.IsTrue(CmdPalProtocolActivation.TryParseExtensionId("sample extension", out var extensionId));
        Assert.AreEqual("sample extension", extensionId);
    }

    [TestMethod]
    public void TryParseExtensionId_RejectsNonCanonicalOrUnsafeId()
    {
        string[] invalidIds =
        [
            string.Empty,
            " colors",
            "colors ",
            "sample/extension",
            "sample\\extension",
            "sample\nextension",
            new('a', 257),
        ];

        foreach (var invalidId in invalidIds)
        {
            Assert.IsFalse(CmdPalProtocolActivation.TryParseExtensionId(invalidId, out var extensionId), invalidId);
            Assert.AreEqual(string.Empty, extensionId);
        }
    }

    [TestMethod]
    public void Parse_BackgroundUri_ReturnsBackgroundRoute()
    {
        var result = CmdPalProtocolActivation.Parse(new Uri("x-cmdpal://background"));

        Assert.IsInstanceOfType<CmdPalProtocolRoute.Background>(result);
    }

    [TestMethod]
    public void Parse_ReloadUri_ReturnsReloadRoute()
    {
        var result = CmdPalProtocolActivation.Parse(new Uri("x-cmdpal://reload"));

        Assert.IsInstanceOfType<CmdPalProtocolRoute.Reload>(result);
    }

    [DataTestMethod]
    [DataRow("https://extensions/gallery/sample")]
    [DataRow("x-cmdpal://settings/unknown")]
    [DataRow("x-cmdpal://settings-extra")]
    [DataRow("x-cmdpal://background-task")]
    [DataRow("x-cmdpal://reload-anything")]
    [DataRow("x-cmdpal://extensions/gallery/%20colors%20")]
    [DataRow("x-cmdpal://extensions/gallery/sample%2Fextension")]
    [DataRow("x-cmdpal://extensions/gallery/sample%5Cextension")]
    [DataRow("x-cmdpal://extensions/gallery/sample%0Aextension")]
    [DataRow("x-cmdpal://extensions/gallery/sample#fragment")]
    public void Parse_UnknownOrUnsafeUri_ReturnsNull(string uri)
    {
        var result = CmdPalProtocolActivation.Parse(new Uri(uri));

        Assert.IsNull(result);
    }
}
