// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class PackagedAppMetadataTests
{
    [TestMethod]
    [DataRow("launcher.exe", "launcher.exe")]
    [DataRow("launcher.exe", "launcher")]
    [DataRow(@"VFS\Tools\launcher.exe", "launcher.exe")]
    [DataRow(@"VFS\Tools\launcher.exe", "launcher")]
    [DataRow(@"VFS\Tools\launcher.exe", "LAUNCHER.EXE")]
    public void CreateCatalogItem_ExecutableNameAndStemUseOrdinaryMetadata(string executable, string query)
    {
        var app = CreateApplication(executable);
        var item = PackagedAppSource.CreateCatalogItem(app);
        var payload = (PackagedAppSnapshot)item.Payload;
        var row = new AppListItem(item.ToAppItem(), useThumbnails: false);

        Assert.AreEqual(executable, payload.Executable);
        CollectionAssert.Contains(item.MatchTerms.ToArray(), Path.GetFileName(executable));
        Assert.AreEqual(app.UserModelId, row.App.UserModelId);
        Assert.IsTrue(row.App.IsPackaged);
        Assert.AreEqual(string.Empty, row.App.ExePath);
        Assert.IsNull(row.App.FullExecutablePath);
        Assert.IsNull(item.Payload.GetCanonicalTargetPath());
        Assert.AreEqual(0, row.ExecutableNames.Count);
        foreach (var mode in Enum.GetValues<ExecutableNameMatchMode>())
        {
            var match = new AppSearch(query, new PrecomputedFuzzyMatcher(), mode).Evaluate(row);
            Assert.IsTrue(match.HasMatch, $"Executable metadata should match in mode {mode}.");
            Assert.IsTrue(match.IsExactMetadataMatch);
            Assert.IsFalse(match.IsExactExecutableMatch, "A manifest executable is search metadata, not an executable-priority target.");
        }
    }

    [TestMethod]
    [DataRow("VFS")]
    [DataRow("ProgramFilesX64")]
    [DataRow("program")]
    [DataRow("files")]
    [DataRow("x64")]
    [DataRow("VendorLayout")]
    [DataRow("PayloadDirectory")]
    public void CreateCatalogItem_ExecutableLayoutFoldersDoNotBecomeSearchMetadata(string query)
    {
        var app = CreateApplication(string.Empty, "Editor");
        app.Description = "A text editor";
        app.Package.Location = @"C:\Packages\Editor";
        var baseline = new AppListItem(PackagedAppSource.CreateCatalogItem(app).ToAppItem(), useThumbnails: false);
        app.Executable = @"VFS\ProgramFilesX64\VendorLayout\PayloadDirectory\launcher.exe";
        var item = PackagedAppSource.CreateCatalogItem(app);
        var row = new AppListItem(item.ToAppItem(), useThumbnails: false);

        Assert.AreEqual(app.Executable, ((PackagedAppSnapshot)item.Payload).Executable);
        CollectionAssert.Contains(item.MatchTerms.ToArray(), "launcher.exe");
        Assert.IsFalse(item.MatchTerms.Contains(app.Executable, StringComparer.OrdinalIgnoreCase));
        foreach (var mode in Enum.GetValues<ExecutableNameMatchMode>())
        {
            var search = new AppSearch(query, new PrecomputedFuzzyMatcher(), mode);
            Assert.IsFalse(search.Evaluate(baseline).HasMatch, "The fixture's other metadata must not match the layout folder query.");
            Assert.IsFalse(search.Evaluate(row).HasMatch, "Manifest layout folders must not become application search terms.");
        }

        foreach (var executableQuery in new[] { "launcher.exe", "launcher" })
        {
            var match = new AppSearch(executableQuery, new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameAndStem).Evaluate(row);
            Assert.IsTrue(match.IsExactMetadataMatch);
            Assert.IsFalse(match.IsExactExecutableMatch);
        }
    }

    [TestMethod]
    public void CreateCatalogItem_SharedExecutableKeepsDifferentAumidsDistinct()
    {
        var first = CreateApplication(@"VFS\Tools\shared-host.exe", "First application", "Contoso.Metadata_123!First");
        var second = CreateApplication(first.Executable, "Second application", "Contoso.Metadata_123!Second");
        second.Package = first.Package;
        var items = new[]
        {
            PackagedAppSource.CreateCatalogItem(first),
            PackagedAppSource.CreateCatalogItem(second),
        };
        var rows = items.Select(item => new AppListItem(item.ToAppItem(), useThumbnails: false)).ToArray();

        Assert.AreEqual(2, items.Select(item => item.Identity).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.AreEqual(2, rows.Select(row => row.Command!.Id).Distinct(StringComparer.Ordinal).Count());
        CollectionAssert.AreEquivalent(new[] { first.UserModelId, second.UserModelId }, rows.Select(row => row.App.UserModelId).ToArray());
        foreach (var item in items)
        {
            Assert.AreEqual(AppIdentity.ForPackaged(((PackagedAppSnapshot)item.Payload).UserModelId), item.Identity);
            Assert.IsNull(item.Payload.GetCanonicalTargetPath());
        }

        foreach (var row in rows)
        {
            Assert.AreEqual(0, row.ExecutableNames.Count);
            foreach (var query in new[] { "shared-host.exe", "shared-host" })
            {
                var match = new AppSearch(query, new PrecomputedFuzzyMatcher(), ExecutableNameMatchMode.FilenameAndStem).Evaluate(row);
                Assert.IsTrue(match.IsExactMetadataMatch);
                Assert.IsFalse(match.IsExactExecutableMatch);
            }
        }
    }

    [TestMethod]
    public void CreateCatalogItem_ChangedExecutableKeepsIdentityAndCommandIds()
    {
        var app = CreateApplication(@"VFS\Tools\old-launcher.exe");
        var original = PackagedAppSource.CreateCatalogItem(app);
        app.Executable = @"VFS\Tools\new-launcher.exe";
        var updated = PackagedAppSource.CreateCatalogItem(app);

        Assert.AreEqual(original.Identity, updated.Identity);
        Assert.AreEqual(new AppCommand(original.ToAppItem()).Id, new AppCommand(updated.ToAppItem()).Id);
        CollectionAssert.AreEqual(original.CommandIds.ToArray(), updated.CommandIds.ToArray());
        CollectionAssert.AreEqual(original.IdentityAliases.ToArray(), updated.IdentityAliases.ToArray());
        Assert.AreEqual(@"VFS\Tools\old-launcher.exe", ((PackagedAppSnapshot)original.Payload).Executable);
        Assert.AreEqual(app.Executable, ((PackagedAppSnapshot)updated.Payload).Executable);
        Assert.IsFalse(original.HasSamePersistedContent(updated));
        Assert.IsFalse(updated.MatchTerms.Contains("old-launcher.exe", StringComparer.OrdinalIgnoreCase));
        CollectionAssert.Contains(updated.MatchTerms.ToArray(), Path.GetFileName(app.Executable));
        Assert.IsNull(updated.Payload.GetCanonicalTargetPath());
        Assert.AreEqual(app.UserModelId, updated.ToAppItem().UserModelId);
    }

    [TestMethod]
    public void CreateCatalogItem_ExecutionAliasesChangeOnlySearchMetadata()
    {
        var app = CreateApplication("WindowsTerminal.exe", "Terminal");
        var original = PackagedAppSource.CreateCatalogItem(app);
        var aliases = new[] { "wt.exe", "WT.EXE" };
        app.ExecutionAliases = aliases;
        var updated = PackagedAppSource.CreateCatalogItem(app);
        var repeated = PackagedAppSource.CreateCatalogItem(app);
        aliases[0] = "changed.exe";

        Assert.AreEqual(original.Identity, updated.Identity);
        Assert.AreEqual(new AppCommand(original.ToAppItem()).Id, new AppCommand(updated.ToAppItem()).Id);
        CollectionAssert.AreEqual(original.CommandIds.ToArray(), updated.CommandIds.ToArray());
        CollectionAssert.AreEqual(original.IdentityAliases.ToArray(), updated.IdentityAliases.ToArray());
        Assert.IsFalse(original.HasSamePersistedContent(updated));
        Assert.IsTrue(updated.HasSamePersistedContent(repeated), "Repeated discovery must not publish unchanged alias terms.");
        Assert.AreEqual(1, updated.MatchTerms.Count(term => term.Equals("wt.exe", StringComparison.OrdinalIgnoreCase)));
        Assert.IsFalse(updated.MatchTerms.Contains("changed.exe"));
    }

    [TestMethod]
    public void CreateCatalogItem_AbsentExecutableRetainsPackagedApplication()
    {
        var app = TestDataHelper.CreateTestUWPApplication("Metadata application", "Contoso.Metadata_123!App");
        var item = PackagedAppSource.CreateCatalogItem(app);
        var row = new AppListItem(item.ToAppItem(), useThumbnails: false);

        Assert.AreEqual(string.Empty, app.Executable);
        Assert.AreEqual(string.Empty, new PackagedAppSnapshot().Executable);
        Assert.AreEqual(string.Empty, ((PackagedAppSnapshot)item.Payload).Executable);
        Assert.AreEqual(AppIdentity.ForPackaged(app.UserModelId), item.Identity);
        Assert.AreEqual(app.UserModelId, row.App.UserModelId);
        Assert.AreEqual(app.Name, row.Title);
        Assert.AreEqual(string.Empty, row.App.ExePath);
        Assert.IsNull(row.App.FullExecutablePath);
        Assert.IsNull(item.Payload.GetCanonicalTargetPath());
        Assert.AreEqual(0, row.ExecutableNames.Count);
        Assert.IsFalse(item.MatchTerms.Any(string.IsNullOrWhiteSpace));
    }

    [STATestMethod]
    [DataRow(@"VFS\Tools\launcher.exe")]
    [DataRow("")]
    public void InitializeAppInfo_TypedManifestReaderCapturesOptionalExecutable(string executable)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-packaged-metadata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "AppxManifest.xml"), CreateManifest(executable));
            var package = TestDataHelper.CreateTestUWPApplication("Manifest fixture").Package;

            package.InitializeAppInfo(root);

            Assert.AreEqual(1, package.Apps.Count, "The real typed manifest reader should retain the application even without Executable.");
            var app = package.Apps.Single();
            Assert.AreEqual(executable, app.Executable);
            Assert.AreEqual(0, app.ExecutionAliases.Count);
            Assert.AreEqual("Manifest fixture", app.DisplayName);
            Assert.IsTrue(app.Enabled);
            StringAssert.EndsWith(app.UserModelId, "!App");
            var item = PackagedAppSource.CreateCatalogItem(app);
            Assert.AreEqual(executable, ((PackagedAppSnapshot)item.Payload).Executable);
            Assert.AreEqual(AppIdentity.ForPackaged(app.UserModelId), item.Identity);
            Assert.AreEqual(app.UserModelId, item.ToAppItem().UserModelId);
            Assert.IsNull(item.Payload.GetCanonicalTargetPath());
            if (!string.IsNullOrEmpty(executable))
            {
                CollectionAssert.Contains(item.MatchTerms.ToArray(), Path.GetFileName(executable));
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [STATestMethod]
    [DataRow("uap3", "desktop")]
    [DataRow("uap5", "uap5")]
    [DataRow("uap5", "uap8")]
    public void InitializeAppInfo_ExecutionAliasNameAndStemUseOrdinaryMetadata(string extensionPrefix, string aliasPrefix)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-packaged-alias-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var extensions = CreateExecutionAliasExtension("wt.exe", extensionPrefix, aliasPrefix);
            File.WriteAllText(Path.Combine(root, "AppxManifest.xml"), CreateManifest("WindowsTerminal.exe", extensions));
            var package = TestDataHelper.CreateTestUWPApplication("Manifest fixture").Package;
            package.InitializeAppInfo(root);

            Assert.AreEqual(1, package.Apps.Count);
            var app = package.Apps.Single();
            Assert.AreEqual("wt.exe", app.ExecutionAliases.Single());
            var item = PackagedAppSource.CreateCatalogItem(app);
            var row = new AppListItem(item.ToAppItem(), useThumbnails: false);
            CollectionAssert.Contains(item.MatchTerms.ToArray(), "wt.exe");
            Assert.AreEqual(AppIdentity.ForPackaged(app.UserModelId), item.Identity);
            Assert.AreEqual(app.UserModelId, row.App.UserModelId);
            Assert.AreEqual(string.Empty, row.App.ExePath);
            Assert.IsNull(item.Payload.GetCanonicalTargetPath());
            Assert.AreEqual(0, row.ExecutableNames.Count);
            foreach (var mode in Enum.GetValues<ExecutableNameMatchMode>())
            {
                foreach (var query in new[] { "wt", "wt.exe", "WT.EXE" })
                {
                    var match = new AppSearch(query, new PrecomputedFuzzyMatcher(), mode).Evaluate(row);
                    Assert.IsTrue(match.HasMatch, $"Execution alias '{query}' should match in mode {mode}.");
                    Assert.IsTrue(match.IsExactMetadataMatch);
                    Assert.IsFalse(match.IsExactExecutableMatch);
                }
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [STATestMethod]
    public void InitializeAppInfo_ExecutionAliasesStayWithTheirDeclaringApplication()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-packaged-alias-scope-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var otherApplication = $"""
                <Application Id="Helper" Executable="Helper.exe" EntryPoint="CmdPal.Metadata.Helper">
                  <uap:VisualElements DisplayName="Helper fixture" Description="Helper description"
                                      BackgroundColor="transparent" Square150x150Logo="Assets\Logo.png" Square44x44Logo="Assets\Logo.png" />
                  {CreateExecutionAliasExtension("assist.exe", "uap5", "uap5")}
                </Application>
                """;
            File.WriteAllText(
                Path.Combine(root, "AppxManifest.xml"),
                CreateManifest("WindowsTerminal.exe", CreateExecutionAliasExtension("wt.exe", "uap3", "desktop"), otherApplication));
            var package = TestDataHelper.CreateTestUWPApplication("Manifest fixture").Package;
            package.InitializeAppInfo(root);

            Assert.AreEqual(2, package.Apps.Count);
            var main = package.Apps.Single(app => app.UserModelId.EndsWith("!App", StringComparison.Ordinal));
            var helper = package.Apps.Single(app => app.UserModelId.EndsWith("!Helper", StringComparison.Ordinal));
            Assert.AreEqual("wt.exe", main.ExecutionAliases.Single());
            Assert.AreEqual("assist.exe", helper.ExecutionAliases.Single());
            Assert.IsFalse(PackagedAppSource.CreateCatalogItem(main).MatchTerms.Contains("assist.exe"));
            Assert.IsFalse(PackagedAppSource.CreateCatalogItem(helper).MatchTerms.Contains("wt.exe"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static MockUWPApplication CreateApplication(
        string executable,
        string name = "Metadata application",
        string userModelId = "Contoso.Metadata_123!App")
    {
        var app = (MockUWPApplication)TestDataHelper.CreateTestUWPApplication(name, userModelId);
        app.Executable = executable;
        return app;
    }

    private static string CreateExecutionAliasExtension(string alias, string extensionPrefix, string aliasPrefix)
    {
        return $"""
            <Extensions>
              <{extensionPrefix}:Extension Category="windows.appExecutionAlias" Executable="AliasHost.exe" EntryPoint="Windows.FullTrustApplication">
                <{extensionPrefix}:AppExecutionAlias>
                  <{aliasPrefix}:ExecutionAlias Alias="{alias}" />
                </{extensionPrefix}:AppExecutionAlias>
              </{extensionPrefix}:Extension>
            </Extensions>
            """;
    }

    private static string CreateManifest(string executable, string extensions = "", string additionalApplications = "")
    {
        var activationAttributes = string.IsNullOrEmpty(executable)
            ? "StartPage=\"default.html\""
            : $"Executable=\"{executable}\" EntryPoint=\"CmdPal.Metadata.App\"";
        return $"""
            <?xml version="1.0" encoding="utf-8"?>
            <Package xmlns="http://schemas.microsoft.com/appx/manifest/foundation/windows10"
                     xmlns:uap="http://schemas.microsoft.com/appx/manifest/uap/windows10"
                     xmlns:uap3="http://schemas.microsoft.com/appx/manifest/uap/windows10/3"
                     xmlns:uap5="http://schemas.microsoft.com/appx/manifest/uap/windows10/5"
                     xmlns:uap8="http://schemas.microsoft.com/appx/manifest/uap/windows10/8"
                     xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
                     IgnorableNamespaces="uap uap3 uap5 uap8 desktop">
              <Identity Name="CmdPal.MetadataFixture" Publisher="CN=CmdPal" Version="1.0.0.0" ProcessorArchitecture="neutral" />
              <Properties>
                <DisplayName>Manifest fixture</DisplayName>
                <PublisherDisplayName>CmdPal</PublisherDisplayName>
                <Logo>Assets\StoreLogo.png</Logo>
              </Properties>
              <Dependencies>
                <TargetDeviceFamily Name="Windows.Universal" MinVersion="10.0.0.0" MaxVersionTested="10.0.0.0" />
              </Dependencies>
              <Resources>
                <Resource Language="en-US" />
              </Resources>
              <Applications>
                <Application Id="App" {activationAttributes}>
                  <uap:VisualElements DisplayName="Manifest fixture" Description="Manifest executable metadata"
                                      BackgroundColor="transparent" Square150x150Logo="Assets\Logo.png" Square44x44Logo="Assets\Logo.png" />
                  {extensions}
                </Application>
                {additionalApplications}
              </Applications>
            </Package>
            """;
    }
}
