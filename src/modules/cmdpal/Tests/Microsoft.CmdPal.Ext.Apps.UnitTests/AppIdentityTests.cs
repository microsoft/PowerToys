// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppIdentityTests
{
    [TestMethod]
    public void LaunchTargets_UseKindSpecificEqualityAndHashing()
    {
        var path = LaunchTarget.FilePath(@"C:\Apps\App.exe");
        var equivalentPath = LaunchTarget.FilePath(@"c:\apps\Subfolder\..\app.EXE");
        Assert.AreEqual(path, equivalentPath);
        Assert.AreEqual(path.GetHashCode(), equivalentPath.GetHashCode());
        Assert.AreEqual(1, new[] { path, equivalentPath }.Distinct().Count());

        var url = LaunchTarget.Url("com.epicgames.launcher://apps/Example?action=launch");
        var otherUrl = LaunchTarget.Url("com.epicgames.launcher://apps/example?action=launch");
        Assert.AreNotEqual(url, otherUrl);
        Assert.AreEqual(2, new[] { url, otherUrl }.Distinct().Count());
        Assert.AreNotEqual(url.IdentityToken, otherUrl.IdentityToken);
        Assert.IsFalse(string.Equals(url.IdentityToken, otherUrl.IdentityToken, StringComparison.OrdinalIgnoreCase));
        Assert.AreNotEqual(path, LaunchTarget.Url(path.Value));
        Assert.AreNotEqual(LaunchTarget.Url(path.Value), path);
    }

    [TestMethod]
    public void CommandIds_UseActualFilenameWithExtensionAndPreserveArguments()
    {
        const string identity = @"win32:C:\Apps\my-app.exe|args:2D2D54657374";
        var id = AppIdentity.ForCommand(identity);
        StringAssert.StartsWith(id, "app-v1-win32-my-app.exe-");
        Assert.AreEqual(id, AppIdentity.ForCommand(identity.ToUpperInvariant()));
        Assert.AreNotEqual(id, AppIdentity.ForCommand(@"win32:C:\Apps\my-app.exe|args:2D2D74657374"));
        StringAssert.StartsWith(AppIdentity.ForCommand(@"win32:C:\Apps\my-app.cmd|args:"), "app-v1-win32-my-app.cmd-");
        Assert.AreEqual("app-v1-packaged-Contoso.App_123!App", AppIdentity.ForCommand("packaged:Contoso.App_123!App"));
    }

    [TestMethod]
    [DataRow("app-v1-win32-", true)]
    [DataRow("APP-V1-WIN32-my-app.exe-", true)]
    [DataRow("APP:V1:", false)]
    [DataRow("app-v2-win32-", false)]
    [DataRow("app-v1-other-", false)]
    [DataRow("app-v1-win32--", false)]
    [DataRow("app-v1-win32-dir/app.exe-", false)]
    [DataRow("app-v1-win32-..-", false)]
    public void CommandIdParser_AcceptsOnlyKnownUnambiguousSyntax(string prefix, bool valid)
    {
        var id = prefix + new string('a', 64);
        Assert.AreEqual(valid, AppIdentity.TryNormalizeCommandId(id, out var normalized));
        if (valid)
        {
            StringAssert.EndsWith(normalized, new string('A', 64));
        }

        Assert.IsFalse(AppIdentity.IsCommandId(prefix + new string('g', 64)));
        Assert.IsFalse(AppIdentity.IsCommandId(prefix + new string('a', 63)));
    }

    [TestMethod]
    public void CommandResolution_AcceptsEquivalentSpellingButRejectsMisleadingNames()
    {
        var app = new AppItem { CatalogId = @"win32:C:\Apps\my-app.exe|args:", Name = "Display name", LaunchTarget = @"C:\Apps\my-app.exe" };
        var legacyId = AppCommand.GenerateId(app.Name, app.Subtitle, app.LaunchTarget);
        app.CommandIds = [legacyId, .. AppIdentity.GetCommandIds(app.CatalogId)];
        var row = new AppListItem(app);
        var snapshot = new AppListItemSnapshot([row], []);
        var id = row.Command!.Id;
        var equivalent = id.ToUpperInvariant();
        Assert.AreEqual(equivalent, snapshot.GetCommandItem(equivalent)?.Command?.Id);
        Assert.AreSame(row, snapshot.GetVisibleApp($"app-v1-win32-{id[^64..].ToLowerInvariant()}"));
        Assert.AreSame(row, snapshot.GetVisibleApp(legacyId));
        Assert.IsNull(snapshot.GetVisibleApp(id.Replace("my-app.exe", "notepad.exe", StringComparison.Ordinal)));
        Assert.IsNull(snapshot.GetVisibleApp(id.Replace("my-app.exe", "my-app.cmd", StringComparison.Ordinal)));
        Assert.IsNull(snapshot.GetVisibleApp(id.Replace("my-app.exe", "my-app", StringComparison.Ordinal)));
        var hiddenSnapshot = new AppListItemSnapshot([], [row]);
        Assert.IsNull(hiddenSnapshot.GetVisibleApp(equivalent));
        Assert.AreEqual(equivalent, hiddenSnapshot.GetCommandItem(equivalent)?.Command?.Id);
    }

    [TestMethod]
    public void CommandResolution_RetainsExplicitHistoricalNamesAndAmbiguityMarkers()
    {
        var app = new AppItem { CatalogId = @"win32:C:\Apps\new.exe|args:", Name = "App", LaunchTarget = @"C:\Apps\new.exe" };
        var row = new AppListItem(app);
        var historicalId = AppIdentity.ForCommand(@"win32:C:\Apps\old.exe|args:");
        var ambiguousId = AppIdentity.ForCommand("win32:ambiguous");
        var snapshot = new AppListItemSnapshot([row], [], commandAliases: new System.Collections.Generic.Dictionary<string, string>
        {
            [historicalId] = row.Command!.Id,
            [ambiguousId] = string.Empty,
        });
        Assert.AreSame(row, snapshot.GetVisibleApp(historicalId.ToUpperInvariant()));
        Assert.IsNull(snapshot.GetVisibleApp(ambiguousId.ToLowerInvariant()));
    }

    [TestMethod]
    public void PackagedCommandParser_PreservesAumidPayload()
    {
        const string aumid = "Contoso.App_123!App";
        Assert.IsTrue(AppIdentity.TryNormalizeCommandId($"APP-V1-PACKAGED-{aumid}", out var normalized));
        Assert.AreEqual($"app-v1-packaged-{aumid}", normalized);
        Assert.IsFalse(AppIdentity.TryNormalizeCommandId("app-v1-packaged-", out _));
        Assert.IsFalse(AppIdentity.TryNormalizeCommandId($"app-v1-packaged-{aumid}/other", out _));
    }

    [TestMethod]
    [DataRow("app-v1-packaged-Contoso.App_123!Main")]
    [DataRow("APP-V1-PACKAGED-Contoso.App_123!Main")]
    [DataRow("app-v1-packaged-contoso.app_123!Main")]
    [DataRow("app-v1-packaged-Contoso.App_123!main")]
    [DataRow("APP-V1-PACKAGED-CONTOSO.APP_123!MAIN")]
    public void PackagedCommandResolution_AcceptsEquivalentCasingWithoutChangingPublishedIdentity(string requestedId)
    {
        const string aumid = "Contoso.App_123!Main";
        var app = new AppItem { CatalogId = AppIdentity.ForPackaged(aumid), Name = "App", AppUserModelId = aumid, IsPackaged = true };
        var row = new AppListItem(app);
        var snapshot = new AppListItemSnapshot([row], []);

        Assert.AreSame(row, snapshot.GetVisibleApp(requestedId));
        Assert.AreEqual(requestedId, snapshot.GetCommandItem(requestedId)?.Command?.Id);
        Assert.AreEqual("app-v1-packaged-Contoso.App_123!Main", row.Command!.Id);
        Assert.AreEqual(aumid, app.AppUserModelId);
        foreach (var hiddenSnapshot in new[] { new AppListItemSnapshot([], [row]), new AppListItemSnapshot([], [], [row]) })
        {
            Assert.IsNull(hiddenSnapshot.GetVisibleApp(requestedId));
            Assert.AreSame(row, hiddenSnapshot.GetApp(requestedId));
            Assert.AreEqual(requestedId, hiddenSnapshot.GetCommandItem(requestedId)?.Command?.Id);
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PackagedCommandResolution_RejectsAmbiguousCaseVariants(bool canonicalCollision)
    {
        const string alias = "app-v1-packaged-Contoso.Shared_123!Main";
        var first = new AppItem
        {
            CatalogId = AppIdentity.ForPackaged(canonicalCollision ? "Contoso.Shared_123!Main" : "Contoso.First_123!Main"),
            Name = "First",
            CommandIds = [alias],
        };
        var second = new AppItem
        {
            CatalogId = AppIdentity.ForPackaged(canonicalCollision ? "contoso.shared_123!main" : "Contoso.Second_123!Main"),
            Name = "Second",
            CommandIds = [alias.ToUpperInvariant()],
        };
        var snapshot = new AppListItemSnapshot(
            [new AppListItem(first), new AppListItem(second)],
            []);

        foreach (var requestedId in new[] { alias, alias.ToUpperInvariant(), alias.ToLowerInvariant() })
        {
            Assert.IsNull(snapshot.GetVisibleApp(requestedId), requestedId);
            Assert.IsNull(snapshot.GetCommandItem(requestedId), requestedId);
        }
    }

    [TestMethod]
    public void PackagedCommandResolution_KeepsLegacyAliasKeysCaseSensitive()
    {
        const string aumid = "Contoso.App_123!Main";
        var legacyId = AppCommand.GenerateId("Legacy display name", string.Empty, string.Empty);
        const string savedLegacyId = "Earlier display name_42";
        var app = new AppItem
        {
            CatalogId = AppIdentity.ForPackaged(aumid),
            Name = "App",
            AppUserModelId = aumid,
            IsPackaged = true,
            CommandIds = [legacyId],
        };
        var row = new AppListItem(app);
        var snapshot = new AppListItemSnapshot([row], [], commandAliases: new System.Collections.Generic.Dictionary<string, string>
        {
            [savedLegacyId] = row.Command!.Id.ToUpperInvariant(),
        });

        foreach (var requestedId in new[] { legacyId, savedLegacyId })
        {
            Assert.AreSame(row, snapshot.GetVisibleApp(requestedId));
            Assert.AreEqual(requestedId, snapshot.GetCommandItem(requestedId)?.Command?.Id);
            Assert.IsNull(snapshot.GetVisibleApp(requestedId.ToUpperInvariant()));
            Assert.IsNull(snapshot.GetVisibleApp(requestedId.ToLowerInvariant()));
        }
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void PackagedCommandResolution_CurrentCanonicalIdOverridesSavedCaseVariant(
        bool typedAliasFirst,
        bool conflictingTarget)
    {
        const string legacyId = "Earlier display name_42";
        var firstRow = new AppListItem(
            new AppItem { CatalogId = "packaged:Contoso.Current_123!Main", Name = "Current app" });
        var secondRow = new AppListItem(
            new AppItem { CatalogId = "packaged:Contoso.Other_123!Main", Name = "Other app" });
        var canonicalId = firstRow.Command!.Id;
        var typedKey = canonicalId.ToUpperInvariant();
        var typedTarget = conflictingTarget ? secondRow.Command!.Id : string.Empty;
        var savedAliases = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        if (typedAliasFirst)
        {
            savedAliases.Add(typedKey, typedTarget);
            savedAliases.Add(legacyId, typedKey.ToLowerInvariant());
        }
        else
        {
            savedAliases.Add(legacyId, typedKey.ToLowerInvariant());
            savedAliases.Add(typedKey, typedTarget);
        }

        var snapshot = new AppListItemSnapshot([firstRow, secondRow], [], commandAliases: savedAliases);

        foreach (var requestedId in new[] { canonicalId, typedKey, canonicalId.ToLowerInvariant(), legacyId })
        {
            Assert.AreSame(firstRow, snapshot.GetVisibleApp(requestedId));
            Assert.AreEqual(requestedId, snapshot.GetCommandItem(requestedId)?.Command?.Id);
        }

        Assert.AreEqual(canonicalId, firstRow.Command!.Id);
        Assert.AreSame(secondRow, snapshot.GetVisibleApp(secondRow.Command!.Id));
        Assert.IsNull(snapshot.GetVisibleApp(legacyId.ToUpperInvariant()));
    }

    [TestMethod]
    public void PackagedCommandResolution_RetainsShortcutIdentityAfterPackagedFirstMerge()
    {
        const string shortcutAumid = "contoso.app_123!main";
        const string packagedAumid = "Contoso.App_123!Main";
        var shortcut = new AppCatalogItem(
            AppIdentity.ForPackaged(shortcutAumid),
            20,
            new AppCatalogSourceReference("start-menu", @"C:\Start Menu\App.lnk"),
            [],
            new Win32AppPayload { Name = "App shortcut", LnkFilePath = @"C:\Start Menu\App.lnk", PackagedAppUserModelId = shortcutAumid });
        var shortcutRow = new AppListItem(shortcut.ToAppItem());
        var requestedId = shortcutRow.Command!.Id;
        Assert.AreSame(shortcutRow, new AppListItemSnapshot([shortcutRow], []).GetVisibleApp(requestedId));
        var packaged = new AppCatalogItem(
            AppIdentity.ForPackaged(packagedAumid),
            0,
            new AppCatalogSourceReference("packaged", packagedAumid),
            [],
            new PackagedAppPayload { Name = "App", AppUserModelId = packagedAumid });
        var mergedRow = new AppListItem(packaged.MergeProvenance(shortcut).ToAppItem());
        var snapshot = new AppListItemSnapshot([mergedRow], []);

        Assert.AreSame(mergedRow, snapshot.GetVisibleApp(requestedId));
        Assert.AreEqual(requestedId, snapshot.GetCommandItem(requestedId)?.Command?.Id);
        Assert.AreEqual(AppIdentity.ForCommand(packaged.Identity), mergedRow.Command!.Id);
        Assert.AreEqual(packagedAumid, mergedRow.App.AppUserModelId);
    }

    [TestMethod]
    [DataRow(false, false)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(true, true)]
    public void CommandResolution_SavedTypedAmbiguityRejectsDependentLegacyAlias(
        bool typedAliasFirst,
        bool conflictingTarget)
    {
        var historicalId = AppIdentity.ForCommand("packaged:Contoso.Old_123!Main");
        const string legacyId = "Earlier display name_42";
        var firstRow = new AppListItem(
            new AppItem
            {
                CatalogId = "packaged:Contoso.Current_123!Main",
                Name = "Current app",
                CommandIds = [historicalId],
            });
        var secondRow = new AppListItem(
            new AppItem
            {
                CatalogId = "packaged:Contoso.Other_123!Main",
                Name = "Other app",
            });
        var typedKey = conflictingTarget ? historicalId.ToUpperInvariant() : historicalId;
        var typedTarget = conflictingTarget ? secondRow.Command!.Id : string.Empty;
        var savedAliases = new System.Collections.Generic.Dictionary<string, string>(StringComparer.Ordinal);
        if (typedAliasFirst)
        {
            savedAliases.Add(typedKey, typedTarget);
            savedAliases.Add(legacyId, historicalId.ToLowerInvariant());
        }
        else
        {
            savedAliases.Add(legacyId, historicalId.ToLowerInvariant());
            savedAliases.Add(typedKey, typedTarget);
        }

        var snapshot = new AppListItemSnapshot(
            [firstRow, secondRow],
            [],
            commandAliases: savedAliases);

        Assert.IsNull(snapshot.GetVisibleApp(historicalId));
        Assert.IsNull(snapshot.GetVisibleApp(historicalId.ToUpperInvariant()));
        Assert.IsNull(snapshot.GetVisibleApp(legacyId));
        Assert.IsNull(snapshot.GetCommandItem(legacyId));
        Assert.AreSame(firstRow, snapshot.GetVisibleApp(firstRow.Command!.Id));
        Assert.AreSame(secondRow, snapshot.GetVisibleApp(secondRow.Command!.Id));
    }

    [TestMethod]
    [DataRow(@"C:\Tools\console.exe", "", "")]
    [DataRow(@"C:\Tools\console.exe", "c:/TOOLS/", "")]
    [DataRow(@"%SystemRoot%\System32\cmd.exe", @"%SystemRoot%\System32\", "")]
    [DataRow(@"C:\Tools\console.exe", "c:/Projects/Work/", @"c:\Projects\Work")]
    [DataRow(@"C:\Tools\console.lnk", @"C:\Tools", @"C:\Tools")]
    [DataRow(@"C:\Tools\console.exe", ".", ".")]
    [DataRow(@"C:\Tools\console.exe", @".\Work", @".\Work")]
    [DataRow(@"C:\Tools\console.exe", "C:Work", "C:Work")]
    public void GetDistinctWorkingDirectory_NormalizesOnlyDefaultExecutableDirectories(string target, string directory, string expected)
    {
        Assert.AreEqual(expected, AppIdentity.GetDistinctWorkingDirectory(target, directory));
    }

    [TestMethod]
    [DataRow("com.squirrel.GitHubDesktop.GitHubDesktop", @"C:\Apps\GitHubDesktop\app-2.7.2", true)]
    [DataRow("COM.SQUIRREL.GITHUBDESKTOP.GITHUBDESKTOP", "c:/apps/GitHubDesktop/app-3.6.3/", true)]
    [DataRow("", @"C:\Apps\GitHubDesktop\app-2.7.2", false)]
    [DataRow("Contoso.GitHubDesktop", @"C:\Apps\GitHubDesktop\app-2.7.2", false)]
    [DataRow("com.squirrel.Other.GitHubDesktop", @"C:\Apps\GitHubDesktop\app-2.7.2", false)]
    [DataRow("com.squirrel.GitHubDesktop.Other", @"C:\Apps\GitHubDesktop\app-2.7.2", false)]
    [DataRow("com.squirrel.GitHubDesktop.GitHubDesktop", @"C:\Projects\app-2.7.2", false)]
    [DataRow("com.squirrel.GitHubDesktop.GitHubDesktop", @"C:\Apps\GitHubDesktop\app-2.7.2\Work", false)]
    [DataRow("com.squirrel.GitHubDesktop.GitHubDesktop", @"C:\Apps\GitHubDesktop\app-Work", false)]
    public void GetDistinctWorkingDirectory_RecognizesOnlySquirrelInstallerVersionDirectories(string explicitId, string directory, bool isDefault)
    {
        var distinctDirectory = AppIdentity.GetDistinctWorkingDirectory(@"C:\Apps\GitHubDesktop\GitHubDesktop.exe", directory, explicitId);
        Assert.AreEqual(isDefault, string.IsNullOrEmpty(distinctDirectory));
    }
}
