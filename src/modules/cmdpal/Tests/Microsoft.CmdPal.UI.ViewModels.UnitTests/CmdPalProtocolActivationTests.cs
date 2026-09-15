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
    private readonly CmdPalProtocolActivation _protocolActivation = new();

    [TestMethod]
    public void TryParse_SettingsUri_ReturnsGeneralSettingsRoute()
    {
        Assert.IsTrue(_protocolActivation.TryParse(new Uri("x-cmdpal://settings"), out var result));

        Assert.IsInstanceOfType<CmdPalProtocolRoute.OpenSettings>(result);
        var message = ((CmdPalProtocolRoute.OpenSettings)result).Message;
        Assert.AreEqual(string.Empty, message.SettingsPageTag);
        Assert.IsNull(message.ExtensionGalleryId);
    }

    [TestMethod]
    public void TryParse_GalleryUri_ReturnsGallerySettingsRoute()
    {
        Assert.IsTrue(_protocolActivation.TryParse(new Uri("x-cmdpal://extensions/gallery"), out var result));

        Assert.IsInstanceOfType<CmdPalProtocolRoute.OpenSettings>(result);
        var message = ((CmdPalProtocolRoute.OpenSettings)result).Message;
        Assert.AreEqual("Gallery", message.SettingsPageTag);
        Assert.IsNull(message.ExtensionGalleryId);
    }

    [TestMethod]
    public void TryParse_GalleryExtensionUri_ReturnsDecodedExtensionId()
    {
        Assert.IsTrue(_protocolActivation.TryParse(
            new Uri("X-CMDPAL://EXTENSIONS/GALLERY/sample%20extension?source=web"),
            out var result));

        Assert.IsInstanceOfType<CmdPalProtocolRoute.OpenSettings>(result);
        var message = ((CmdPalProtocolRoute.OpenSettings)result).Message;
        Assert.AreEqual("Gallery", message.SettingsPageTag);
        Assert.AreEqual("sample extension", message.ExtensionGalleryId);
    }

    [TestMethod]
    public void TryParseExtensionId_PreservesValidIdExactly()
    {
        Assert.IsTrue(_protocolActivation.TryParseExtensionId("sample extension", out var extensionId));
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
            Assert.IsFalse(_protocolActivation.TryParseExtensionId(invalidId, out var extensionId), invalidId);
            Assert.AreEqual(string.Empty, extensionId);
        }
    }

    [TestMethod]
    public void TryParse_BackgroundUri_ReturnsBackgroundRoute()
    {
        Assert.IsTrue(_protocolActivation.TryParse(new Uri("x-cmdpal://background"), out var result));

        Assert.IsInstanceOfType<CmdPalProtocolRoute.Background>(result);
    }

    [TestMethod]
    public void TryParse_ReloadUri_ReturnsReloadRoute()
    {
        Assert.IsTrue(_protocolActivation.TryParse(new Uri("x-cmdpal://reload"), out var result));

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
    public void TryParse_UnknownOrUnsafeUri_ReturnsFalse(string uri)
    {
        var parsed = _protocolActivation.TryParse(new Uri(uri), out var result);

        Assert.IsFalse(parsed);
        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryParse_NullUri_ReturnsFalse()
    {
        var parsed = _protocolActivation.TryParse(null, out var result);

        Assert.IsFalse(parsed);
        Assert.IsNull(result);
    }
}
