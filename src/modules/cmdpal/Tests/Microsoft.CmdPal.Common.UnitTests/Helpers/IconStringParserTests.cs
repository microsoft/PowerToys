// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Helpers;

namespace Microsoft.CmdPal.Common.UnitTests.Helpers;

[TestClass]
public class IconStringParserTests
{
    [TestMethod]
    public void Parse_FileUriPng_ReturnsManagedImageSource()
    {
        var result = IconStringParser.Parse("file:///X:/some/path/StoreLogo.png");

        Assert.AreEqual(IconStringKind.ImageSource, result.Kind);
        Assert.IsNotNull(result.Uri);
    }

    [TestMethod]
    public void Parse_LocalSvgPath_ReturnsManagedImageSource()
    {
        var result = IconStringParser.Parse(@"C:\Temp\Assets\ExtensionIconPlaceholder.svg");

        Assert.AreEqual(IconStringKind.ImageSource, result.Kind);
        Assert.IsTrue(result.Uri?.IsFile);
    }

    [TestMethod]
    public void Parse_MsAppxUri_ReturnsManagedImageSource()
    {
        var result = IconStringParser.Parse("ms-appx:///Assets/Icons/ExtensionIconPlaceholder.png");

        Assert.AreEqual(IconStringKind.ImageSource, result.Kind);
        Assert.AreEqual("ms-appx", result.Uri?.Scheme);
    }

    [TestMethod]
    public void Parse_HttpsUriWithoutImageExtension_ReturnsManagedImageSource()
    {
        var result = IconStringParser.Parse("https://contoso.example/icon?id=123");

        Assert.AreEqual(IconStringKind.ImageSource, result.Kind);
        Assert.AreEqual(Uri.UriSchemeHttps, result.Uri?.Scheme);
    }

    [TestMethod]
    public void Parse_BinaryIconReferenceWithIndex_ReturnsNativeBinaryIcon()
    {
        var result = IconStringParser.Parse(@"C:\Windows\System32\shell32.dll,210");

        Assert.AreEqual(IconStringKind.ShellIcon, result.Kind);
        Assert.AreEqual(@"C:\Windows\System32\shell32.dll", result.BinaryPath);
        Assert.AreEqual(210, result.BinaryIconIndex);
    }

    [TestMethod]
    public void Parse_BinaryIconReferenceWithoutIndex_DefaultsToZero()
    {
        var result = IconStringParser.Parse(@"C:\Windows\System32\notepad.exe");

        Assert.AreEqual(IconStringKind.ShellIcon, result.Kind);
        Assert.AreEqual(@"C:\Windows\System32\notepad.exe", result.BinaryPath);
        Assert.AreEqual(0, result.BinaryIconIndex);
    }

    [TestMethod]
    public void Parse_FileUriBinaryIconReference_UsesLocalPath()
    {
        var result = IconStringParser.Parse("file:///C:/Windows/System32/shell32.dll,210");

        Assert.AreEqual(IconStringKind.ShellIcon, result.Kind);
        StringAssert.EndsWith(result.BinaryPath, @"Windows\System32\shell32.dll");
        Assert.AreEqual(210, result.BinaryIconIndex);
    }

    [TestMethod]
    public void Parse_GlyphIcon_ReturnsManagedGlyphSource()
    {
        var result = IconStringParser.Parse("\uE8C8");

        Assert.AreEqual(IconStringKind.Glyph, result.Kind);
        Assert.IsNull(result.Uri);
        Assert.AreEqual("\uE8C8", result.Glyph);
    }

    [TestMethod]
    public void Parse_CmdPalIconChain_ReturnsDescriptor()
    {
        var iconString = CmdPalUri.CreateIcon(
            [
                new CmdPalIconSourceCandidate(@"C:\Apps\Contoso.lnk", CmdPalIconSourceKind.Thumbnail),
                new CmdPalIconSourceCandidate(@"C:\Apps\Contoso.exe"),
            ]);

        var result = IconStringParser.Parse(iconString);

        Assert.AreEqual(IconStringKind.CmdPalIcon, result.Kind);
        Assert.IsNotNull(result.CmdPalIcon);
        Assert.AreEqual(2, result.CmdPalIcon.Sources.Count);
        Assert.AreEqual(CmdPalIconSourceKind.Thumbnail, result.CmdPalIcon.Sources[0].Kind);
        Assert.AreEqual("file:///C:/Apps/Contoso.lnk", result.CmdPalIcon.Sources[0].Source);
        Assert.AreEqual(CmdPalIconSourceKind.Icon, result.CmdPalIcon.Sources[1].Kind);
        Assert.AreEqual("file:///C:/Apps/Contoso.exe", result.CmdPalIcon.Sources[1].Source);
    }

