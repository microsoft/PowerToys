// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using System.Xml.Linq;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class GeneratedIconProtocolTests
{
    [DataTestMethod]
    [DataRow("|Swatch|#07A|", "#0077AA", null)]
    [DataRow("|Swatch|#807A|", "#0077AA", "0.533")]
    [DataRow("|Swatch|#102030", "#102030", null)]
    [DataRow("|Swatch|#80102030|", "#102030", "0.502")]
    public void SwatchSupportsXamlHexColorForms(string value, string expectedFill, string? expectedOpacity)
    {
        Assert.IsTrue(GeneratedIconProtocol.TryCreateSwatchSvg(value, ElementTheme.Light, out var svg));

        var shape = ParseSvg(svg).Element(SvgName("circle"));
        Assert.IsNotNull(shape);
        Assert.AreEqual(expectedFill, shape.Attribute("fill")?.Value);
        Assert.AreEqual(expectedOpacity, shape.Attribute("fill-opacity")?.Value);
        Assert.AreEqual("15.5", shape.Attribute("r")?.Value);
    }

    [TestMethod]
    public void ThemeAwareSwatchSelectsThemeColorAndUsesThemeInCacheIdentity()
    {
        const string Value = "|Swatch|#FF0067C0|#FF60CDFF|square|";

        Assert.IsTrue(GeneratedIconProtocol.TryCreateSwatchSvg(Value, ElementTheme.Light, out var lightSvg));
        Assert.IsTrue(GeneratedIconProtocol.TryCreateSwatchSvg(Value, ElementTheme.Dark, out var darkSvg));

        Assert.AreEqual("#0067C0", GetBackgroundFill(lightSvg));
        Assert.AreEqual("#60CDFF", GetBackgroundFill(darkSvg));
        Assert.AreEqual(ElementTheme.Light, GeneratedIconProtocol.GetCacheTheme(Value, ElementTheme.Light));
        Assert.AreEqual(ElementTheme.Dark, GeneratedIconProtocol.GetCacheTheme(Value, ElementTheme.Dark));
        Assert.AreEqual(ElementTheme.Light, GeneratedIconProtocol.GetCacheTheme(Value, ElementTheme.Default));
    }

    [TestMethod]
    public void SingleColorSwatchSharesCacheIdentityAcrossThemes()
    {
        const string Value = "|Swatch|#0067C0|";

        Assert.AreEqual(ElementTheme.Default, GeneratedIconProtocol.GetCacheTheme(Value, ElementTheme.Light));
        Assert.AreEqual(ElementTheme.Default, GeneratedIconProtocol.GetCacheTheme(Value, ElementTheme.Dark));
    }

    [DataTestMethod]
    [DataRow("danger", "#C42B1C", "#FF99A4", true, null)]
    [DataRow("subtle", "#616161", "#C5C5C5", true, null)]
    [DataRow("info", "#0067C0", "#60CDFF", true, null)]
    [DataRow("warning", "#9D5D00", "#FCE100", true, null)]
    [DataRow("success", "#0F7B0F", "#6CCB5F", true, null)]
    [DataRow("neutral", "#8A8A8A", "#9D9D9D", true, null)]
    [DataRow("dark", "#1B1A19", "#1B1A19", false, null)]
    [DataRow("normal", "#000000", "#FFFFFF", true, null)]
    [DataRow("transparent", "#000000", "#000000", false, "0")]
    public void SwatchSupportsSemanticColors(
        string semanticColor,
        string expectedLight,
        string expectedDark,
        bool isThemeDependent,
        string? expectedOpacity)
    {
        var value = $"|Swatch|{semanticColor}|square|";

        Assert.IsTrue(GeneratedIconProtocol.TryCreateSwatchSvg(value, ElementTheme.Light, out var lightSvg));
        Assert.IsTrue(GeneratedIconProtocol.TryCreateSwatchSvg(value, ElementTheme.Dark, out var darkSvg));

        Assert.AreEqual(expectedLight, GetBackgroundFill(lightSvg));
        Assert.AreEqual(expectedDark, GetBackgroundFill(darkSvg));
        Assert.AreEqual(expectedOpacity, GetBackgroundOpacity(lightSvg));
        Assert.AreEqual(expectedOpacity, GetBackgroundOpacity(darkSvg));
        Assert.IsNotNull(ParseSvg(lightSvg).Element(SvgName("rect")));
        Assert.AreEqual(
            isThemeDependent ? ElementTheme.Light : ElementTheme.Default,
            GeneratedIconProtocol.GetCacheTheme(value, ElementTheme.Light));
        Assert.AreEqual(
            isThemeDependent ? ElementTheme.Dark : ElementTheme.Default,
            GeneratedIconProtocol.GetCacheTheme(value, ElementTheme.Dark));
    }

    [TestMethod]
    public async Task InitialsSupportsNormalAndTransparentSemanticBackgrounds()
    {
        const string Normal = "|Initials|N|normal|circle|";
        const string Transparent = "|Initials|T|transparent|square|";

        var normalLight = await CreateSvgAsync(Normal, ElementTheme.Light);
        var normalDark = await CreateSvgAsync(Normal, ElementTheme.Dark);
        Assert.AreEqual("#000000", GetBackgroundFill(normalLight));
        Assert.AreEqual("#FFFFFF", GetBackgroundFill(normalDark));
        Assert.AreEqual("#FFFFFF", GetForegroundFill(normalLight));
        Assert.AreEqual("#000000", GetForegroundFill(normalDark));

        var transparentLight = await CreateSvgAsync(Transparent, ElementTheme.Light);
        var transparentDark = await CreateSvgAsync(Transparent, ElementTheme.Dark);
        Assert.AreEqual("0", GetBackgroundOpacity(transparentLight));
        Assert.AreEqual("0", GetBackgroundOpacity(transparentDark));
        Assert.AreEqual("#000000", GetForegroundFill(transparentLight));
        Assert.AreEqual("#FFFFFF", GetForegroundFill(transparentDark));
    }

    [TestMethod]
    public async Task TranslucentInitialsUsesThemeForContrastAndCacheIdentity()
    {
        const string Value = "|Initials|AB|#80000000|square|";

        var lightSvg = await CreateSvgAsync(Value, ElementTheme.Light);
        var darkSvg = await CreateSvgAsync(Value, ElementTheme.Dark);

        Assert.AreEqual("#000000", GetForegroundFill(lightSvg));
        Assert.AreEqual("#FFFFFF", GetForegroundFill(darkSvg));
        Assert.AreEqual(ElementTheme.Light, GeneratedIconProtocol.GetCacheTheme(Value, ElementTheme.Light));
        Assert.AreEqual(ElementTheme.Dark, GeneratedIconProtocol.GetCacheTheme(Value, ElementTheme.Dark));
    }

    [TestMethod]
    public async Task InitialsSupportsCircleSquareAndVectorGlyphs()
    {
        var circleSvg = await CreateSvgAsync(
            "|Initials|a|#FFFFFFFF|circle|",
            ElementTheme.Light);
        var squareSvg = await CreateSvgAsync(
            "|Initials|CP|#FF005FB8|#FF60CDFF|square|",
            ElementTheme.Dark);

        var circle = ParseSvg(circleSvg);
        Assert.IsNotNull(circle.Element(SvgName("circle")));
        Assert.IsFalse(string.IsNullOrEmpty(circle.Element(SvgName("path"))?.Attribute("d")?.Value));
        Assert.AreEqual("#000000", circle.Element(SvgName("path"))?.Attribute("fill")?.Value);

        var square = ParseSvg(squareSvg);
        Assert.IsNotNull(square.Element(SvgName("rect")));
        Assert.AreEqual("#60CDFF", square.Element(SvgName("rect"))?.Attribute("fill")?.Value);
        Assert.IsFalse(string.IsNullOrEmpty(square.Element(SvgName("path"))?.Attribute("d")?.Value));
    }

    [TestMethod]
    public async Task SwatchAndInitialsShareCircleAndSquareBackgroundGeometry()
    {
        Assert.IsTrue(GeneratedIconProtocol.TryCreateSwatchSvg("|Swatch|#0067C0|", ElementTheme.Light, out var circleSwatch));
        var circleInitials = await CreateSvgAsync("|Initials|A|#0067C0|", ElementTheme.Light);
        Assert.IsTrue(GeneratedIconProtocol.TryCreateSwatchSvg("|Swatch|#0067C0|square|", ElementTheme.Light, out var squareSwatch));
        var squareInitials = await CreateSvgAsync("|Initials|A|#0067C0|square|", ElementTheme.Light);

        Assert.AreEqual(GetBackgroundGeometry(circleSwatch), GetBackgroundGeometry(circleInitials));
        Assert.AreEqual(GetBackgroundGeometry(squareSwatch), GetBackgroundGeometry(squareInitials));
        Assert.AreNotEqual(GetBackgroundGeometry(circleSwatch), GetBackgroundGeometry(squareSwatch));
    }

    [DataTestMethod]
    [DataRow("Æ")]
    [DataRow("Ж")]
    [DataRow("Ω")]
    [DataRow("東")]
    [DataRow("ش")]
    [DataRow("A\u030A")]
    [DataRow("👩‍💻")]
    [DataRow("👩‍💻Å東")]
    public async Task InitialsSupportsOneToThreeUnicodeTextElements(string initials)
    {
        var svg = await CreateSvgAsync($"|Initials|{initials}|#0067C0|circle|", ElementTheme.Light);

        var path = ParseSvg(svg).Element(SvgName("path"));
        Assert.IsNotNull(path);
        Assert.IsFalse(string.IsNullOrEmpty(path.Attribute("d")?.Value));
        Assert.IsFalse(Encoding.UTF8.GetString(svg).Contains("<text", StringComparison.Ordinal));
    }

    [TestMethod]
    public async Task InitialsPercentEncodingDistinguishesSeparatorAndPercentText()
    {
        const string Separator = "|Initials|A%7CB|#0F7B0F|square|";
        const string Percent = "|Initials|%25|#0F7B0F|square|";

        var separatorSvg = await CreateSvgAsync(Separator, ElementTheme.Light);
        var percentSvg = await CreateSvgAsync(Percent, ElementTheme.Light);

        Assert.IsNotNull(ParseSvg(separatorSvg).Element(SvgName("path")));
        Assert.IsNotNull(ParseSvg(percentSvg).Element(SvgName("path")));
        Assert.AreNotEqual(
            GeneratedIconProtocol.GetCacheIdentity(Separator),
            GeneratedIconProtocol.GetCacheIdentity(Percent));
    }

    [TestMethod]
    public void InitialsCacheIdentityUsesCanonicalUnicodeAndEscaping()
    {
        const string Precomposed = "|Initials|Å|#0067C0|circle|";
        const string Decomposed = "|Initials|A\u030A|#0067C0|circle|";
        const string EscapedPrecomposed = "|Initials|%C3%85|#0067C0|circle|";
        const string CanonicalAscii = "|Initials|JP|#0067C0|circle|";
        const string LowercaseAscii = "|Initials|jp|#0067C0|circle|";
        const string PaddedAscii = "|Initials| JP |#0067C0|circle|";

        var expected = GeneratedIconProtocol.GetCacheIdentity(Precomposed);
        Assert.AreEqual(expected, GeneratedIconProtocol.GetCacheIdentity(Decomposed));
        Assert.AreEqual(expected, GeneratedIconProtocol.GetCacheIdentity(EscapedPrecomposed));
        StringAssert.Contains(
            GeneratedIconProtocol.GetCacheIdentity("|Initials|A%7CB|#0067C0|circle|"),
            "A%7CB");
        StringAssert.Contains(
            GeneratedIconProtocol.GetCacheIdentity("|Initials|%25|#0067C0|circle|"),
            "%25");
        Assert.AreEqual(
            GeneratedIconProtocol.GetCacheIdentity(CanonicalAscii),
            GeneratedIconProtocol.GetCacheIdentity(LowercaseAscii));
        Assert.AreEqual(
            GeneratedIconProtocol.GetCacheIdentity(CanonicalAscii),
            GeneratedIconProtocol.GetCacheIdentity(PaddedAscii));
    }

    [DataTestMethod]
    [DataRow("|Swatch|#fff|", "|Swatch|#FFF|")]
    [DataRow("|Swatch|#abcdef|#a1b2c3|", "|Swatch|#ABCDEF|#A1B2C3|")]
    [DataRow("|Swatch|INFO|SQUARE|", "|Swatch|info|square|")]
    [DataRow("|Initials|CP|#fff|CIRCLE|", "|Initials|CP|#FFF|circle|")]
    [DataRow("|Initials|cp|#abcdef|CIRCLE|", "|Initials|CP|#ABCDEF|circle|")]
    [DataRow("|Initials|CP|WARNING|SQUARE|", "|Initials|CP|warning|square|")]
    public void EquivalentGeneratedStyleTokensShareCacheIdentity(string value, string canonical)
    {
        Assert.AreEqual(canonical, GeneratedIconProtocol.GetCacheIdentity(value));
        Assert.AreEqual(
            GeneratedIconProtocol.GetCacheIdentity(canonical),
            GeneratedIconProtocol.GetCacheIdentity(value));
    }

    [DataTestMethod]
    [DataRow("|Swatch|#FFF|")]
    [DataRow("|Swatch|#ABCDEF|#A1B2C3|")]
    [DataRow("|Swatch|info|square|")]
    [DataRow("|Initials|A|#0067C0|circle|")]
    [DataRow("|Initials|AB|#0067C0|square|")]
    [DataRow("|Initials|JP|#0067C0|circle|")]
    [DataRow("|Initials|123|#0067C0|square|")]
    [DataRow("|Initials|CP|warning|square|")]
    public void CanonicalGeneratedIdentitiesReuseInput(string value)
    {
        Assert.AreSame(value, GeneratedIconProtocol.GetCacheIdentity(value));
    }

    [TestMethod]
    public async Task MissingInitialsFontDegradesToBackgroundTile()
    {
        var svg = await CreateSvgAsync("|Initials|\U0010FFFF|#C42B1C|square|", ElementTheme.Light);
        var root = ParseSvg(svg);

        Assert.IsNotNull(root.Element(SvgName("rect")));
        Assert.IsNull(root.Element(SvgName("path")));
    }

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("|Swatch|")]
    [DataRow("|Swatch|red|")]
    [DataRow("|Swatch|#12345|")]
    [DataRow("|Swatch|#123456|triangle|")]
    [DataRow("|Swatch|#123456|#654321|#ABCDEF|")]
    [DataRow("|swatch|#123456|")]
    [DataRow("|Initials||#123456|")]
    [DataRow("|Initials|TOOLONG|#123456|")]
    [DataRow("|Initials|ABCD|#123456|")]
    [DataRow("|Initials|A%|#123456|")]
    [DataRow("|Initials|A%7|#123456|")]
    [DataRow("|Initials|A%XX|#123456|")]
    [DataRow("|Initials|%C3|#123456|")]
    [DataRow("|Initials|%FF|#123456|")]
    [DataRow("|Initials|%F0%9F%91|#123456|")]
    [DataRow("|Initials|AB|#123456|triangle|")]
    [DataRow("|Initials|AB|unknown|circle|")]
    [DataRow("|Initials|AB|#123456|#654321|square|extra|")]
    public async Task InvalidProtocolIsRejected(string? value)
    {
        var (success, svg) = await TryCreateSvgAsync(value, ElementTheme.Light);
        Assert.IsFalse(success);
        Assert.AreEqual(0, svg.Length);
    }

    private static async Task<byte[]> CreateSvgAsync(string value, ElementTheme theme)
    {
        var (success, svg) = await TryCreateSvgAsync(value, theme);
        Assert.IsTrue(success);
        return svg;
    }

    private static async Task<(bool Success, byte[] Svg)> TryCreateSvgAsync(
        string? value,
        ElementTheme theme)
    {
        var processor = IconProtocolRegistry.Find(value);
        if (processor is null)
        {
            return (false, []);
        }

        IconPathConverter.PreparedIcon? preparedIcon;
        if (!processor.TryPrepareSynchronously(value!, 32, theme, out preparedIcon))
        {
            using var result = await processor.PrepareAsync(value!, 32, theme);
            preparedIcon = result.TakePreparedIcon();
        }

        using (preparedIcon)
        {
            return preparedIcon?.Kind == IconPathConverter.PreparedIconKind.SvgData
                && preparedIcon.SvgData is { Length: > 0 } svg
                    ? (true, svg)
                    : (false, []);
        }
    }

    private static XElement ParseSvg(byte[] svg) => XDocument.Parse(Encoding.UTF8.GetString(svg)).Root!;

    private static string? GetBackgroundFill(byte[] svg)
    {
        var root = ParseSvg(svg);
        return (root.Element(SvgName("circle")) ?? root.Element(SvgName("rect")))?.Attribute("fill")?.Value;
    }

    private static string? GetBackgroundOpacity(byte[] svg)
    {
        var root = ParseSvg(svg);
        return (root.Element(SvgName("circle")) ?? root.Element(SvgName("rect")))?.Attribute("fill-opacity")?.Value;
    }

    private static string? GetForegroundFill(byte[] svg) =>
        ParseSvg(svg).Element(SvgName("path"))?.Attribute("fill")?.Value;

    private static string GetBackgroundGeometry(byte[] svg)
    {
        var root = ParseSvg(svg);
        var background = root.Element(SvgName("circle")) ?? root.Element(SvgName("rect"));
        Assert.IsNotNull(background);

        var geometry = background.Name.LocalName;
        foreach (var attribute in background.Attributes())
        {
            if (attribute.Name.LocalName is not "fill" and not "fill-opacity")
            {
                geometry += $"|{attribute.Name.LocalName}={attribute.Value}";
            }
        }

        return geometry;
    }

    private static XName SvgName(string localName) => XName.Get(localName, "http://www.w3.org/2000/svg");
}
