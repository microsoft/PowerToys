// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

public partial class AppCatalogTests
{
    private const string FirstPwaAumid = "www.youtube.com-A512A4D6_pd8mbgmqs65xy!App";
    private const string SecondPwaAumid = "www.youtube.com-70053563_pd8mbgmqs65xy!App";
    private const string FirstPwaPackageFullName = "www.youtube.com-A512A4D6_1.0.0.0_neutral__pd8mbgmqs65xy";
    private const string SecondPwaPackageFullName = "www.youtube.com-70053563_1.0.0.5_neutral__pd8mbgmqs65xy";

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    [DataRow(true, true)]
    public async Task RefreshAsync_EquivalentEdgePwasPreserveIdsAndUsePreferredPayload(bool reverseOrder, bool preferFirst)
    {
        var first = CreateEdgePwaCatalogItem(FirstPwaAumid, priority: preferFirst ? -1 : 0);
        var second = CreateEdgePwaCatalogItem(SecondPwaAumid);
        var preferred = preferFirst ? first : second;
        var other = preferFirst ? second : first;
        var shortcut = TestDataHelper.CreateTestWin32Metadata("YouTube shortcut", @"C:\Desktop\YouTube.lnk");
        shortcut.AppType = Win32AppType.ShortcutApplication;
        shortcut.PackagedAppUserModelId = ((PackagedAppPayload)other.Payload).AppUserModelId;
        var shortcutItem = CreateWin32CatalogItem(shortcut, "win32:youtube-shortcut", 20, "desktop");
        using var packagedSource = new TestAppSource("packaged", reverseOrder ? [second, first] : [first, second]);
        using var shortcutSource = new TestAppSource("desktop", [shortcutItem]);
        using var catalog = CreateCatalog(reverseOrder ? [packagedSource, shortcutSource] : [shortcutSource, packagedSource], new TestCache(null));

        await catalog.RefreshAsync();

        var snapshot = catalog.GetSnapshot();
        var app = snapshot.Items.Single();
        Assert.AreEqual(preferred.Identity, app.CatalogId);
        Assert.AreEqual(((PackagedAppPayload)preferred.Payload).AppUserModelId, app.AppUserModelId);
        Assert.IsTrue(app.IsPackaged);
        CollectionAssert.Contains(app.MatchTerms.ToArray(), shortcut.Name);
        var commands = app.Commands;
        Assert.IsNotNull(commands);
        var uninstall = commands.OfType<CommandContextItem>()
            .Select(item => item.Command)
            .OfType<UninstallApplicationConfirmation>()
            .Single();
        var confirmation = uninstall.Invoke().Args as ConfirmationArgs;
        Assert.IsNotNull(confirmation);
        Assert.IsInstanceOfType<UninstallApplicationCommand>(confirmation.PrimaryCommand);

        // Inspect the queued target without invoking removal against installed packages.
        var packageTarget = typeof(UninstallApplicationCommand).GetField("_packageFullName", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(packageTarget);
        Assert.AreEqual(((PackagedAppPayload)preferred.Payload).PackageFullName, packageTarget.GetValue(confirmation.PrimaryCommand));
        Assert.AreNotEqual(((PackagedAppPayload)other.Payload).PackageFullName, packageTarget.GetValue(confirmation.PrimaryCommand));
        var row = new AppListItem(app, useThumbnails: false);
        var rows = new AppListItemSnapshot([row], [], commandAliases: snapshot.CommandAliases);
        foreach (var item in new[] { first, second, shortcutItem })
        {
            Assert.AreSame(row, rows.GetApp(AppIdentity.ForCommand(item.Identity)));
            Assert.AreSame(row, rows.GetApp(item.Payload.GetCommandId()));
        }

        Assert.AreEqual(AppIdentity.ForPackaged(FirstPwaAumid), first.Identity, "Source entries must retain their original identity.");
        Assert.AreEqual(AppIdentity.ForPackaged(SecondPwaAumid), second.Identity);
    }

    [TestMethod]
    [DataRow("publisher")]
    [DataRow("host")]
    [DataRow("host-publisher")]
    [DataRow("runtime")]
    [DataRow("parameters-case")]
    [DataRow("user-data-directory")]
    [DataRow("profile")]
    [DataRow("url-case")]
    [DataRow("url-query")]
    [DataRow("missing")]
    [DataRow("incomplete")]
    public async Task RefreshAsync_DifferentOrUnknownEdgePwaLaunchesRemainSeparate(string difference)
    {
        var info = TestDataHelper.CreateEdgePwaLaunchInfo();
        var parameters = difference == "parameters-case"
            ? info.Parameters.Replace("MSEDGE", "msedge", StringComparison.Ordinal)
            : info.Parameters + " --user-data-dir=\"C:\\Profiles\\Other\"";
        var different = difference switch
        {
            "publisher" => info with { PackagePublisher = "CN=Other Publisher" },
            "host" => info with { HostPackageName = "Microsoft.MicrosoftEdge.Beta" },
            "host-publisher" => info with { HostPackagePublisher = "CN=Other Publisher" },
            "runtime" => info with { HostId = "Other" },
            "parameters-case" or "user-data-directory" => info with
            {
                Parameters = parameters,
                LaunchContext = info.LaunchContext.Replace(info.Parameters, parameters, StringComparison.Ordinal),
            },
            "profile" => info with { LaunchContext = info.LaunchContext.Replace("?Default;", "?Work;", StringComparison.Ordinal) },
            "url-case" => info with { LaunchContext = info.LaunchContext.Replace("feature=ytca", "feature=YTCA", StringComparison.Ordinal) },
            "url-query" => info with { LaunchContext = info.LaunchContext + "&account=work" },
            "missing" => null,
            "incomplete" => new EdgePwaLaunchInfo(),
            _ => throw new ArgumentOutOfRangeException(nameof(difference)),
        };
        using var source = new TestAppSource(
            "packaged",
            [CreateEdgePwaCatalogItem(FirstPwaAumid, info), CreateEdgePwaCatalogItem(SecondPwaAumid, different)]);
        using var catalog = CreateCatalog([source], new TestCache(null));

        await catalog.RefreshAsync();

        Assert.AreEqual(2, catalog.GetSnapshot().Items.Count);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RefreshAsync_RemovingPreferredEdgePwaKeepsPinsAndVisibility(bool hidden)
    {
        var first = CreateEdgePwaCatalogItem(FirstPwaAumid);
        var second = CreateEdgePwaCatalogItem(SecondPwaAumid);
        var aliases = CreateCommandAliases();
        var legacyId = first.Payload.GetCommandId();
        aliases.SetSnapshot(new Dictionary<string, string> { [legacyId] = string.Empty }.ToFrozenDictionary(StringComparer.Ordinal));
        using var source = new TestAppSource("packaged", [first, second]);
        using var catalog = CreateCatalog([source], new TestCache(null), commandAliases: aliases);
        await catalog.RefreshAsync();
        Assert.AreEqual(second.Identity, catalog.GetSnapshot().Items.Single().CatalogId);
        if (hidden)
        {
            await catalog.SetAppHiddenAsync(second.Identity, hidden: true);
        }

        source.SetItems([first]);
        await catalog.RefreshAsync();

        var snapshot = catalog.GetSnapshot();
        var app = (hidden ? snapshot.HiddenItems : snapshot.Items).Single();
        Assert.AreEqual(first.Identity, app.CatalogId);
        Assert.AreEqual(FirstPwaAumid, app.AppUserModelId);
        var row = new AppListItem(app, useThumbnails: false);
        var rows = new AppListItemSnapshot(hidden ? [] : [row], hidden ? [row] : [], commandAliases: snapshot.CommandAliases);
        Assert.AreSame(row, rows.GetApp(AppIdentity.ForCommand(first.Identity)));
        Assert.AreSame(row, rows.GetApp(AppIdentity.ForCommand(second.Identity)));
        Assert.IsNull(rows.GetApp(legacyId), "An existing ambiguity marker must not be cleared by deduplication.");
    }

    private static AppCatalogItem CreateEdgePwaCatalogItem(string aumid, int priority = 0)
    {
        return CreateEdgePwaCatalogItem(aumid, TestDataHelper.CreateEdgePwaLaunchInfo(), priority);
    }

    private static AppCatalogItem CreateEdgePwaCatalogItem(string aumid, EdgePwaLaunchInfo? info, int priority = 0)
    {
        return new AppCatalogItem(
            AppIdentity.ForPackaged(aumid),
            priority,
            new AppCatalogSourceReference("packaged", aumid),
            ["YouTube"],
            new PackagedAppPayload
            {
                Name = "YouTube",
                Description = "YouTube",
                AppUserModelId = aumid,
                PackageFullName = aumid == FirstPwaAumid ? FirstPwaPackageFullName : SecondPwaPackageFullName,
                EdgePwaLaunch = info,
            });
    }
}