    [TestMethod]
    public void Parse_CmdPalThemedIcon_ReturnsThemeSpecificSources()
    {
        var iconString = CmdPalUri.CreateThemedIcon(
            lightSources:
            [
                new CmdPalIconSourceCandidate("ms-appx:///Assets/Light.png"),
            ],
            darkSources:
            [
                new CmdPalIconSourceCandidate(@"C:\Apps\Contoso.dark.ico", CmdPalIconSourceKind.Thumbnail),
            ]);

        var result = IconStringParser.Parse(iconString);

        Assert.AreEqual(IconStringKind.CmdPalIcon, result.Kind);
        Assert.IsNotNull(result.CmdPalIcon);
        Assert.AreEqual(1, result.CmdPalIcon.LightSources.Count);
        Assert.AreEqual("ms-appx:///Assets/Light.png", result.CmdPalIcon.LightSources[0].Source);
        Assert.AreEqual(1, result.CmdPalIcon.DarkSources.Count);
        Assert.AreEqual(CmdPalIconSourceKind.Thumbnail, result.CmdPalIcon.DarkSources[0].Kind);
        Assert.AreEqual("file:///C:/Apps/Contoso.dark.ico", result.CmdPalIcon.DarkSources[0].Source);
    }

    [TestMethod]
    public void Parse_CmdPalGlyphWithFont_ReturnsGlyphOverride()
    {
        var result = IconStringParser.Parse(CmdPalUri.CreateGlyph("\uE700", "Wingdings"));

        Assert.AreEqual(IconStringKind.Glyph, result.Kind);
        Assert.AreEqual("\uE700", result.Glyph);
        Assert.AreEqual("Wingdings", result.FontFamily);
    }

    [TestMethod]
    public void Parse_CmdPalIconWithNestedUriQuery_PreservesEscapedValue()
    {
        var iconString = CmdPalUri.CreateIcon(
        [
            new CmdPalIconSourceCandidate("https://contoso.example/icon?id=123&mode=light"),
            new CmdPalIconSourceCandidate(@"C:\Apps\Contoso.exe", CmdPalIconSourceKind.Thumbnail),
        ]);

        var result = IconStringParser.Parse(iconString);

        Assert.AreEqual(IconStringKind.CmdPalIcon, result.Kind);
        Assert.IsNotNull(result.CmdPalIcon);
        Assert.AreEqual(2, result.CmdPalIcon.Sources.Count);
        Assert.AreEqual("https://contoso.example/icon?id=123&mode=light", result.CmdPalIcon.Sources[0].Source);
        Assert.AreEqual(CmdPalIconSourceKind.Thumbnail, result.CmdPalIcon.Sources[1].Kind);
        Assert.AreEqual("file:///C:/Apps/Contoso.exe", result.CmdPalIcon.Sources[1].Source);
    }

    [TestMethod]
    public void Parse_CmdPalNilIcon_ReturnsNullSource()
    {
        var result = IconStringParser.Parse(CmdPalUri.CreateNilIcon());

        Assert.AreEqual(IconStringKind.NullSource, result.Kind);
    }

    [TestMethod]
    public void RequiresTheme_CmdPalThemedIcon_ReturnsTrue()
    {
        var iconString = CmdPalUri.CreateThemedIcon(
            lightSources: [new CmdPalIconSourceCandidate("ms-appx:///Assets/Light.png")],
            darkSources: [new CmdPalIconSourceCandidate("ms-appx:///Assets/Dark.png")]);

        Assert.IsTrue(IconStringParser.RequiresTheme(iconString));
    }

    [TestMethod]
    public void RequiresTheme_UnthemedCmdPalIcon_ReturnsFalse()
    {
        var iconString = CmdPalUri.CreateIcon(
        [
            new CmdPalIconSourceCandidate(@"C:\Apps\Contoso.exe"),
        ]);

        Assert.IsFalse(IconStringParser.RequiresTheme(iconString));
    }

    [TestMethod]
    public void Parse_ResUriBinaryIconReference_UsesLocalPath()
    {
        var result = IconStringParser.Parse("res:///C:/Windows/System32/shell32.dll,-137");

        Assert.AreEqual(IconStringKind.ShellIcon, result.Kind);
        Assert.AreEqual(@"C:\Windows\System32\shell32.dll", result.BinaryPath);
        Assert.AreEqual(-137, result.BinaryIconIndex);
    }
}
