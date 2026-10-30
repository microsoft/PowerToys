// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.CmdPal.Ext.Apps.Helpers;
using Microsoft.CmdPal.Ext.Apps.Programs;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using ContrastMode = Microsoft.CmdPal.Common.IconContrastMode;

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
    [DataRow(false, ContrastMode.Standard, "AppLogo.targetsize-16.png", "AppLogo.scale-100.png", "AppLogo.scale-100.png")]
    [DataRow(false, ContrastMode.Standard, "AppLogo.targetsize-16.png", "AppLogo.targetsize-32_contrast-black.png", "AppLogo.targetsize-32_contrast-black.png")]
    [DataRow(true, ContrastMode.White, "AppLogo.targetsize-32.png", "AppLogo.targetsize-32_contrast-white.png", "AppLogo.targetsize-32_contrast-white.png")]
    [DataRow(false, ContrastMode.Black, "AppLogo.targetsize-16_contrast-black.png", "AppLogo.scale-100.png", "AppLogo.scale-100.png")]
    [DataRow(false, ContrastMode.Black, "AppLogo.targetsize-16_contrast-black.png", "AppLogo.targetsize-16.png", "AppLogo.targetsize-16_contrast-black.png")]
    [DataRow(true, ContrastMode.Standard, "AppLogo.targetsize-32_theme-dark.png", "AppLogo.targetsize-32_theme-light.png", "AppLogo.targetsize-32_theme-light.png")]
    [DataRow(true, ContrastMode.White, "AppLogo.targetsize-32_theme-dark.png", "AppLogo.targetsize-32_theme-light.png", "AppLogo.targetsize-32_theme-light.png")]
    public void LogoPathFromUri_PreservesThemeAndSizeFallbackOrder(bool isLight, ContrastMode contrastMode, string first, string second, string expected)
    {
        var directory = Path.Combine(_packageRoot, "Assets");
        CreateAsset(directory, first);
        CreateAsset(directory, second);

        var result = AppxIconLoader.LogoPathFromUri("AppLogo.png", new PackagedIconTheme(isLight, contrastMode), 24, CreatePackage(), resolvedResourcePath: null);

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
            new PackagedIconTheme(false),
            20,
            CreatePackage(),
            resolvedPath);

        Assert.AreEqual(expectedPath, result.LogoPath);
        Assert.IsFalse(result.IsHighContrast);
        Assert.IsTrue(result.MeetsMinimumSize(20));
    }

    [TestMethod]
    [DataRow(false, ContrastMode.Standard, 20, "unplated")]
    [DataRow(false, ContrastMode.Standard, 64, "unplated")]
    [DataRow(true, ContrastMode.Standard, 64, "lightunplated")]
    public void LogoPathFromUri_SplitScalePackage_PrefersMainPackageTargetSize(bool isLight, ContrastMode contrastMode, int size, string alternateForm)
    {
        var resolvedPath = CreateAsset(Path.Combine(_packageRoot, "resource-pack", "Assets"), "AppLogo.scale-100.png");
        var mainDirectory = Path.Combine(_packageRoot, "Assets");
        foreach (var targetSize in new[] { 20, 64 })
        {
            CreateAsset(mainDirectory, $"AppLogo.targetsize-{targetSize}_altform-unplated.png");
            CreateAsset(mainDirectory, $"AppLogo.targetsize-{targetSize}_altform-lightunplated.png");
        }

        var result = AppxIconLoader.LogoPathFromUri(@"Assets\AppLogo.png", new PackagedIconTheme(isLight, contrastMode), size, CreatePackage(), resolvedPath);

        Assert.AreEqual(Path.Combine(mainDirectory, $"AppLogo.targetsize-{size}_altform-{alternateForm}.png"), result.LogoPath);
        Assert.IsTrue(result.MeetsMinimumSize(size));
    }

    [TestMethod]
    public void LogoPathFromUri_SplitScalePackage_PreservesPriFallbackWithoutAdequateTargetSize()
    {
        var resolvedPath = CreateAsset(Path.Combine(_packageRoot, "resource-pack", "Assets"), "AppLogo.scale-100.png");
        CreateAsset(Path.Combine(_packageRoot, "Assets"), "AppLogo.targetsize-16_altform-unplated.png");
        CreateAsset(Path.Combine(_packageRoot, "Assets"), "AppLogo.scale-200.png");

        var result = AppxIconLoader.LogoPathFromUri(@"Assets\AppLogo.png", new PackagedIconTheme(false), 64, CreatePackage(), resolvedPath);

        Assert.AreEqual(resolvedPath, result.LogoPath);
    }

    [DataTestMethod]
    [DataRow(false, ContrastMode.Standard, "", "_contrast-black", false)]
    [DataRow(false, ContrastMode.Standard, "", "_contrast-black", true)]
    [DataRow(true, ContrastMode.Standard, "", "_contrast-white", false)]
    [DataRow(true, ContrastMode.Standard, "", "_contrast-white", true)]
    [DataRow(false, ContrastMode.Black, "_contrast-black", "", false)]
    [DataRow(false, ContrastMode.Black, "_contrast-black", "", true)]
    [DataRow(true, ContrastMode.White, "_contrast-white", "", false)]
    [DataRow(true, ContrastMode.White, "_contrast-white", "", true)]
    [DataRow(false, ContrastMode.Standard, "_contrast-standard", "_contrast-black", false)]
    [DataRow(false, ContrastMode.Black, "_contrast-high", "", false)]
    public void LogoPathFromUri_PrefersMatchingContrastScaleOverOppositeTarget(
        bool isLight,
        ContrastMode contrastMode,
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
            new PackagedIconTheme(isLight, contrastMode),
            24,
            CreatePackage(),
            resolvedPath);

        Assert.AreEqual(resolvedPath, result.LogoPath);
        Assert.IsFalse(result.IsTargetSizeIcon);
    }

    [DataTestMethod]
    [DataRow(false, ContrastMode.Standard, "", "_contrast-black")]
    [DataRow(true, ContrastMode.Standard, "", "_contrast-white")]
    [DataRow(false, ContrastMode.Black, "_contrast-black", "")]
    [DataRow(true, ContrastMode.White, "_contrast-white", "")]
    public void LogoPathFromUri_SplitPackage_FindsMatchingContrastScaleAfterOppositeTarget(
        bool isLight,
        ContrastMode contrastMode,
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
            new PackagedIconTheme(isLight, contrastMode),
            24,
            CreatePackage(),
            resolvedPath);

        Assert.AreEqual(expectedPath, result.LogoPath);
        Assert.IsFalse(result.IsTargetSizeIcon);
    }

    [DataTestMethod]
    [DataRow(false, ContrastMode.Standard, "", "_contrast-black")]
    [DataRow(false, ContrastMode.Black, "_contrast-black", "")]
    public void LogoPathFromUri_SplitPackage_OppositeTargetRemainsFallbackForUndersizedMatchingIcon(
        bool isLight,
        ContrastMode contrastMode,
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
            new PackagedIconTheme(isLight, contrastMode),
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
            new PackagedIconTheme(true),
            32,
            CreatePackage(),
            resolvedPath);

        Assert.AreEqual(expectedPath, result.LogoPath);
        Assert.AreEqual(32, result.KnownSize);
    }

    [TestMethod]
    [DataRow(true, ContrastMode.Standard)]
    [DataRow(true, ContrastMode.White)]
    public void LogoPathFromUri_LightTheme_PrefersLightUnplatedAcrossTargetSizes(bool isLight, ContrastMode contrastMode)
    {
        var physicalDirectory = Path.Combine(_packageRoot, "Assets");
        var resolvedPath = CreateAsset(physicalDirectory, "AppLogo.scale-100.png");
        CreateAsset(physicalDirectory, "AppLogo.targetsize-20_altform-unplated.png");
        var expectedPath = CreateAsset(
            physicalDirectory,
            "AppLogo.targetsize-24_altform-lightunplated.png");

        var result = AppxIconLoader.LogoPathFromUri(
            @"Assets\AppLogo.png",
            new PackagedIconTheme(isLight, contrastMode),
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
            new PackagedIconTheme(false, ContrastMode.Black),
            20,
            CreatePackage(),
            resolvedPath);

        Assert.AreEqual(expectedPath, result.LogoPath);
        Assert.IsTrue(result.IsHighContrast);
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
            new PackagedIconTheme(false),
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
            new PackagedIconTheme(false),
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
            new PackagedIconTheme(false),
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
            new PackagedIconTheme(false),
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
            new PackagedIconTheme(false),
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
            new PackagedIconTheme(false),
            20,
            CreatePackage(),
            resolvedResourcePath: null);

        Assert.AreEqual(expectedPath, result.LogoPath);
    }

    private PackagedAppIcons.Request CreatePackage()
    {
        return new("Contoso.TestApp_1.0.0.0_x64__test", _packageRoot, "AppLogo.png");
    }

    private static string CreateAsset(string directory, string fileName)
    {
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, fileName);
        File.WriteAllBytes(path, [0]);
        return path;
    }
}
