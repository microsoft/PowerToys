// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class SettingsLinkResolverTests
{
    private readonly ISettingsLinkResolver _resolver = new SettingsLinkResolver();

    [TestMethod]
    public void Resolve_KnownLink_ReturnsExactDestination()
    {
        var result = _resolver.Resolve("EXTERNAL-COMMAND-LINKS", null);

        Assert.AreEqual(SettingsLinkIds.General.ExternalCommandLinks, result.Destination.LinkId);
        Assert.AreEqual(SettingsLinkFallback.None, result.Fallback);
    }

    [DataTestMethod]
    [DataRow(null, SettingsLinkIds.General.Page)]
    [DataRow("unknown", SettingsLinkIds.General.Page)]
    public void Resolve_UnknownLink_UsesAvailablePage(string? linkId, string pageLinkId)
    {
        var result = _resolver.Resolve(linkId, null);

        Assert.AreEqual(pageLinkId, result.Destination.LinkId);
        Assert.AreEqual(SettingsLinkFallback.UnknownLink, result.Fallback);
    }

    [TestMethod]
    public void Resolve_UnknownExtensionTargetWithProvider_UsesProviderPage()
    {
        var result = _resolver.Resolve("extension/serch-weight", "com.example.Provider");

        Assert.AreEqual(SettingsLinkIds.Extensions.Extension.Page, result.Destination.LinkId);
        Assert.IsTrue(result.Destination.RequiresExtensionProvider);
        Assert.AreEqual(SettingsLinkFallback.UnknownLink, result.Fallback);
    }

    [TestMethod]
    public void Resolve_UnknownExtensionTargetWithoutProvider_UsesExtensionsPage()
    {
        var result = _resolver.Resolve("extension/serch-weight", null);

        Assert.AreEqual(SettingsLinkIds.Extensions.Page, result.Destination.LinkId);
        Assert.AreEqual(SettingsLinkFallback.UnknownProvider, result.Fallback);
    }

    [TestMethod]
    public void Resolve_ExtensionLinkWithoutProvider_UsesExtensionsPage()
    {
        var result = _resolver.Resolve(SettingsLinkIds.Extensions.Extension.Page, null);

        Assert.AreEqual(SettingsLinkIds.Extensions.Page, result.Destination.LinkId);
        Assert.AreEqual(SettingsLinkFallback.UnknownProvider, result.Fallback);
    }

    [DataTestMethod]
    [DataRow(true, false, SettingsLinkFallback.HiddenTarget)]
    [DataRow(true, true, SettingsLinkFallback.HiddenTarget)]
    [DataRow(false, true, SettingsLinkFallback.DisabledProvider)]
    [DataRow(false, false, SettingsLinkFallback.MissingTarget)]
    public void ClassifyUnavailableTarget_DistinguishesReasons(
        bool isHidden,
        bool extensionDisabled,
        SettingsLinkFallback expected)
    {
        Assert.AreEqual(expected, _resolver.ClassifyUnavailableTarget(isHidden, extensionDisabled));
    }

    [TestMethod]
    public void TryResolve_ReturnsCanonicalLinkAndCurrentDestination()
    {
        Assert.IsTrue(_resolver.TryResolve("EXTERNAL-COMMAND-LINKS", out var destination));

        Assert.AreEqual(SettingsLinkIds.General.ExternalCommandLinks, destination.LinkId);
        Assert.AreEqual(SettingsPageTags.General, destination.PageTag);
        Assert.AreEqual("external-command-links", destination.ElementId);
        Assert.IsFalse(destination.RequiresExtensionProvider);
    }

    [TestMethod]
    public void TryResolve_DistinguishesLinksWithTheSameElementId()
    {
        Assert.IsTrue(_resolver.TryResolve(SettingsLinkIds.Appearance.Theme, out var appearance));
        Assert.IsTrue(_resolver.TryResolve(SettingsLinkIds.Dock.Theme, out var dock));

        Assert.AreEqual(SettingsPageTags.Appearance, appearance.PageTag);
        Assert.AreEqual(SettingsPageTags.Dock, dock.PageTag);
        Assert.AreEqual("theme", appearance.ElementId);
        Assert.AreEqual("theme", dock.ElementId);
    }

    [TestMethod]
    public void TryResolve_ExtensionLinkRequiresProviderParameter()
    {
        Assert.IsTrue(_resolver.TryResolve(SettingsLinkIds.Extensions.Extension.Settings, out var destination));

        Assert.AreEqual(SettingsPageTags.Extensions, destination.PageTag);
        Assert.AreEqual("settings", destination.ElementId);
        Assert.IsTrue(destination.RequiresExtensionProvider);
    }

    [TestMethod]
    public void TryResolve_FallbackOrderOpensDialogAfterNavigation()
    {
        Assert.IsTrue(_resolver.TryResolve(SettingsLinkIds.Extensions.FallbackOrder, out var destination));

        Assert.AreEqual(SettingsPageTags.Extensions, destination.PageTag);
        Assert.AreEqual("more", destination.ElementId);
        Assert.AreEqual(SettingsLinkAction.OpenFallbackOrder, destination.Action);
    }

    [TestMethod]
    [DataRow(SettingsPageTags.General, null, false, SettingsLinkIds.General.Page)]
    [DataRow(SettingsPageTags.General, "activation-section", false, SettingsLinkIds.General.Activation)]
    [DataRow(SettingsPageTags.Appearance, "THEME", false, SettingsLinkIds.Appearance.Theme)]
    [DataRow(SettingsPageTags.Appearance, "layout-section", false, SettingsLinkIds.Appearance.Layout)]
    [DataRow(SettingsPageTags.Dock, "theme", false, SettingsLinkIds.Dock.Theme)]
    [DataRow(SettingsPageTags.Dock, "behavior-section", false, SettingsLinkIds.Dock.BehaviorSection)]
    [DataRow(SettingsPageTags.Extensions, "more", false, SettingsLinkIds.Extensions.FallbackOrder)]
    [DataRow(SettingsPageTags.Extensions, null, true, SettingsLinkIds.Extensions.Extension.Page)]
    [DataRow(SettingsPageTags.Extensions, "settings", true, SettingsLinkIds.Extensions.Extension.Settings)]
    public void TryResolveDestination_ReturnsPreferredLink(
        string pageTag,
        string? elementId,
        bool requiresExtensionProvider,
        string expectedLinkId)
    {
        Assert.IsTrue(_resolver.TryResolveDestination(
            pageTag,
            elementId,
            requiresExtensionProvider,
            out var destination));

        Assert.AreEqual(expectedLinkId, destination.LinkId);
    }

    [TestMethod]
    public void TryResolveDestination_RejectsUnknownLocation()
    {
        Assert.IsFalse(_resolver.TryResolveDestination(
            SettingsPageTags.General,
            "missing",
            requiresExtensionProvider: false,
            out var destination));
        Assert.IsNull(destination);
    }
}
