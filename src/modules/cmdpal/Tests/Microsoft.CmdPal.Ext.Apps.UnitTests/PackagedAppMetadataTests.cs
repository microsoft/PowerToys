// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common.Text;
using Microsoft.CmdPal.Ext.Apps.AppList;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Payloads;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CmdPal.Ext.Apps.Packaged;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public partial class PackagedAppMetadataTests
{
    [TestMethod]
    public void CacheKey_ContainsNoThemeState()
    {
        using var source = new PackagedAppSource(new SilentPackageCatalog());
        Assert.AreEqual(Environment.OSVersion.Version.ToString(), source.CacheKey);
    }

    [STATestMethod]
    [DoNotParallelize]
    public async Task Load_FailedManifestReportsIncompleteAndRecoversAfterWriterCloses()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-manifest-sharing-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "AppxManifest.xml");
        try
        {
            File.WriteAllText(path, CreateManifest("app.exe"));
            var package = new Mock<IPackage>();
            package.SetupGet(value => value.Name).Returns("Manifest fixture");
            package.SetupGet(value => value.FullName).Returns("CmdPal.MetadataFixture_1.0.0.0_neutral__123");
            package.SetupGet(value => value.FamilyName).Returns("CmdPal.MetadataFixture_123");
            package.SetupGet(value => value.InstalledLocation).Returns(root);
            var manager = new Mock<IPackageManager>();
            manager.Setup(value => value.FindPackagesForCurrentUser()).Returns([package.Object]);
            using var source = new PackagedAppSource(new SilentPackageCatalog(), packageManager: manager.Object);
            using (var writer = File.Open(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            {
                var scan = (AppSourceScanResult)await source.LoadAsync(CancellationToken.None);
                Assert.AreEqual(0, scan.Count);
                Assert.IsFalse(scan.IsComplete);
            }

            var recovered = (AppSourceScanResult)await source.LoadAsync(CancellationToken.None);
            Assert.AreEqual(1, recovered.Count);
            Assert.IsTrue(recovered.IsComplete);
            using var exclusiveWriter = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.None);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

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
        var payload = (PackagedAppPayload)item.Payload;
        var row = new AppListItem(item.ToAppItem());

        Assert.AreEqual(executable, payload.Executable);
        CollectionAssert.Contains(item.MatchTerms.ToArray(), Path.GetFileName(executable));
        Assert.AreEqual(app.AppUserModelId, row.App.AppUserModelId);
        Assert.IsTrue(row.App.IsPackaged);
        Assert.AreEqual(string.Empty, row.App.LaunchTarget);
        Assert.IsNull(row.App.ResolvedTarget);
        Assert.AreEqual(Path.Combine(app.Package.InstalledLocation, executable), item.Payload.GetCanonicalTargetPath());
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
        var app = TestDataHelper.CreateTestPackagedMetadata("Editor", "Contoso.Metadata_123!App", @"C:\Packages\Editor");
        app.Description = "A text editor";
        var baseline = new AppListItem(PackagedAppSource.CreateCatalogItem(app).ToAppItem());
        app.Executable = @"VFS\ProgramFilesX64\VendorLayout\PayloadDirectory\launcher.exe";
        var item = PackagedAppSource.CreateCatalogItem(app);
        var row = new AppListItem(item.ToAppItem());

        Assert.AreEqual(app.Executable, ((PackagedAppPayload)item.Payload).Executable);
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
        var rows = items.Select(item => new AppListItem(item.ToAppItem())).ToArray();

        Assert.AreEqual(2, items.Select(item => item.Identity).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.AreEqual(2, rows.Select(row => row.Command!.Id).Distinct(StringComparer.Ordinal).Count());
        CollectionAssert.AreEquivalent(new[] { first.AppUserModelId, second.AppUserModelId }, rows.Select(row => row.App.AppUserModelId).ToArray());
        foreach (var item in items)
        {
            var payload = (PackagedAppPayload)item.Payload;
            Assert.AreEqual(AppIdentity.ForPackaged(payload.AppUserModelId), item.Identity);
            Assert.AreEqual(Path.Combine(payload.PackageLocation, payload.Executable), item.Payload.GetCanonicalTargetPath());
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
        Assert.AreEqual(@"VFS\Tools\old-launcher.exe", ((PackagedAppPayload)original.Payload).Executable);
        Assert.AreEqual(app.Executable, ((PackagedAppPayload)updated.Payload).Executable);
        Assert.IsFalse(original.HasSamePersistedContent(updated));
        Assert.IsFalse(updated.MatchTerms.Contains("old-launcher.exe", StringComparer.OrdinalIgnoreCase));
        CollectionAssert.Contains(updated.MatchTerms.ToArray(), Path.GetFileName(app.Executable));
        Assert.AreEqual(Path.Combine(app.Package.InstalledLocation, app.Executable), updated.Payload.GetCanonicalTargetPath());
        Assert.AreEqual(app.AppUserModelId, updated.ToAppItem().AppUserModelId);
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
    [DataRow(@"C:\Packages\App", "app.exe", @"C:\Packages\App\app.exe")]
    [DataRow(@"C:\Packages\App", @"Tools\app.exe", @"C:\Packages\App\Tools\app.exe")]
    [DataRow(@"C:\Packages\App", @"Tools\..\app.exe", @"C:\Packages\App\app.exe")]
    [DataRow(@"C:\Packages\App", "", null)]
    [DataRow(@"C:\Packages\App", @"C:\Elsewhere\app.exe", null)]
    [DataRow(@"C:\Packages\App", @"\Elsewhere\app.exe", null)]
    [DataRow(@"C:\Packages\App", "C:app.exe", null)]
    [DataRow(@"C:\Packages\App", @"..\AppOther\app.exe", null)]
    [DataRow(@"C:\Packages\App", "https://example.com/app.exe", null)]
    [DataRow(@"C:\Packages\App", "app.txt", null)]
    [DataRow(@"C:\Packages\App", "bad\0app.exe", null)]
    [DataRow("relative-package", "app.exe", null)]
    [DataRow("", "app.exe", null)]
    public void CanonicalTargetPath_OnlyUsesPackageLocalExecutables(string packagePath, string executable, string expected)
    {
        IAppCatalogPayload payload = new PackagedAppPayload
        {
            PackageLocation = packagePath,
            Executable = executable,
        };

        Assert.AreEqual(expected, payload.GetCanonicalTargetPath());
    }

    [TestMethod]
    public void CreateCatalogItem_AbsentExecutableRetainsPackagedApplication()
    {
        var app = TestDataHelper.CreateTestPackagedMetadata("Metadata application", "Contoso.Metadata_123!App");
        var item = PackagedAppSource.CreateCatalogItem(app);
        var row = new AppListItem(item.ToAppItem());

        Assert.AreEqual(string.Empty, app.Executable);
        Assert.AreEqual(string.Empty, new PackagedAppPayload().Executable);
        Assert.AreEqual(string.Empty, ((PackagedAppPayload)item.Payload).Executable);
        Assert.AreEqual(AppIdentity.ForPackaged(app.AppUserModelId), item.Identity);
        Assert.AreEqual(app.AppUserModelId, row.App.AppUserModelId);
        Assert.AreEqual(app.Name, row.Title);
        Assert.AreEqual(string.Empty, row.App.LaunchTarget);
        Assert.IsNull(row.App.ResolvedTarget);
        Assert.IsNull(item.Payload.GetCanonicalTargetPath());
        Assert.AreEqual(0, row.ExecutableNames.Count);
        Assert.IsFalse(item.MatchTerms.Any(string.IsNullOrWhiteSpace));
    }

    [STATestMethod]
    [DataRow(@"VFS\Tools\launcher.exe")]
    [DataRow("")]
    public void ReadManifest_TypedManifestReaderCapturesOptionalExecutable(string executable)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-packaged-metadata-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "AppxManifest.xml"), CreateManifest(executable));
            var package = TestDataHelper.CreateTestPackagedMetadata("Manifest fixture", packageLocation: root).Package;

            var apps = PackagedAppReader.ReadManifest(package, out var complete);
            Assert.IsTrue(complete);

            Assert.AreEqual(1, apps.Count, "The real typed manifest reader should retain the application even without Executable.");
            var app = apps.Single();
            Assert.AreEqual(executable, app.Executable);
            Assert.AreEqual(0, app.ExecutionAliases.Count);
            Assert.AreEqual("Manifest fixture", app.Name);
            StringAssert.EndsWith(app.AppUserModelId, "!App");
            var item = PackagedAppSource.CreateCatalogItem(app);
            Assert.AreEqual(executable, ((PackagedAppPayload)item.Payload).Executable);
            Assert.AreEqual(AppIdentity.ForPackaged(app.AppUserModelId), item.Identity);
            Assert.AreEqual(app.AppUserModelId, item.ToAppItem().AppUserModelId);
            Assert.AreEqual(string.IsNullOrEmpty(executable) ? null : Path.Combine(root, executable), item.Payload.GetCanonicalTargetPath());
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
    public void ReadManifest_ExecutionAliasNameAndStemUseOrdinaryMetadata(string extensionPrefix, string aliasPrefix)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-packaged-alias-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var extensions = CreateExecutionAliasExtension("wt.exe", extensionPrefix, aliasPrefix);
            File.WriteAllText(Path.Combine(root, "AppxManifest.xml"), CreateManifest("WindowsTerminal.exe", extensions));
            var package = TestDataHelper.CreateTestPackagedMetadata("Manifest fixture", packageLocation: root).Package;
            var apps = PackagedAppReader.ReadManifest(package, out var complete);
            Assert.IsTrue(complete);

            Assert.AreEqual(1, apps.Count);
            var app = apps.Single();
            Assert.AreEqual("wt.exe", app.ExecutionAliases.Single());
            var item = PackagedAppSource.CreateCatalogItem(app);
            var row = new AppListItem(item.ToAppItem());
            CollectionAssert.Contains(item.MatchTerms.ToArray(), "wt.exe");
            Assert.AreEqual(AppIdentity.ForPackaged(app.AppUserModelId), item.Identity);
            Assert.AreEqual(app.AppUserModelId, row.App.AppUserModelId);
            Assert.AreEqual(string.Empty, row.App.LaunchTarget);
            Assert.AreEqual(Path.Combine(root, app.Executable), item.Payload.GetCanonicalTargetPath());
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
    public void ReadManifest_ExecutionAliasesStayWithTheirDeclaringApplication()
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
            var package = TestDataHelper.CreateTestPackagedMetadata("Manifest fixture", packageLocation: root).Package;
            var apps = PackagedAppReader.ReadManifest(package, out var complete);
            Assert.IsTrue(complete);

            Assert.AreEqual(2, apps.Count);
            var main = apps.Single(app => app.AppUserModelId.EndsWith("!App", StringComparison.Ordinal));
            var helper = apps.Single(app => app.AppUserModelId.EndsWith("!Helper", StringComparison.Ordinal));
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

    [STATestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ReadManifest_ElevationUsesEachApplicationsOwnTrustLevel(bool mainIsFullTrust)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-packaged-trust-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var mainTrust = mainIsFullTrust ? "mediumIL" : "appContainer";
            var helperTrust = mainIsFullTrust ? "appContainer" : "mediumIL";
            var helper = $"""
                <Application Id="Helper" Executable="Helper.exe" EntryPoint="CmdPal.Metadata.Helper" uap10:TrustLevel="{helperTrust}">
                  <uap:VisualElements DisplayName="Helper" Description="Helper"
                                      BackgroundColor="transparent" Square150x150Logo="Assets\Logo.png" Square44x44Logo="Assets\Logo.png" />
                </Application>
                """;
            var manifest = CreateManifest("app.exe", additionalApplications: helper)
                .Replace("<Application Id=\"App\"", $"<Application Id=\"App\" uap10:TrustLevel=\"{mainTrust}\"", StringComparison.Ordinal);
            File.WriteAllText(Path.Combine(root, "AppxManifest.xml"), manifest);
            var package = TestDataHelper.CreateTestPackagedMetadata(packageLocation: root).Package;

            var apps = PackagedAppReader.ReadManifest(package, out var complete);

            Assert.IsTrue(complete);
            Assert.AreEqual(2, apps.Count);
            Assert.AreEqual(mainIsFullTrust, apps.Single(app => app.AppUserModelId.EndsWith("!App", StringComparison.Ordinal)).CanRunElevated);
            Assert.AreEqual(!mainIsFullTrust, apps.Single(app => app.AppUserModelId.EndsWith("!Helper", StringComparison.Ordinal)).CanRunElevated);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [STATestMethod]
    [DataRow(false, false)]
    [DataRow(false, true)]
    [DataRow(true, false)]
    public void ReadManifest_PreservesVisibilityAndOptionalEntryPoint(bool hidden, bool fullTrustEntryPoint)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-packaged-optional-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var manifest = CreateManifest(fullTrustEntryPoint ? "app.exe" : string.Empty);
            if (hidden)
            {
                manifest = manifest.Replace("<uap:VisualElements", "<uap:VisualElements AppListEntry=\"none\"", StringComparison.Ordinal);
            }

            if (fullTrustEntryPoint)
            {
                manifest = manifest.Replace("CmdPal.Metadata.App", "Windows.FullTrustApplication", StringComparison.Ordinal)
                    .Replace("xmlns:desktop=", "xmlns:rescap=\"http://schemas.microsoft.com/appx/manifest/foundation/windows10/restrictedcapabilities\" xmlns:desktop=", StringComparison.Ordinal)
                    .Replace("IgnorableNamespaces=\"", "IgnorableNamespaces=\"rescap ", StringComparison.Ordinal)
                    .Replace("</Package>", "<Capabilities><rescap:Capability Name=\"runFullTrust\" /></Capabilities></Package>", StringComparison.Ordinal);
            }

            File.WriteAllText(Path.Combine(root, "AppxManifest.xml"), manifest);
            var package = TestDataHelper.CreateTestPackagedMetadata(packageLocation: root).Package;

            var apps = PackagedAppReader.ReadManifest(package, out var complete);

            Assert.IsTrue(complete);
            Assert.AreEqual(hidden ? 0 : 1, apps.Count);
            if (!hidden)
            {
                Assert.AreEqual("Manifest executable metadata", apps.Single().Description);
                Assert.AreEqual(fullTrustEntryPoint, apps.Single().CanRunElevated);
                Assert.AreEqual(fullTrustEntryPoint ? "app.exe" : string.Empty, apps.Single().Executable);
            }

            using var writer = File.Open(Path.Combine(root, "AppxManifest.xml"), FileMode.Open, FileAccess.Write, FileShare.None);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [STATestMethod]
    public void ReadManifest_InvalidSchemaReportsNativeFailureAndReleasesFile()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-packaged-invalid-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, "AppxManifest.xml");
        try
        {
            File.WriteAllText(path, CreateManifest("app.exe")
                .Replace("Description=\"Manifest executable metadata\"", string.Empty, StringComparison.Ordinal));
            var package = TestDataHelper.CreateTestPackagedMetadata(packageLocation: root).Package;

            Assert.ThrowsExactly<COMException>(() => PackagedAppReader.ReadManifest(package, out _));

            using var writer = File.Open(path, FileMode.Open, FileAccess.Write, FileShare.None);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [STATestMethod]
    [DataRow("Windows8")]
    [DataRow("Windows81")]
    [DataRow("Windows10")]
    public void ReadManifest_SupportedSchemasRetainLogicalLogoReferences(string schema)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-packaged-legacy-icons-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        try
        {
            var smallLogo = Path.Combine(root, "Assets", "SmallLogo.targetsize-30.png");
            var largeLogo = Path.Combine(root, "Assets", "LargeLogo.targetsize-150.png");
            File.WriteAllBytes(smallLogo, [0]);
            File.WriteAllBytes(largeLogo, [0]);
            var windows81 = schema == "Windows81";
            var namespaceDeclaration = windows81
                ? """xmlns:m2="http://schemas.microsoft.com/appx/2013/manifest" IgnorableNamespaces="m2" """
                : string.Empty;
            var prefix = windows81 ? "m2:" : string.Empty;
            var osVersion = windows81 ? "6.3.0" : "6.2.1";
            var logoAttributes = windows81
                ? """Square30x30Logo="Assets\SmallLogo.png" Square150x150Logo="Assets\LargeLogo.png" """
                : """SmallLogo="Assets\SmallLogo.png" Logo="Assets\LargeLogo.png" """;
            var manifest = $"""
                <?xml version="1.0" encoding="utf-8"?>
                <Package xmlns="http://schemas.microsoft.com/appx/2010/manifest" {namespaceDeclaration}>
                  <Identity Name="CmdPal.MetadataFixture" Publisher="CN=CmdPal" Version="1.0.0.0" ProcessorArchitecture="neutral" />
                  <Properties>
                    <DisplayName>Manifest fixture</DisplayName>
                    <PublisherDisplayName>CmdPal</PublisherDisplayName>
                    <Logo>Assets\StoreLogo.png</Logo>
                  </Properties>
                  <Prerequisites>
                    <OSMinVersion>{osVersion}</OSMinVersion>
                    <OSMaxVersionTested>{osVersion}</OSMaxVersionTested>
                  </Prerequisites>
                  <Resources>
                    <Resource Language="en-US" />
                  </Resources>
                  <Applications>
                    <Application Id="App" StartPage="default.html">
                      <{prefix}VisualElements DisplayName="Manifest fixture" Description="Legacy icon metadata"
                                              ForegroundText="light" BackgroundColor="transparent" {logoAttributes}>
                        <{prefix}SplashScreen Image="Assets\SplashScreen.png" />
                      </{prefix}VisualElements>
                    </Application>
                  </Applications>
                </Package>
                """;
            if (schema == "Windows10")
            {
                manifest = CreateManifest("app.exe")
                    .Replace("Square44x44Logo=\"Assets\\Logo.png\"", "Square44x44Logo=\"Assets\\SmallLogo.png\"", StringComparison.Ordinal)
                    .Replace("Square150x150Logo=\"Assets\\Logo.png\"", "Square150x150Logo=\"Assets\\LargeLogo.png\"", StringComparison.Ordinal);
            }

            File.WriteAllText(Path.Combine(root, "AppxManifest.xml"), manifest);
            var package = TestDataHelper.CreateTestPackagedMetadata(packageLocation: root).Package;

            var apps = PackagedAppReader.ReadManifest(package, out var complete);

            Assert.IsTrue(complete);
            Assert.AreEqual(1, apps.Count);
            Assert.AreEqual(@"Assets\SmallLogo.png", apps[0].SmallLogoUri);
            Assert.AreEqual(@"Assets\LargeLogo.png", apps[0].LargeLogoUri);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static PackagedAppMetadata CreateApplication(
        string executable,
        string name = "Metadata application",
        string userModelId = "Contoso.Metadata_123!App")
    {
        var app = TestDataHelper.CreateTestPackagedMetadata(name, userModelId);
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
                     xmlns:uap10="http://schemas.microsoft.com/appx/manifest/uap/windows10/10"
                     xmlns:desktop="http://schemas.microsoft.com/appx/manifest/desktop/windows10"
                     IgnorableNamespaces="uap uap3 uap5 uap8 uap10 desktop">
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
