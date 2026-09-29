// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class CmdPalProtocolActivationTests
{
    private readonly CmdPalProtocolActivation _protocolActivation = new(new SettingsLinkResolver());

    [TestMethod]
    public void TryParse_SettingsUri_ReturnsGeneralSettingsRoute()
    {
        Assert.IsTrue(_protocolActivation.TryParse(new Uri("x-cmdpal://settings"), out var result));

        Assert.IsInstanceOfType<CmdPalProtocolRoute.OpenSettings>(result);
        var message = ((CmdPalProtocolRoute.OpenSettings)result).Message;
        Assert.AreEqual(string.Empty, message.SettingsPageTag);
        Assert.IsNull(message.ExtensionGalleryId);
        Assert.IsNull(message.SettingsLinkId);
        Assert.IsNull(message.ExtensionProviderId);
    }

    [DataTestMethod]
    [DataRow("general", SettingsLinkIds.General.Page)]
    [DataRow("appearance", SettingsLinkIds.Appearance.Page)]
    [DataRow("extensions", SettingsLinkIds.Extensions.Page)]
    [DataRow("dock", SettingsLinkIds.Dock.Page)]
    public void TryParse_SettingsPageUri_ReturnsStableLink(string page, string expectedLinkId)
    {
        Assert.IsTrue(_protocolActivation.TryParse(new Uri($"x-cmdpal://settings/{page}"), out var result));

        var message = ((CmdPalProtocolRoute.OpenSettings)result).Message;
        Assert.AreEqual(string.Empty, message.SettingsPageTag);
        Assert.IsNull(message.ExtensionGalleryId);
        Assert.AreEqual(expectedLinkId, message.SettingsLinkId);
        Assert.IsNull(message.ExtensionProviderId);
    }

    [TestMethod]
    public void TryParse_SettingsDestinationUri_ReturnsStableLinkWithoutUiDestination()
    {
        Assert.IsTrue(_protocolActivation.TryParse(
            new Uri("X-CMDPAL://SETTINGS/EXTERNAL-COMMAND-LINKS"),
            out var result));

        var message = ((CmdPalProtocolRoute.OpenSettings)result).Message;
        Assert.AreEqual(string.Empty, message.SettingsPageTag);
        Assert.AreEqual(SettingsLinkIds.General.ExternalCommandLinks, message.SettingsLinkId);
    }

    [TestMethod]
    public void TryParse_ExtensionSettingsUri_ReturnsProviderRoute()
    {
        Assert.IsTrue(_protocolActivation.TryParse(
            new Uri("x-cmdpal://settings/extension/com.microsoft.cmdpal.builtin.calculator"),
            out var result));

        var message = ((CmdPalProtocolRoute.OpenSettings)result).Message;
        Assert.AreEqual(string.Empty, message.SettingsPageTag);
        Assert.AreEqual(SettingsLinkIds.Extensions.Extension.Page, message.SettingsLinkId);
        Assert.AreEqual("com.microsoft.cmdpal.builtin.calculator", message.ExtensionProviderId);
    }

    [TestMethod]
    public void TryParse_ExtensionSettingsTargetUri_ReturnsProviderAndTarget()
    {
        Assert.IsTrue(_protocolActivation.TryParse(
            new Uri("X-CMDPAL://SETTINGS/EXTENSION/com.example.Provider/SETTINGS"),
            out var result));

        var message = ((CmdPalProtocolRoute.OpenSettings)result).Message;
        Assert.AreEqual(string.Empty, message.SettingsPageTag);
        Assert.AreEqual("com.example.Provider", message.ExtensionProviderId);
        Assert.AreEqual(SettingsLinkIds.Extensions.Extension.Settings, message.SettingsLinkId);
    }

    [TestMethod]
    public void TryParse_GalleryUri_ReturnsGallerySettingsRoute()
    {
        Assert.IsTrue(_protocolActivation.TryParse(new Uri("x-cmdpal://extensions/gallery"), out var result));

        Assert.IsInstanceOfType<CmdPalProtocolRoute.OpenSettings>(result);
        var message = ((CmdPalProtocolRoute.OpenSettings)result).Message;
        Assert.AreEqual(SettingsPageTags.Gallery, message.SettingsPageTag);
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
        Assert.AreEqual(SettingsPageTags.Gallery, message.SettingsPageTag);
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

    [TestMethod]
    public void TryParse_CommandUri_ReturnsExactProviderAndCommandRoute()
    {
        Assert.IsTrue(_protocolActivation.TryParse(
            new Uri("x-cmdpal://commands/provider.id/command%20id"),
            out var result));

        Assert.AreEqual(new CmdPalProtocolRoute.ExecuteCommand("provider.id", "command id"), result);
    }

    [TestMethod]
    public void TryParse_CommandUriWithListPageOptions_ReturnsTypedOptions()
    {
        Assert.IsTrue(_protocolActivation.TryParse(
            new Uri("x-cmdpal://commands/provider.id/command.id?filter=running&query=ssh%20server"),
            out var result));

        var expected = new CmdPalProtocolRoute.ExecuteCommand(
            "provider.id",
            "command.id",
            new ListPageLaunchOptions(Query: "ssh server", FilterId: "running"));
        Assert.IsInstanceOfType<CmdPalProtocolRoute.ExecuteCommand>(result);
        Assert.AreEqual(expected, result);
        Assert.IsTrue(((CmdPalProtocolRoute.ExecuteCommand)result).ListPageOptions!.RequiresOneTimeConsent);
    }

    [DataTestMethod]
    [DataRow("provider.id", "command-id")]
    [DataRow("provider with spaces", "command with spaces")]
    [DataRow("provider:id", "command:id")]
    public void CreateUri_CommandRoute_RoundTrips(string providerId, string commandId)
    {
        var route = new CmdPalProtocolRoute.ExecuteCommand(providerId, commandId);

        var uri = _protocolActivation.CreateUri(route);

        Assert.IsTrue(_protocolActivation.TryParse(uri, out var result));
        Assert.AreEqual(route, result);
    }

    [TestMethod]
    public void CreateUri_CommandRouteWithListPageOptions_UsesCanonicalOrderAndRoundTrips()
    {
        var route = new CmdPalProtocolRoute.ExecuteCommand(
            "provider.id",
            "command.id",
            new ListPageLaunchOptions(Query: "C++ & .NET", FilterId: "running/active"));

        var uri = _protocolActivation.CreateUri(route);

        Assert.AreEqual(
            "x-cmdpal://commands/provider.id/command.id?filter=running%2Factive&query=C%2B%2B%20%26%20.NET",
            uri.AbsoluteUri);
        Assert.IsTrue(_protocolActivation.TryParse(uri, out var result));
        Assert.AreEqual(route, result);
    }

    [TestMethod]
    public void CreateUri_QueryOnlyCommandRoute_DoesNotRequireOneTimeConsent()
    {
        var options = new ListPageLaunchOptions(Query: "ssh");
        var route = new CmdPalProtocolRoute.ExecuteCommand("provider", "command", options);

        var uri = _protocolActivation.CreateUri(route);

        Assert.AreEqual("x-cmdpal://commands/provider/command?query=ssh", uri.AbsoluteUri);
        Assert.IsFalse(options.RequiresOneTimeConsent);
        Assert.IsTrue(_protocolActivation.TryParse(uri, out var result));
        Assert.AreEqual(route, result);
    }

    [TestMethod]
    public void CreateUri_FilterOnlyCommandRoute_RequiresOneTimeConsent()
    {
        var options = new ListPageLaunchOptions(FilterId: "running");
        var route = new CmdPalProtocolRoute.ExecuteCommand("provider", "command", options);

        var uri = _protocolActivation.CreateUri(route);

        Assert.AreEqual("x-cmdpal://commands/provider/command?filter=running", uri.AbsoluteUri);
        Assert.IsTrue(options.RequiresOneTimeConsent);
        Assert.IsTrue(_protocolActivation.TryParse(uri, out var result));
        Assert.AreEqual(route, result);
    }

    [TestMethod]
    public void CreateUri_GalleryExtensionRoute_UsesExpectedPath()
    {
        var route = new CmdPalProtocolRoute.OpenSettings(new OpenSettingsMessage(SettingsPageTags.Gallery, "jiripolasek.colors"));

        var uri = _protocolActivation.CreateUri(route);

        Assert.AreEqual("x-cmdpal://extensions/gallery/jiripolasek.colors", uri.AbsoluteUri);
        Assert.IsTrue(_protocolActivation.TryParse(uri, out var result));
        Assert.AreEqual(route, result);
    }

    [TestMethod]
    public void CreateUri_SettingsDestinationRoute_UsesExpectedPathAndRoundTrips()
    {
        var route = new CmdPalProtocolRoute.OpenSettings(new OpenSettingsMessage(
            SettingsLinkId: SettingsLinkIds.Appearance.Background));

        var uri = _protocolActivation.CreateUri(route);

        Assert.AreEqual("x-cmdpal://settings/appearance-background", uri.AbsoluteUri);
        Assert.IsTrue(_protocolActivation.TryParse(uri, out var result));
        Assert.AreEqual(route, result);
    }

    [TestMethod]
    public void CreateUri_ExtensionSettingsTargetRoute_UsesExpectedPathAndRoundTrips()
    {
        var route = new CmdPalProtocolRoute.OpenSettings(new OpenSettingsMessage(
            SettingsLinkId: SettingsLinkIds.Extensions.Extension.SearchWeight,
            ExtensionProviderId: "com.example.Provider"));

        var uri = _protocolActivation.CreateUri(route);

        Assert.AreEqual(
            "x-cmdpal://settings/extension/com.example.Provider/search-weight",
            uri.AbsoluteUri);
        Assert.IsTrue(_protocolActivation.TryParse(uri, out var result));
        Assert.AreEqual(route, result);
    }

    [TestMethod]
    public void CreateUri_NonCanonicalSettingsLinkId_Throws()
    {
        var route = new CmdPalProtocolRoute.OpenSettings(new OpenSettingsMessage(
            SettingsLinkId: "External-Command-Links"));

        Assert.ThrowsException<ArgumentException>(() => _protocolActivation.CreateUri(route));
    }

    [TestMethod]
    public void CreateUri_UnknownSettingsLinkId_Throws()
    {
        var route = new CmdPalProtocolRoute.OpenSettings(new OpenSettingsMessage(SettingsLinkId: "unknown"));

        Assert.ThrowsException<ArgumentException>(() => _protocolActivation.CreateUri(route));
    }

    [DataTestMethod]
    [DataRow("x-cmdpal://settings/unknown", "unknown")]
    [DataRow("x-cmdpal://settings/extension", "extension")]
    [DataRow("x-cmdpal://settings/EXTERNAL-COMMAND-LINKS", "external-command-links")]
    public void TryParse_WellFormedSettingsDestination_Parses(string uri, string linkId)
    {
        Assert.IsTrue(_protocolActivation.TryParse(new Uri(uri), out var result));
        Assert.AreEqual(
            new CmdPalProtocolRoute.OpenSettings(new OpenSettingsMessage(SettingsLinkId: linkId)),
            result);
    }

    [TestMethod]
    public void TryParse_UnknownExtensionSettingsTarget_Parses()
    {
        Assert.IsTrue(_protocolActivation.TryParse(
            new Uri("x-cmdpal://settings/extension/provider/unknown"),
            out var result));
        Assert.AreEqual(
            new CmdPalProtocolRoute.OpenSettings(new OpenSettingsMessage(
                SettingsLinkId: "extension/unknown",
                ExtensionProviderId: "provider")),
            result);
    }

    [DataTestMethod]
    [DataRow("x-cmdpal://settings/-background")]
    [DataRow("x-cmdpal://settings/background-")]
    [DataRow("x-cmdpal://settings/bad_target")]
    [DataRow("x-cmdpal://settings/%20background")]
    [DataRow("x-cmdpal://settings/%E2%84%AAeep-previous-query")]
    [DataRow("x-cmdpal://settings/general/external-command-links")]
    public void TryParse_InvalidSettingsDestination_ReturnsFalse(string uri)
    {
        Assert.IsFalse(_protocolActivation.TryParse(new Uri(uri), out var result));
        Assert.IsNull(result);
    }

    [DataTestMethod]
    [DataRow("x-cmdpal://settings/extension/%20provider")]
    [DataRow("x-cmdpal://settings/extension/provider%2Fchild")]
    [DataRow("x-cmdpal://settings/extension/provider/bad_target")]
    [DataRow("x-cmdpal://settings/extension/provider/%E2%84%AAeep")]
    [DataRow("x-cmdpal://settings/extensions/extension/provider")]
    public void TryParse_InvalidExtensionSettingsUri_ReturnsFalse(string uri)
    {
        Assert.IsFalse(_protocolActivation.TryParse(new Uri(uri), out var result));
        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryParse_ExcessivelyLongExtensionSettingsProvider_ReturnsFalse()
    {
        var uri = new Uri($"x-cmdpal://settings/extension/{new string('a', 257)}");

        Assert.IsFalse(_protocolActivation.TryParse(uri, out var result));
        Assert.IsNull(result);
    }

    [TestMethod]
    public void CreateUri_ExtensionProviderOnOtherSettingsPage_Throws()
    {
        var route = new CmdPalProtocolRoute.OpenSettings(new OpenSettingsMessage(
            SettingsLinkId: SettingsLinkIds.General.Page,
            ExtensionProviderId: "provider"));

        Assert.ThrowsException<ArgumentException>(() => _protocolActivation.CreateUri(route));
    }

    [TestMethod]
    public void CreateUri_ExtensionSettingsLinkWithoutProvider_Throws()
    {
        var route = new CmdPalProtocolRoute.OpenSettings(new OpenSettingsMessage(
            SettingsLinkId: SettingsLinkIds.Extensions.Extension.Page));

        Assert.ThrowsException<ArgumentException>(() => _protocolActivation.CreateUri(route));
    }

    [TestMethod]
    public void TryParse_ExcessivelyLongSettingsDestination_ReturnsFalse()
    {
        var uri = new Uri($"x-cmdpal://settings/{new string('a', 65)}");

        Assert.IsFalse(_protocolActivation.TryParse(uri, out var result));
        Assert.IsNull(result);
    }

    [DataTestMethod]
    [DataRow("x-cmdpal://commands/provider")]
    [DataRow("x-cmdpal://commands/provider/command/extra")]
    [DataRow("x-cmdpal://commands/provider/sample%2Fcommand")]
    [DataRow("x-cmdpal://commands/%20/command")]
    [DataRow("x-cmdpal://commands/provider/%20")]
    [DataRow("x-cmdpal://commands/%20provider/command")]
    [DataRow("x-cmdpal://commands/provider/command%20")]
    public void TryParse_InvalidCommandUri_ReturnsFalse(string uri)
    {
        Assert.IsFalse(_protocolActivation.TryParse(new Uri(uri), out var result));
        Assert.IsNull(result);
    }

    [DataTestMethod]
    [DataRow("x-cmdpal://commands/provider/command?unknown=value")]
    [DataRow("x-cmdpal://commands/provider/command?Query=ssh")]
    [DataRow("x-cmdpal://commands/provider/command?query=one&query=two")]
    [DataRow("x-cmdpal://commands/provider/command?query=one&qu%65ry=two")]
    [DataRow("x-cmdpal://commands/provider/command?filter=")]
    [DataRow("x-cmdpal://commands/provider/command?query=")]
    [DataRow("x-cmdpal://commands/provider/command?query=%20%20")]
    [DataRow("x-cmdpal://commands/provider/command?filter=%20running")]
    [DataRow("x-cmdpal://commands/provider/command?query=line%0Abreak")]
    [DataRow("x-cmdpal://commands/provider/command?filter=running&")]
    public void TryParse_InvalidCommandQuery_ReturnsFalse(string uri)
    {
        Assert.IsFalse(_protocolActivation.TryParse(new Uri(uri), out var result));
        Assert.IsNull(result);
    }

    [TestMethod]
    public void TryParse_ExcessivelyLongCommandQuery_ReturnsFalse()
    {
        var uri = new Uri($"x-cmdpal://commands/provider/command?query={new string('a', 1025)}");

        Assert.IsFalse(_protocolActivation.TryParse(uri, out var result));
        Assert.IsNull(result);
    }

    [TestMethod]
    public void CreateUri_CommandRouteWithPathSeparator_Throws()
    {
        var route = new CmdPalProtocolRoute.ExecuteCommand("provider", "nested/command");

        Assert.ThrowsException<ArgumentException>(() => _protocolActivation.CreateUri(route));
    }

    [TestMethod]
    public void CreateUri_CommandRouteWithEmptyListPageOptions_Throws()
    {
        var route = new CmdPalProtocolRoute.ExecuteCommand("provider", "command", new ListPageLaunchOptions());

        Assert.ThrowsException<ArgumentException>(() => _protocolActivation.CreateUri(route));
    }

    [TestMethod]
    public void Policy_MapsEveryKnownRouteToOneActivationAction()
    {
        Assert.IsInstanceOfType<CmdPalProtocolAction.RunInBackground>(
            CmdPalProtocolPolicy.Evaluate(new CmdPalProtocolRoute.Background()));

        var settingsMessage = new OpenSettingsMessage(SettingsPageTags.Gallery, "sample.extension");
        var openSettings = CmdPalProtocolPolicy.Evaluate(new CmdPalProtocolRoute.OpenSettings(settingsMessage));
        Assert.IsInstanceOfType<CmdPalProtocolAction.OpenSettings>(openSettings);
        Assert.AreSame(settingsMessage, ((CmdPalProtocolAction.OpenSettings)openSettings).Message);

        var reload = new CmdPalProtocolRoute.Reload();
        var reloadConsent = CmdPalProtocolPolicy.Evaluate(reload);
        Assert.IsInstanceOfType<CmdPalProtocolAction.RequestConsent>(reloadConsent);
        Assert.AreSame(reload, ((CmdPalProtocolAction.RequestConsent)reloadConsent).Route);

        var execute = new CmdPalProtocolRoute.ExecuteCommand("provider", "command");
        var commandConsent = CmdPalProtocolPolicy.Evaluate(execute);
        Assert.IsInstanceOfType<CmdPalProtocolAction.RequestConsent>(commandConsent);
        Assert.AreSame(execute, ((CmdPalProtocolAction.RequestConsent)commandConsent).Route);
    }

    [DataTestMethod]
    [DataRow("https://extensions/gallery/sample")]
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
