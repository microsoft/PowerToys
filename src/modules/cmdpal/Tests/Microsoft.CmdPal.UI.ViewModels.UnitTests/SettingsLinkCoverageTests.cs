// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using Microsoft.CmdPal.UI.Messages;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class SettingsLinkCoverageTests
{
    private const string SettingsPagesDirectory = "SettingsPages";
    private const string SectionHeaderStyleName = "SettingsSectionHeaderTextBlockStyle";
    private const string TargetAttributeName = "SettingsPageTarget.Id";

    private static readonly HashSet<string> ConfigurationControlNames = new(StringComparer.Ordinal)
    {
        "CheckBox",
        "CheckBoxWithDescriptionControl",
        "ColorPickerButton",
        "ComboBox",
        "NumberBox",
        "PasswordBox",
        "RadioButton",
        "RadioButtons",
        "ShortcutControl",
        "Slider",
        "TextBox",
        "ToggleButton",
        "ToggleSwitch",
    };

    private static readonly SettingsPageDefinition[] SettingsPages =
    [
        new("GeneralPage.xaml", SettingsPageTags.General, RequiresSectionHeaderTargets: true),
        new("AppearancePage.xaml", SettingsPageTags.Appearance, RequiresSectionHeaderTargets: true),
        new("ExtensionsPage.xaml", SettingsPageTags.Extensions),
        new("ExtensionPage.xaml", SettingsPageTags.Extensions, RequiresExtensionProvider: true),
        new("DockSettingsPage.xaml", SettingsPageTags.Dock, RequiresSectionHeaderTargets: true),
    ];

    [TestMethod]
    public void Catalog_CoversEverySettingsConfigurationContainer()
    {
        var destinations = SettingsLinkResolver.Destinations.ToArray();

        foreach (var page in SettingsPages)
        {
            var document = LoadPage(page);
            var anchors = document
                .Descendants()
                .SelectMany(element => element.Attributes()
                    .Where(IsTargetAttribute)
                    .Select(attribute => (Id: attribute.Value, Element: element)))
                .ToArray();

            foreach (var anchor in anchors)
            {
                Assert.IsFalse(
                    string.IsNullOrWhiteSpace(anchor.Id),
                    $"{GetLocation(page, anchor.Element)} has an empty settings target ID.");
            }

            var duplicate = anchors
                .GroupBy(anchor => anchor.Id, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault(group => group.Count() > 1);
            Assert.IsNull(duplicate, $"{page.FileName} has duplicate settings target '{duplicate?.Key}'.");

            foreach (var container in document.Descendants().Where(IsConfigurationContainer))
            {
                var anchor = container
                    .AncestorsAndSelf()
                    .SelectMany(element => element.Attributes().Where(IsTargetAttribute))
                    .FirstOrDefault();
                Assert.IsNotNull(
                    anchor,
                    $"{GetLocation(page, container)} configuration container '{container.Name.LocalName}' is not covered by a settings target.");
            }

            var pageDestinations = destinations.Where(destination => BelongsToPage(destination, page)).ToArray();
            Assert.IsTrue(
                pageDestinations.Any(destination => destination.ElementId is null),
                $"{page.FileName} has no page-level settings link.");

            foreach (var anchor in anchors)
            {
                Assert.IsTrue(
                    pageDestinations.Any(destination =>
                        string.Equals(destination.ElementId, anchor.Id, StringComparison.OrdinalIgnoreCase)),
                    $"{GetLocation(page, anchor.Element)} target '{anchor.Id}' is not registered.");
            }

            foreach (var destination in pageDestinations.Where(destination => destination.ElementId is not null))
            {
                Assert.IsTrue(
                    anchors.Any(anchor =>
                        string.Equals(anchor.Id, destination.ElementId, StringComparison.OrdinalIgnoreCase)),
                    $"Settings link '{destination.LinkId}' points to missing target '{destination.ElementId}' in {page.FileName}.");
            }
        }

        foreach (var destination in destinations)
        {
            Assert.AreEqual(
                1,
                SettingsPages.Count(page => BelongsToPage(destination, page)),
                $"Settings link '{destination.LinkId}' does not map to exactly one tested settings page.");
        }
    }

    [TestMethod]
    public void Catalog_AllSettingsLinksRoundTripThroughProtocol()
    {
        var protocolActivation = new CmdPalProtocolActivation(new SettingsLinkResolver());

        foreach (var destination in SettingsLinkResolver.Destinations)
        {
            var route = new CmdPalProtocolRoute.OpenSettings(new OpenSettingsMessage(
                SettingsLinkId: destination.LinkId,
                ExtensionProviderId: destination.RequiresExtensionProvider ? "com.example.Provider" : null));

            var uri = protocolActivation.CreateUri(route);

            Assert.IsTrue(protocolActivation.TryParse(uri, out var parsed), destination.LinkId);
            Assert.AreEqual(route, parsed, destination.LinkId);
        }
    }

    [TestMethod]
    public void Catalog_CoversEveryPrimarySettingsSectionHeader()
    {
        foreach (var page in SettingsPages.Where(page => page.RequiresSectionHeaderTargets))
        {
            foreach (var header in LoadPage(page).Descendants().Where(IsSectionHeader))
            {
                var anchor = header
                    .AncestorsAndSelf()
                    .SelectMany(element => element.Attributes().Where(IsTargetAttribute))
                    .FirstOrDefault();
                Assert.IsNotNull(
                    anchor,
                    $"{GetLocation(page, header)} section header is not covered by a settings target.");
            }
        }
    }

    [TestMethod]
    public void Catalog_EveryCurrentLocationHasPreferredLink()
    {
        var resolver = new SettingsLinkResolver();
        var locations = SettingsLinkResolver.Destinations.ToArray().GroupBy(destination => new
        {
            destination.PageTag,
            destination.ElementId,
            destination.RequiresExtensionProvider,
        });

        foreach (var location in locations)
        {
            Assert.IsTrue(
                resolver.TryResolveDestination(
                    location.Key.PageTag,
                    location.Key.ElementId,
                    location.Key.RequiresExtensionProvider,
                    out var destination),
                $"Settings location '{location.Key.PageTag}/{location.Key.ElementId}' has no preferred link.");
            Assert.IsTrue(
                location.Any(candidate => string.Equals(
                    candidate.LinkId,
                    destination.LinkId,
                    StringComparison.Ordinal)),
                $"Settings location '{location.Key.PageTag}/{location.Key.ElementId}' resolved outside its aliases.");
        }
    }

    [TestMethod]
    [DataRow(SettingsLinkIds.General.About, "SettingsExpander")]
    [DataRow(SettingsLinkIds.General.AboutSection, "TextBlock")]
    public void Catalog_AboutLinksPreserveCardAndSectionTargets(string linkId, string expectedControl)
    {
        var resolver = new SettingsLinkResolver();
        Assert.IsTrue(resolver.TryResolve(linkId, out var destination));
        Assert.AreEqual(SettingsPageTags.General, destination.PageTag);

        var page = SettingsPages.Single(page => page.PageTag == SettingsPageTags.General);
        var target = LoadPage(page).Descendants().Single(element => element.Attributes().Any(attribute =>
            IsTargetAttribute(attribute) && attribute.Value == destination.ElementId));

        Assert.AreEqual(expectedControl, target.Name.LocalName);
    }

    private static bool BelongsToPage(SettingsLinkDestination destination, SettingsPageDefinition page) =>
        string.Equals(destination.PageTag, page.PageTag, StringComparison.Ordinal) &&
        destination.RequiresExtensionProvider == page.RequiresExtensionProvider;

    private static string GetLocation(SettingsPageDefinition page, XObject node)
    {
        var lineInfo = (IXmlLineInfo)node;
        return lineInfo.HasLineInfo() ? $"{page.FileName}:{lineInfo.LineNumber}" : page.FileName;
    }

    private static bool IsConfigurationContainer(XElement element) =>
        element.Name.LocalName is "SettingsCard" or "SettingsExpander" &&
        element.DescendantsAndSelf().Any(IsConfigurationControl);

    private static bool IsConfigurationControl(XElement element) =>
        ConfigurationControlNames.Contains(element.Name.LocalName) ||
        element.Attributes().Any(attribute =>
            attribute.Value.Contains("Mode=TwoWay", StringComparison.Ordinal));

    private static bool IsSectionHeader(XElement element) =>
        element.Attributes().Any(attribute =>
            attribute.Value.Contains(SectionHeaderStyleName, StringComparison.Ordinal));

    private static bool IsTargetAttribute(XAttribute attribute) =>
        string.Equals(attribute.Name.LocalName, TargetAttributeName, StringComparison.Ordinal);

    private static XDocument LoadPage(SettingsPageDefinition page)
    {
        var path = Path.Combine(AppContext.BaseDirectory, SettingsPagesDirectory, page.FileName);
        Assert.IsTrue(File.Exists(path), $"Missing settings page test input '{path}'.");
        return XDocument.Load(path, LoadOptions.SetLineInfo);
    }

    private sealed record SettingsPageDefinition(
        string FileName,
        string PageTag,
        bool RequiresExtensionProvider = false,
        bool RequiresSectionHeaderTargets = false);
}
