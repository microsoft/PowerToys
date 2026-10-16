// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CmdPal.Ext.Apps.Helpers;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.CmdPal.Ext.Apps.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.Ext.Apps.UnitTests;

[TestClass]
public class AppxIconLoaderTests
{
    private string _packageRoot = null!;

    [TestInitialize]
    public void TestInitialize()
    {
        _packageRoot = Path.Combine(Path.GetTempPath(), $"cmdpal-appx-icons-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_packageRoot);
    }

    [TestCleanup]
    public void TestCleanup()
    {
        if (Directory.Exists(_packageRoot))
        {
            Directory.Delete(_packageRoot, recursive: true);
        }
    }

    [DataTestMethod]
    [DataRow(Theme.Dark, "AppLogo.targetsize-16.png", "AppLogo.scale-100.png", "AppLogo.scale-100.png")]
    [DataRow(Theme.Dark, "AppLogo.targetsize-16.png", "AppLogo.targetsize-32_contrast-black.png", "AppLogo.targetsize-32_contrast-black.png")]
    [DataRow(Theme.HighContrastWhite, "AppLogo.targetsize-32.png", "AppLogo.targetsize-32_contrast-white.png", "AppLogo.targetsize-32_contrast-white.png")]
    [DataRow(Theme.HighContrastBlack, "AppLogo.targetsize-16_contrast-black.png", "AppLogo.scale-100.png", "AppLogo.scale-100.png")]
    [DataRow(Theme.HighContrastBlack, "AppLogo.targetsize-16_contrast-black.png", "AppLogo.targetsize-16.png", "AppLogo.targetsize-16_contrast-black.png")]
    [DataRow(Theme.Light, "AppLogo.targetsize-32_theme-dark.png", "AppLogo.targetsize-32_theme-light.png", "AppLogo.targetsize-32_theme-light.png")]
    [DataRow(Theme.HighContrastWhite, "AppLogo.targetsize-32_theme-dark.png", "AppLogo.targetsize-32_theme-light.png", "AppLogo.targetsize-32_theme-light.png")]
    public void LogoPathFromUri_PreservesThemeAndSizeFallbackOrder(Theme theme, string first, string second, string expected)
    {
        var directory = Path.Combine(_packageRoot, "Assets");
        CreateAsset(directory, first);
        CreateAsset(directory, second);

        var result = AppxIconLoader.LogoPathFromUri("AppLogo.png", theme, 24, CreatePackage(), resolvedResourcePath: null);

        Assert.AreEqual(Path.Combine(directory, expected), result.LogoPath);
    }

    [TestMethod]
    public void LogoPathFromUri_PriRemappedDirectory_SelectsExactTargetSize()
    {
        var physicalDirectory = Path.Combine(_packageRoot, "images", "BETA");
        var resolvedPath = CreateAsset(physicalDirectory, "Square44x44LogoBeta.scale-100.png");
        var expectedPath = CreateAsset(
            physicalDirectory,
            "Square44x44LogoBeta.targetsize-20_altform-unplated.png");
        CreateAsset(physicalDirectory, "Square44x44LogoBeta.targetsize-24_altform-unplated.png");

        var result = AppxIconLoader.LogoPathFromUri(
            @"BETA\Square44x44LogoBETA.png",
            Theme.Dark,
            20,
            CreatePackage(),
            resolvedPath);

        Assert.AreEqual(expectedPath, result.LogoPath);
        Assert.AreEqual(LogoType.Colored, result.LogoType);
        Assert.IsTrue(result.MeetsMinimumSize(20));
    }

    [TestMethod]
    [DataRow(Theme.Dark, 20, "unplated")]
    [DataRow(Theme.Dark, 64, "unplated")]
    [DataRow(Theme.Light, 64, "lightunplated")]
    public void LogoPathFromUri_SplitScalePackage_PrefersMainPackageTargetSize(Theme theme, int size, string alternateForm)
    {
        var resolvedPath = CreateAsset(Path.Combine(_packageRoot, "resource-pack", "Assets"), "AppLogo.scale-100.png");
        var mainDirectory = Path.Combine(_packageRoot, "Assets");
        foreach (var targetSize in new[] { 20, 64 })
        {
            CreateAsset(mainDirectory, $"AppLogo.targetsize-{targetSize}_altform-unplated.png");
            CreateAsset(mainDirectory, $"AppLogo.targetsize-{targetSize}_altform-lightunplated.png");
        }

        var result = AppxIconLoader.LogoPathFromUri(@"Assets\AppLogo.png", theme, size, CreatePackage(), resolvedPath);

        Assert.AreEqual(Path.Combine(mainDirectory, $"AppLogo.targetsize-{size}_altform-{alternateForm}.png"), result.LogoPath);
        Assert.IsTrue(result.MeetsMinimumSize(size));
    }

    [TestMethod]
    public void LogoPathFromUri_SplitScalePackage_PreservesPriFallbackWithoutAdequateTargetSize()
    {
        var resolvedPath = CreateAsset(Path.Combine(_packageRoot, "resource-pack", "Assets"), "AppLogo.scale-100.png");
        CreateAsset(Path.Combine(_packageRoot, "Assets"), "AppLogo.targetsize-16_altform-unplated.png");
        CreateAsset(Path.Combine(_packageRoot, "Assets"), "AppLogo.scale-200.png");

        var result = AppxIconLoader.LogoPathFromUri(@"Assets\AppLogo.png", Theme.Dark, 64, CreatePackage(), resolvedPath);

        Assert.AreEqual(resolvedPath, result.LogoPath);
    }

    [DataTestMethod]
    [DataRow(Theme.System, "", "_contrast-black", false)]
    [DataRow(Theme.Dark, "", "_contrast-black", false)]
    [DataRow(Theme.Dark, "", "_contrast-black", true)]
    [DataRow(Theme.Light, "", "_contrast-white", false)]
    [DataRow(Theme.Light, "", "_contrast-white", true)]
    [DataRow(Theme.HighContrastBlack, "_contrast-black", "", false)]
    [DataRow(Theme.HighContrastBlack, "_contrast-black", "", true)]
    [DataRow(Theme.HighContrastWhite, "_contrast-white", "", false)]
    [DataRow(Theme.HighContrastWhite, "_contrast-white", "", true)]
    [DataRow(Theme.Dark, "_contrast-standard", "_contrast-black", false)]
    [DataRow(Theme.HighContrastBlack, "_contrast-high", "", false)]
    [DataRow(Theme.HighContrastOne, "_contrast-high", "", false)]
    [DataRow(Theme.HighContrastTwo, "_contrast-high", "", false)]
    public void LogoPathFromUri_PrefersMatchingContrastScaleOverOppositeTarget(
        Theme theme,
        string matchingContrast,
        string oppositeContrast,
        bool splitResourcePackage)
    {
        var mainDirectory = Path.Combine(_packageRoot, "Assets");
        var resolvedDirectory = splitResourcePackage
            ? Path.Combine(_packageRoot, "resource-pack", "Assets")
            : mainDirectory;
        var resolvedPath = CreateAsset(resolvedDirectory, $"AppLogo.scale-100{matchingContrast}.png");
        CreateAsset(mainDirectory, $"AppLogo.targetsize-32{oppositeContrast}.png");

        var result = AppxIconLoader.LogoPathFromUri(
            @"Assets\AppLogo.png",
            theme,
            24,
            CreatePackage(),
            resolvedPath);

        Assert.AreEqual(resolvedPath, result.LogoPath);
        Assert.IsFalse(result.IsTargetSizeIcon);
    }

    [DataTestMethod]
    [DataRow(Theme.Dark, "", "_contrast-black")]
    [DataRow(Theme.Light, "", "_contrast-white")]
    [DataRow(Theme.HighContrastBlack, "_contrast-black", "")]
    [DataRow(Theme.HighContrastWhite, "_contrast-white", "")]
    public void LogoPathFromUri_SplitPackage_FindsMatchingContrastScaleAfterOppositeTarget(
        Theme theme,
        string matchingContrast,
        string oppositeContrast)
    {
        var resolvedPath = CreateAsset(
            Path.Combine(_packageRoot, "resource-pack", "Assets"),
            $"AppLogo.targetsize-32{oppositeContrast}.png");
        var expectedPath = CreateAsset(
            Path.Combine(_packageRoot, "Assets"),
            $"AppLogo.scale-100{matchingContrast}.png");

        var result = AppxIconLoader.LogoPathFromUri(
            @"Assets\AppLogo.png",
            theme,
            24,
            CreatePackage(),
            resolvedPath);

        Assert.AreEqual(expectedPath, result.LogoPath);
        Assert.IsFalse(result.IsTargetSizeIcon);
    }

    [DataTestMethod]
    [DataRow(Theme.Dark, "", "_contrast-black")]
    [DataRow(Theme.HighContrastBlack, "_contrast-black", "")]
    public void LogoPathFromUri_SplitPackage_OppositeTargetRemainsFallbackForUndersizedMatchingIcon(
        Theme theme,
        string matchingContrast,
        string oppositeContrast)
    {
        var resolvedPath = CreateAsset(
            Path.Combine(_packageRoot, "resource-pack", "Assets"),
            $"AppLogo.targetsize-16{matchingContrast}.png");
        var expectedPath = CreateAsset(
            Path.Combine(_packageRoot, "Assets"),
            $"AppLogo.targetsize-32{oppositeContrast}.png");

        var result = AppxIconLoader.LogoPathFromUri(
            @"Assets\AppLogo.png",
            theme,
            24,
            CreatePackage(),
            resolvedPath);

        Assert.AreEqual(expectedPath, result.LogoPath);
        Assert.IsTrue(result.MeetsMinimumSize(24));
    }

    [TestMethod]
    public void LogoPathFromUri_LocalizedPriDirectory_HandlesQualifierOrderAndLightTheme()
    {
        var physicalDirectory = Path.Combine(_packageRoot, "Assets", "iCloud", "en-US");
        var resolvedPath = CreateAsset(physicalDirectory, "Square44x44Logo.scale-100.png");
        var expectedPath = CreateAsset(
            physicalDirectory,
            "Square44x44Logo.altform-lightunplated_targetsize-32.png");
        CreateAsset(physicalDirectory, "Square44x44Logo.altform-unplated_targetsize-32.png");
        CreateAsset(physicalDirectory, "Square44x44Logo.targetsize-48.png");

        var result = AppxIconLoader.LogoPathFromUri(
            @"iCloud\Square44x44Logo.png",
            Theme.Light,
            32,
            CreatePackage(),
            resolvedPath);

        Assert.AreEqual(expectedPath, result.LogoPath);
        Assert.AreEqual(32, result.KnownSize);
    }

    [TestMethod]
    [DataRow(Theme.Light)]
    [DataRow(Theme.HighContrastWhite)]
    public void LogoPathFromUri_LightTheme_PrefersLightUnplatedAcrossTargetSizes(Theme theme)
    {
        var physicalDirectory = Path.Combine(_packageRoot, "Assets");
        var resolvedPath = CreateAsset(physicalDirectory, "AppLogo.scale-100.png");
        CreateAsset(physicalDirectory, "AppLogo.targetsize-20_altform-unplated.png");
        var expectedPath = CreateAsset(
            physicalDirectory,
            "AppLogo.targetsize-24_altform-lightunplated.png");

        var result = AppxIconLoader.LogoPathFromUri(
            @"Assets\AppLogo.png",
            theme,
            20,
            CreatePackage(),
            resolvedPath);

        Assert.AreEqual(expectedPath, result.LogoPath);
        Assert.AreEqual(24, result.KnownSize);
    }

    [TestMethod]
    public void LogoPathFromUri_HighContrastTargetWithAlternateForm_SelectsRequestedContrast()
    {
        var physicalDirectory = Path.Combine(_packageRoot, "images", "BETA");
        var resolvedPath = CreateAsset(physicalDirectory, "Square44x44LogoBeta.scale-100.png");
        var expectedPath = CreateAsset(
            physicalDirectory,
            "Square44x44LogoBeta.targetsize-20_altform-unplated_contrast-black.png");
        CreateAsset(
            physicalDirectory,
            "Square44x44LogoBeta.targetsize-20_altform-unplated_contrast-white.png");

        var result = AppxIconLoader.LogoPathFromUri(
            @"BETA\Square44x44LogoBETA.png",
            Theme.HighContrastBlack,
            20,
            CreatePackage(),
            resolvedPath);

        Assert.AreEqual(expectedPath, result.LogoPath);
        Assert.AreEqual(LogoType.HighContrast, result.LogoType);
    }

    [TestMethod]
    public void LogoPathFromUri_TargetSizeOutsideLegacyList_SelectsExactSize()
    {
        var physicalDirectory = Path.Combine(_packageRoot, "Assets");
        var resolvedPath = CreateAsset(physicalDirectory, "AppLogo.scale-100.png");
        CreateAsset(physicalDirectory, "AppLogo.targetsize-48_altform-unplated.png");
        var expectedPath = CreateAsset(physicalDirectory, "AppLogo.targetsize-64_altform-unplated.png");
        CreateAsset(physicalDirectory, "AppLogo.targetsize-80_altform-unplated.png");

        var result = AppxIconLoader.LogoPathFromUri(
            @"Assets\AppLogo.png",
            Theme.Dark,
            64,
            CreatePackage(),
            resolvedPath);

        Assert.AreEqual(expectedPath, result.LogoPath);
        Assert.AreEqual(64, result.KnownSize);
    }

    [TestMethod]
    public void LogoPathFromUri_ScaleOutsideLegacyList_SelectsHighestAvailableScale()
    {
        var physicalDirectory = Path.Combine(_packageRoot, "Assets");
        CreateAsset(physicalDirectory, "AppLogo.scale-200.png");
        var expectedPath = CreateAsset(physicalDirectory, "AppLogo.scale-300.png");

        var result = AppxIconLoader.LogoPathFromUri(
            @"Assets\AppLogo.png",
            Theme.Dark,
            20,
            CreatePackage(),
            resolvedResourcePath: null);

        Assert.AreEqual(expectedPath, result.LogoPath);
        Assert.IsFalse(result.IsTargetSizeIcon);
    }

    [TestMethod]
    public void LogoPathFromUri_WithoutPri_PrefersNeutralResourceContext()
    {
        var physicalDirectory = Path.Combine(_packageRoot, "Assets");
        var expectedPath = CreateAsset(physicalDirectory, "AppLogo.scale-100.png");
        CreateAsset(physicalDirectory, "AppLogo.language-ja_scale-400.png");
        CreateAsset(physicalDirectory, "AppLogo.configuration-debug_scale-450.png");

        var result = AppxIconLoader.LogoPathFromUri(
            @"Assets\AppLogo.png",
            Theme.Dark,
            20,
            CreatePackage(),
            resolvedResourcePath: null);

        Assert.AreEqual(expectedPath, result.LogoPath);
    }

    [DataTestMethod]
    [DataRow("language-en", "language-ja", false)]
    [DataRow("language-en", "language-ja", true)]
    [DataRow("configuration-retail", "configuration-debug", false)]
    [DataRow("configuration-retail", "configuration-debug", true)]
    public void LogoPathFromUri_PriContext_RejectsConflictingTargetSize(
        string resolvedContext,
        string conflictingContext,
        bool splitResourcePackage)
    {
        var mainDirectory = Path.Combine(_packageRoot, "Assets");
        var resolvedDirectory = splitResourcePackage
            ? Path.Combine(_packageRoot, "resource-pack", "Assets")
            : mainDirectory;
        var resolvedPath = CreateAsset(resolvedDirectory, $"AppLogo.{resolvedContext}_scale-100.png");
        CreateAsset(mainDirectory, $"AppLogo.{conflictingContext}_targetsize-32.png");

        var result = AppxIconLoader.LogoPathFromUri(
            @"Assets\AppLogo.png",
            Theme.Dark,
            20,
            CreatePackage(),
            resolvedPath);

        Assert.AreEqual(resolvedPath, result.LogoPath);
    }

    [TestMethod]
    public void LogoPathFromUri_PriContext_AllowsNeutralTargetSize()
    {
        var physicalDirectory = Path.Combine(_packageRoot, "Assets");
        var resolvedPath = CreateAsset(physicalDirectory, "AppLogo.language-en_scale-100.png");
        var expectedPath = CreateAsset(physicalDirectory, "AppLogo.targetsize-32.png");
        CreateAsset(physicalDirectory, "AppLogo.language-ja_targetsize-32.png");

        var result = AppxIconLoader.LogoPathFromUri(
            @"Assets\AppLogo.png",
            Theme.Dark,
            20,
            CreatePackage(),
            resolvedPath);

        Assert.AreEqual(expectedPath, result.LogoPath);
        Assert.IsTrue(result.MeetsMinimumSize(20));
    }

    [TestMethod]
    public void LogoPathFromUri_BareFileName_FallsBackToAssetsDirectory()
    {
        var physicalDirectory = Path.Combine(_packageRoot, "Assets");
        var expectedPath = CreateAsset(
            physicalDirectory,
            "AppLogo.targetsize-24_altform-unplated.png");

        var result = AppxIconLoader.LogoPathFromUri(
            "AppLogo.png",
            Theme.Dark,
            20,
            CreatePackage(),
            resolvedResourcePath: null);

        Assert.AreEqual(expectedPath, result.LogoPath);
    }

    private UWP CreatePackage()
    {
        var package = new Mock<IPackage>();
        package.SetupGet(value => value.Name).Returns("Contoso.TestApp");
        package.SetupGet(value => value.FullName).Returns("Contoso.TestApp_1.0.0.0_x64__test");
        package.SetupGet(value => value.FamilyName).Returns("Contoso.TestApp_test");

        return new UWP(package.Object)
        {
            Location = _packageRoot,
            Version = UWP.PackageVersion.Windows10,
        };
    }

    private static string CreateAsset(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, [0]);
        return path;
    }
}
