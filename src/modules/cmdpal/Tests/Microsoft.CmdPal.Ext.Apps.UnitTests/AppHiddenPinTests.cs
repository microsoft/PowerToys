// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CommandPalette.Extensions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppHiddenPinTests : AppsTestBase
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void SavedRedirect_HiddenTargetRemainsUsableWithoutDiscovery(bool patternHidden)
    {
        var hidden = CreateItem("Hidden", @"E:\Apps\Hidden.exe");
        var legacyId = AppCommand.GenerateId(hidden.App.Name, hidden.App.Subtitle, hidden.App.LaunchTarget);
        hidden.App.CommandIds = [legacyId];
        var oldId = AppIdentity.ForCommand("win32:old-location");
        var snapshot = new AppListItemSnapshot(
            [],
            patternHidden ? [] : [hidden],
            patternHiddenItems: patternHidden ? [hidden] : [],
            commandAliases: new Dictionary<string, string> { [oldId] = hidden.Command!.Id });
        var source = new Mock<IAppListItemSource>();
        source.Setup(service => service.GetSnapshot()).Returns(snapshot);
        using var page = new AllAppsPage(source.Object, TestDataHelper.CreateFuzzyMatcherProvider());
        using var provider = new AllAppsCommandProvider(page, source.Object, Settings);

        Assert.AreSame(hidden, provider.GetCommandItem(hidden.Command.Id));
        foreach (var id in new[] { oldId, legacyId })
        {
            var saved = provider.GetCommandItem(id);
            Assert.IsNotNull(saved);
            Assert.AreEqual(hidden.Title, saved.Title);
            Assert.AreEqual(id, saved.Command!.Id);
            Assert.AreSame(hidden.Icon, saved.Icon);
            Assert.IsInstanceOfType<IInvokableCommand>(saved.Command);
            Assert.AreSame(hidden, snapshot.GetApp(id));
            Assert.IsNull(snapshot.GetVisibleApp(id));
        }

        Assert.AreEqual(0, snapshot.VisibleItems.Count);
        Assert.AreEqual(0, page.GetItems().OfType<AppListItem>().Count());
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void HiddenCanonicalId_SavedMarkerInDifferentCaseCannotOverrideCurrentApp(bool patternHidden)
    {
        var hidden = CreateItem("Hidden", @"E:\Apps\Hidden.exe");
        var id = hidden.Command!.Id;
        var snapshot = new AppListItemSnapshot(
            [],
            patternHidden ? [] : [hidden],
            patternHiddenItems: patternHidden ? [hidden] : [],
            commandAliases: new Dictionary<string, string> { [id.ToUpperInvariant()] = string.Empty });

        foreach (var requested in new[] { id, id.ToUpperInvariant() })
        {
            Assert.AreSame(hidden, snapshot.GetApp(requested));
            Assert.AreEqual(requested, snapshot.GetCommandItem(requested)?.Command?.Id);
            Assert.IsNull(snapshot.GetVisibleApp(requested));
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void HiddenAndVisibleApps_SharedAliasResolvesVisibleApp(bool patternHidden)
    {
        const string sharedId = "Shared editor_123";
        var visible = CreateItem("Visible", @"C:\Apps\Visible.exe");
        var hidden = CreateItem("Hidden", @"E:\Apps\Hidden.exe");
        visible.App.CommandIds = [sharedId];
        hidden.App.CommandIds = [sharedId];
        var snapshot = new AppListItemSnapshot([visible], patternHidden ? [] : [hidden], patternHidden ? [hidden] : []);

        Assert.AreSame(visible, snapshot.GetApp(sharedId));
        Assert.AreSame(visible, snapshot.GetVisibleApp(sharedId));
        Assert.AreEqual(sharedId, snapshot.GetCommandItem(sharedId)?.Command?.Id);
        Assert.AreSame(visible, snapshot.GetVisibleApp(visible.Command!.Id));
        Assert.AreSame(hidden, snapshot.GetApp(hidden.Command!.Id));
        Assert.IsNull(snapshot.GetVisibleApp(hidden.Command.Id));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void PackagedApps_SharedReleasedIdPrefersVisibleAppUnlessSavedAmbiguous(bool patternHidden, bool savedAmbiguous)
    {
        var visiblePayload = new PackagedAppPayload { Name = "Editor", Description = "Edit documents", AppUserModelId = "Contoso.Editor_123!App" };
        var hiddenPayload = visiblePayload with { AppUserModelId = "Contoso.EditorPreview_123!App" };
        var releasedId = visiblePayload.GetCommandId();
        Assert.AreEqual(releasedId, hiddenPayload.GetCommandId());
        var visibleApp = visiblePayload.ToAppItem();
        visibleApp.CatalogId = AppIdentity.ForPackaged(visiblePayload.AppUserModelId);
        visibleApp.CommandIds = [releasedId];
        var hiddenApp = hiddenPayload.ToAppItem();
        hiddenApp.CatalogId = AppIdentity.ForPackaged(hiddenPayload.AppUserModelId);
        hiddenApp.CommandIds = [releasedId];
        var visible = new AppListItem(visibleApp, useThumbnails: false);
        var hidden = new AppListItem(hiddenApp, useThumbnails: false);
        var snapshot = new AppListItemSnapshot(
            [visible],
            patternHidden ? [] : [hidden],
            patternHidden ? [hidden] : [],
            commandAliases: savedAmbiguous ? new Dictionary<string, string> { [releasedId] = string.Empty } : null);

        Assert.AreSame(savedAmbiguous ? null : visible, snapshot.GetApp(releasedId));
        Assert.AreSame(savedAmbiguous ? null : visible, snapshot.GetVisibleApp(releasedId));
        Assert.AreEqual(savedAmbiguous ? null : releasedId, snapshot.GetCommandItem(releasedId)?.Command?.Id);
        Assert.AreSame(visible, snapshot.GetVisibleApp(visible.Command!.Id));
        Assert.AreSame(hidden, snapshot.GetApp(hidden.Command!.Id));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public void PackagedCommandIdCaseCollision_PrefersVisibleApp(bool patternHidden, bool canonicalCollision)
    {
        const string sharedAumid = "Contoso.Shared_123!App";
        var visible = CreatePackagedItem("Visible", canonicalCollision ? sharedAumid : "Contoso.Visible_123!App");
        var hidden = CreatePackagedItem("Hidden", sharedAumid.ToLowerInvariant());
        var sharedId = AppIdentity.ForCommand(AppIdentity.ForPackaged(sharedAumid));
        visible.App.CommandIds = [sharedId];
        hidden.App.CommandIds = [sharedId.ToUpperInvariant()];
        var snapshot = new AppListItemSnapshot([visible], patternHidden ? [] : [hidden], patternHidden ? [hidden] : []);

        // A hidden canonical ID is indexed before the visible alias when canonicalCollision is false.
        foreach (var requested in new[] { sharedId, sharedId.ToUpperInvariant(), sharedId.ToLowerInvariant() })
        {
            Assert.AreSame(visible, snapshot.GetApp(requested));
            Assert.AreSame(visible, snapshot.GetVisibleApp(requested));
            var command = snapshot.GetCommandItem(requested);
            Assert.IsNotNull(command);
            Assert.AreEqual(visible.Title, command.Title);
            Assert.AreEqual(requested, command.Command!.Id);
        }

        Assert.AreSame(visible, snapshot.GetVisibleApp(visible.Command!.Id));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PackagedCaseVariantAlias_SavedAmbiguityRemainsBlocked(bool patternHidden)
    {
        const string sharedId = "app-v1-packaged-Contoso.Shared_123!App";
        var visible = CreatePackagedItem("Visible", "Contoso.Visible_123!App");
        var hidden = CreatePackagedItem("Hidden", "Contoso.Hidden_123!App");
        visible.App.CommandIds = [sharedId];
        hidden.App.CommandIds = [sharedId.ToUpperInvariant()];
        var snapshot = new AppListItemSnapshot(
            [visible],
            patternHidden ? [] : [hidden],
            patternHidden ? [hidden] : [],
            commandAliases: new Dictionary<string, string> { [sharedId.ToLowerInvariant()] = string.Empty });

        foreach (var requested in new[] { sharedId, sharedId.ToUpperInvariant(), sharedId.ToLowerInvariant() })
        {
            Assert.IsNull(snapshot.GetApp(requested));
            Assert.IsNull(snapshot.GetVisibleApp(requested));
            Assert.IsNull(snapshot.GetCommandItem(requested));
        }

        Assert.AreSame(visible, snapshot.GetVisibleApp(visible.Command!.Id));
        Assert.AreSame(hidden, snapshot.GetApp(hidden.Command!.Id));
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void SameVisibility_SharedAliasesRemainAmbiguous(bool hidden, bool typed)
    {
        var sharedId = typed ? "app-v1-packaged-Contoso.Shared_123!App" : "Shared editor_123";
        var first = CreateItem("First", @"C:\Apps\First.exe");
        var second = CreateItem("Second", @"C:\Apps\Second.exe");
        first.App.CommandIds = [sharedId];
        second.App.CommandIds = [typed ? sharedId.ToUpperInvariant() : sharedId];
        var snapshot = new AppListItemSnapshot(hidden ? [] : [first, second], hidden ? [first, second] : []);

        Assert.IsNull(snapshot.GetApp(sharedId));
        Assert.IsNull(snapshot.GetVisibleApp(sharedId));
        Assert.IsNull(snapshot.GetCommandItem(sharedId));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void HiddenAliasResolutionChange_ReloadsSavedCommandsWithoutChangingDiscoveryRows(bool patternHidden)
    {
        const string legacyId = "Old editor_123";
        var hidden = CreateItem("Hidden", @"E:\Apps\Hidden.exe");
        var snapshot = new AppListItemSnapshot([], patternHidden ? [] : [hidden], patternHidden ? [hidden] : []);
        var source = new Mock<IAppListItemSource>();
        source.Setup(service => service.GetSnapshot()).Returns(() => snapshot);
        using var page = new AllAppsPage(source.Object, TestDataHelper.CreateFuzzyMatcherProvider());
        using var provider = new AllAppsCommandProvider(page, source.Object, Settings);
        var notifications = 0;
        provider.ItemsChanged += (_, _) => notifications++;
        Assert.IsNull(provider.GetCommandItem(legacyId));

        snapshot = new AppListItemSnapshot(
            snapshot.VisibleItems,
            snapshot.HiddenItems,
            snapshot.PatternHiddenItems,
            commandAliases: new Dictionary<string, string> { [legacyId] = hidden.Command!.Id });
        source.Raise(service => service.Changed += null, source.Object, EventArgs.Empty);
        Assert.AreEqual(1, notifications);
        Assert.AreEqual(hidden.Title, provider.GetCommandItem(legacyId)?.Title);
        Assert.AreEqual(legacyId, provider.GetCommandItem(legacyId)?.Command?.Id);

        snapshot = new AppListItemSnapshot(
            snapshot.VisibleItems,
            snapshot.HiddenItems,
            snapshot.PatternHiddenItems,
            commandAliases: new Dictionary<string, string> { [legacyId] = string.Empty });
        source.Raise(service => service.Changed += null, source.Object, EventArgs.Empty);
        Assert.AreEqual(2, notifications);
        Assert.IsNull(provider.GetCommandItem(legacyId));
        Assert.AreEqual(0, snapshot.VisibleItems.Count);
    }

    [TestMethod]
    public void HiddenRowsChange_ReloadsSavedCommandsWithoutSearchOnlyReloads()
    {
        var hidden = CreateItem("Hidden", @"E:\Apps\Hidden.exe");
        var snapshot = new AppListItemSnapshot([], [hidden]);
        var source = new Mock<IAppListItemSource>();
        source.Setup(service => service.GetSnapshot()).Returns(() => snapshot);
        using var page = new AllAppsPage(source.Object, TestDataHelper.CreateFuzzyMatcherProvider());
        using var provider = new AllAppsCommandProvider(page, source.Object, Settings);
        var notifications = 0;
        provider.ItemsChanged += (_, _) => notifications++;

        var renamed = CreateItem("Renamed", hidden.App.LaunchTarget);
        snapshot = new AppListItemSnapshot([], [renamed]);
        source.Raise(service => service.Changed += null, source.Object, EventArgs.Empty);
        Assert.AreEqual(1, notifications);
        Assert.AreEqual(renamed.Title, provider.GetCommandItem(hidden.Command!.Id)?.Title);

        snapshot = new AppListItemSnapshot([], [renamed], executableNameMatchMode: ExecutableNameMatchMode.Disabled);
        source.Raise(service => service.Changed += null, source.Object, EventArgs.Empty);
        snapshot = snapshot.WithExecutionAliasOwners(ImmutableDictionary<string, string>.Empty.Add("hidden.exe", "Family!App"));
        source.Raise(service => service.Changed += null, source.Object, EventArgs.Empty);
        source.Raise(service => service.Changed += null, source.Object, EventArgs.Empty);
        Assert.AreEqual(1, notifications);
    }

    private static AppListItem CreateItem(string name, string path)
    {
        return new(new AppItem { Name = name, CatalogId = $"win32:{path}|args:", LaunchTarget = path }, useThumbnails: false);
    }

    private static AppListItem CreatePackagedItem(string name, string aumid)
    {
        var payload = new PackagedAppPayload { Name = name, AppUserModelId = aumid };
        var app = payload.ToAppItem();
        app.CatalogId = AppIdentity.ForPackaged(aumid);
        return new AppListItem(app, useThumbnails: false);
    }
}
