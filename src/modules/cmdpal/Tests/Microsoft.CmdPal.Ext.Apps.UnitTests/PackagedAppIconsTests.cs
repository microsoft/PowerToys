// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.Ext.Apps.Packaged;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

using ContrastMode = Microsoft.CmdPal.Common.IconContrastMode;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class PackagedAppIconsTests
{
    [TestMethod]
    public void Create_PreservesReferencesWithoutCapturingTheme()
    {
        var value = PackagedAppIcons.Create("Package|%_1.0", @"C:\Packages\Space % ä", "Assets/Small|%.png", "Assets/Large.png");
        Assert.IsTrue(PackagedAppIcons.TryParse(value, out var request));
        Assert.AreEqual("Package|%_1.0", request.PackageFullName);
        Assert.AreEqual(@"C:\Packages\Space % ä", request.PackageLocation);
        Assert.AreEqual("Assets/Small|%.png", request.LogoUri);
        Assert.AreEqual("Assets/Large.png", request.LargeLogoUri);
    }

    [TestMethod]
    [DataRow("|packaged-app-icon|a|relative|Logo.png|")]
    [DataRow("|packaged-app-icon|a|C%3A%5CApps||")]
    [DataRow("|packaged-app-icon|a|C%3A%5CApps|Logo.png||extra")]
    [DataRow("|appicon|C:\\Apps\\app.exe|")]
    public void TryParse_RejectsMalformedRequests(string value)
    {
        Assert.IsFalse(PackagedAppIcons.TryParse(value, out _));
        Assert.IsFalse(PackagedAppIcons.TryResolve(value, new(false), 20, out var path));
        Assert.AreEqual(string.Empty, path);
    }

    [TestMethod]
    [DataRow(true, ContrastMode.Standard, "theme-light")]
    [DataRow(false, ContrastMode.Standard, "theme-dark")]
    [DataRow(true, ContrastMode.Black, "contrast-black")]
    [DataRow(false, ContrastMode.White, "contrast-white")]
    [DataRow(false, ContrastMode.High, "contrast-high")]
    public void TryResolve_OneReferenceSelectsEachCapturedContext(bool isLight, ContrastMode mode, string expected)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-icon-context-{Guid.NewGuid():N}");
        Directory.CreateDirectory(Path.Combine(root, "Assets"));
        try
        {
            foreach (var qualifier in new[] { "theme-light", "theme-dark", "contrast-black", "contrast-white", "contrast-high" })
            {
                File.WriteAllBytes(Path.Combine(root, "Assets", $"Logo.targetsize-32_{qualifier}.png"), [0]);
            }

            // Empty resource identities bypass PRI for this file-based fixture.
            var reference = PackagedAppIcons.Create(string.Empty, root, @"Assets\Logo.png");
            Assert.IsTrue(PackagedAppIcons.TryResolve(reference, new(isLight, mode), 20, out var path));
            Assert.AreEqual(Path.Combine(root, "Assets", $"Logo.targetsize-32_{expected}.png"), path);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void TryResolve_HeroUsesLargeLogoWithoutChangingListReference()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-icon-hero-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllBytes(Path.Combine(root, "Small.targetsize-20.png"), [0]);
            File.WriteAllBytes(Path.Combine(root, "Large.targetsize-64.png"), [0]);
            var reference = PackagedAppIcons.Create(string.Empty, root, "Small.png", "Large.png");
            Assert.IsTrue(PackagedAppIcons.TryResolve(reference, new(false), 20, out var list));
            Assert.IsTrue(PackagedAppIcons.TryResolve(reference, new(false), 64, out var hero));
            Assert.AreEqual(Path.Combine(root, "Small.targetsize-20.png"), list);
            Assert.AreEqual(Path.Combine(root, "Large.targetsize-64.png"), hero);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow(true, ContrastMode.Standard, "theme-light")]
    [DataRow(false, ContrastMode.Standard, "theme-dark")]
    [DataRow(true, ContrastMode.Black, "contrast-black")]
    [DataRow(false, ContrastMode.White, "contrast-white")]
    [DataRow(true, ContrastMode.High, "contrast-high")]
    public void TryResolve_PriQualifierFoldersUseRequestContext(bool isLight, ContrastMode mode, string expected)
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-icon-pri-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            WritePriFixture(root);
            var reference = PackagedAppIcons.Create("Contoso.TestApp_1.0.0.0_x64__test", root, @"Assets\Logo.png");
            Assert.IsTrue(PackagedAppIcons.TryResolve(reference, new(isLight, mode), 20, out var path));
            Assert.AreEqual(Path.Combine(root, "Assets", expected, "Logo.png"), path);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void TryResolve_ParallelContextsShareManagerWithoutSharingQualifiers()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-icon-parallel-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            WritePriFixture(root);
            var request = new PackagedAppIcons.Request("Contoso.TestApp_1.0.0.0_x64__test", root, @"Assets\Logo.png");
            var reference = PackagedAppIcons.Create(request.PackageFullName, root, request.LogoUri);
            var contexts = new (PackagedIconTheme Theme, string Qualifier)[]
            {
                (new(true), "theme-light"),
                (new(false), "theme-dark"),
                (new(true, ContrastMode.Black), "contrast-black"),
                (new(false, ContrastMode.White), "contrast-white"),
                (new(false, ContrastMode.High), "contrast-high"),
            };
            var managers = new object[64];
            Parallel.For(0, managers.Length, i =>
            {
                managers[i] = PackagedAppIcons.GetResourceManager(request);
                Assert.IsNotNull(managers[i]);
                var context = contexts[i % contexts.Length];
                Assert.IsTrue(PackagedAppIcons.TryResolve(reference, context.Theme, 20, out var path));
                Assert.AreEqual(Path.Combine(root, "Assets", context.Qualifier, "Logo.png"), path);
            });
            foreach (var manager in managers)
            {
                Assert.AreSame(managers[0], manager);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void TryResolve_FailedPriReadDoesNotPoisonLaterRequests()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-icon-pri-recovery-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var reference = PackagedAppIcons.Create("Contoso.TestApp_1.0.0.0_x64__test", root, @"Assets\Logo.png");
            Assert.IsTrue(PackagedAppIcons.TryParse(reference, out var request));
            Assert.IsNull(PackagedAppIcons.GetResourceManager(request));
            Assert.IsNull(PackagedAppIcons.GetResourceManager(request));
            Assert.IsFalse(PackagedAppIcons.TryResolve(reference, new(true), 20, out _));
            WritePriFixture(root);
            Assert.IsTrue(PackagedAppIcons.TryResolve(reference, new(true), 20, out var path));
            Assert.AreEqual(Path.Combine(root, "Assets", "theme-light", "Logo.png"), path);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    [DoNotParallelize]
    public void TryResolve_CorruptPriLogsOnceAcrossWorkersAndStillUsesFileFallback()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-icon-pri-corrupt-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        var previousLogger = CoreLogger.Instance;
        var logger = new Mock<ILogger>();
        CoreLogger.InitializeLogger(logger.Object);
        try
        {
            File.WriteAllBytes(Path.Combine(root, "resources.pri"), [0]);
            Directory.CreateDirectory(Path.Combine(root, "Assets"));
            var asset = Path.Combine(root, "Assets", "Logo.targetsize-32.png");
            File.WriteAllBytes(asset, [0]);
            var reference = PackagedAppIcons.Create("Contoso.TestApp_1.0.0.0_x64__test", root, @"Assets\Logo.png");
            Parallel.For(0, 32, _ =>
            {
                Assert.IsTrue(PackagedAppIcons.TryResolve(reference, new(false), 20, out var path));
                Assert.AreEqual(asset, path);
            });
            logger.Verify(
                log => log.LogWarning(
                    It.Is<string>(message => message.Contains("Contoso.TestApp_1.0.0.0_x64__test", StringComparison.Ordinal)),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<int>()),
                Times.Once);
            File.Delete(asset);
            WritePriFixture(root);
            Assert.IsTrue(PackagedAppIcons.TryResolve(reference, new(true), 20, out var recovered));
            Assert.AreEqual(Path.Combine(root, "Assets", "theme-light", "Logo.png"), recovered);
        }
        finally
        {
            CoreLogger.InitializeLogger(previousLogger!);
            Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void TryResolve_MissingVariantFallsBackAndMissingLogoIsEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cmdpal-icon-fallback-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            var asset = Path.Combine(root, "Logo.targetsize-32.png");
            File.WriteAllBytes(asset, [0]);
            var reference = PackagedAppIcons.Create(string.Empty, root, "Logo.png");
            Assert.IsTrue(PackagedAppIcons.TryResolve(reference, new(true, ContrastMode.White), 20, out var fallback));
            Assert.AreEqual(asset, fallback);
            File.Delete(asset);
            Assert.IsFalse(PackagedAppIcons.TryResolve(reference, new(true, ContrastMode.White), 20, out var missing));
            Assert.AreEqual(string.Empty, missing);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static void WritePriFixture(string root)
    {
        // Generated with MakePri for Contoso.TestApp: five Assets/<qualifier>/Logo.png files,
        // default qualifiers en-US. No package registration or Windows theme change is needed.
        using var fixture = typeof(PackagedAppIconsTests).Assembly.GetManifestResourceStream(
            "Microsoft.CmdPal.Ext.Apps.UnitTests.TestData.PackagedIcons.pri");
        Assert.IsNotNull(fixture);
        using (var output = File.Create(Path.Combine(root, "resources.pri")))
        {
            fixture.CopyTo(output);
        }

        foreach (var qualifier in new[] { "theme-light", "theme-dark", "contrast-black", "contrast-white", "contrast-high" })
        {
            var directory = Path.Combine(root, "Assets", qualifier);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, "Logo.png"), [0]);
        }
    }
}
