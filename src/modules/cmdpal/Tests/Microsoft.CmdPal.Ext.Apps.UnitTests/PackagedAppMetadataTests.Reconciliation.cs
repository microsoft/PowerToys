// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.Apps.Catalog;
using Microsoft.CmdPal.Ext.Apps.Catalog.Sources;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.ApplicationModel;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

public partial class PackagedAppMetadataTests
{
    [STATestMethod]
    [DoNotParallelize]
    public async Task BackgroundLoad_UnchangedPackagesDoNotOpenManifests()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-package-probe-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var manifestPath = Path.Combine(root, "AppxManifest.xml");
        var originalManager = UWP.PackageManagerWrapper;
        try
        {
            File.WriteAllText(manifestPath, CreateManifest("before.exe"));
            var package = new Mock<IPackage>();
            package.SetupGet(value => value.Name).Returns("Manifest fixture");
            package.SetupGet(value => value.FullName).Returns("CmdPal.MetadataFixture_1.0.0.0_neutral__123");
            package.SetupGet(value => value.FamilyName).Returns("CmdPal.MetadataFixture_123");
            package.SetupGet(value => value.InstalledLocation).Returns(root);
            IPackage[] packages = [package.Object];
            var manager = new Mock<IPackageManager>();
            manager.Setup(value => value.FindPackagesForCurrentUser()).Returns(() => packages);
            UWP.PackageManagerWrapper = manager.Object;
            using var source = new PackagedAppSource(new SilentPackageCatalog());
            var initial = (AppSourceScanResult)await source.LoadAsync(CancellationToken.None);
            Assert.IsTrue(initial.IsComplete);
            Assert.AreEqual(1, initial.Count);

            using (var writer = File.Open(manifestPath, FileMode.Open, FileAccess.Write, FileShare.None))
            {
                var unchanged = await source.LoadAsync(CancellationToken.None, background: true);
                Assert.AreSame(initial, unchanged, "An unchanged manifest must not be opened, even if another process denies reads.");
            }

            File.WriteAllText(manifestPath, CreateManifest("after-update.exe"));
            var updated = await source.LoadAsync(CancellationToken.None, background: true);
            Assert.AreEqual("after-update.exe", ((PackagedAppSnapshot)updated.Single().Payload).Executable);

            packages = [];
            var removed = await source.LoadAsync(CancellationToken.None, background: true);
            Assert.AreEqual(0, removed.Count);

            packages = [package.Object];
            var restored = await source.LoadAsync(CancellationToken.None, background: true);
            Assert.AreEqual(1, restored.Count);
        }
        finally
        {
            UWP.PackageManagerWrapper = originalManager;
            Directory.Delete(root, recursive: true);
        }
    }

    [STATestMethod]
    [DoNotParallelize]
    public async Task Recovery_FailedManifestPreservesOnlyItsPackageFamily()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-scoped-packages-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var originalManager = UWP.PackageManagerWrapper;
        try
        {
            var fixtures = new[] { "Locked", "Removed" };
            var packages = fixtures.Select(name =>
            {
                var location = Directory.CreateDirectory(Path.Combine(root, name)).FullName;
                File.WriteAllText(Path.Combine(location, "AppxManifest.xml"), CreateManifest("app.exe").Replace("CmdPal.MetadataFixture", $"CmdPal.{name}", StringComparison.Ordinal));
                var package = new Mock<IPackage>();
                package.SetupGet(value => value.Name).Returns(name);
                package.SetupGet(value => value.FullName).Returns($"CmdPal.{name}_1.0.0.0_neutral__123");
                package.SetupGet(value => value.FamilyName).Returns($"CmdPal.{name}_123");
                package.SetupGet(value => value.InstalledLocation).Returns(location);
                return package.Object;
            }).ToArray();
            var manager = new Mock<IPackageManager>();
            manager.Setup(value => value.FindPackagesForCurrentUser()).Returns(() => packages);
            UWP.PackageManagerWrapper = manager.Object;
            using var source = new PackagedAppSource(new SilentPackageCatalog());
            var initial = await source.LoadAsync(CancellationToken.None);
            Assert.AreEqual(2, initial.Count);
            packages = [packages[0]];

            using var writer = File.Open(Path.Combine(root, "Locked", "AppxManifest.xml"), FileMode.Open, FileAccess.Write, FileShare.None);
            var scan = (AppSourceScanResult)await source.LoadAsync(CancellationToken.None);
            Assert.IsFalse(scan.IsComplete);
            Assert.IsNotNull(scan.FailedPaths, "The failed package is known; this is not a failure to enumerate packages.");
            string[] expectedFamilies = ["CmdPal.Locked_123"];
            CollectionAssert.AreEqual(expectedFamilies, scan.FailedPackageFamilies.ToArray());
            CollectionAssert.AreEqual(new[] { string.Empty }, scan.RetryPaths.ToArray());
            Assert.IsNotNull(scan.GetRetainedItem(initial.Single(item => ((PackagedAppSnapshot)item.Payload).PackageFamilyName == "CmdPal.Locked_123")));
            Assert.IsNull(scan.GetRetainedItem(initial.Single(item => ((PackagedAppSnapshot)item.Payload).PackageFamilyName == "CmdPal.Removed_123")));
        }
        finally
        {
            UWP.PackageManagerWrapper = originalManager;
            Directory.Delete(root, recursive: true);
        }
    }

    private sealed class SilentPackageCatalog : IPackageCatalog
    {
        public event TypedEventHandler<PackageCatalog, PackageInstallingEventArgs> PackageInstalling
        {
            add
            {
            }

            remove
            {
            }
        }

        public event TypedEventHandler<PackageCatalog, PackageUninstallingEventArgs> PackageUninstalling
        {
            add
            {
            }

            remove
            {
            }
        }

        public event TypedEventHandler<PackageCatalog, PackageUpdatingEventArgs> PackageUpdating
        {
            add
            {
            }

            remove
            {
            }
        }
    }
}
