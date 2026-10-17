// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text;
using Microsoft.CmdPal.Common;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.UI.Xaml;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
public class SvgIconProtocolTests
{
    private const string Template = """
        <svg xmlns="http://www.w3.org/2000/svg">
          <path id="theme" fill="{{ThemeColor}}" />
          <path id="accent" fill="{{AccentColor}}" />
        </svg>
        """;

    private const string CurrentColorTemplate = """
        <svg xmlns="http://www.w3.org/2000/svg" color="{{ThemeColor}}">
          <path id="base" fill="currentColor" />
          <path id="overlay" fill="{{AccentColor}}" />
        </svg>
        """;

    [DataTestMethod]
    [DataRow(ElementTheme.Light)]
    [DataRow(ElementTheme.Dark)]
    public void ThemedSvgUsesCapturedContrastForegroundAndPlainSvgRemainsLiteral(ElementTheme theme)
    {
        var contrast = new IconContrast(IconContrastMode.High, 0xFF12AB34, 0xFF000000);
        var value = $"|ThemedSvg|danger|{Template}";
        var processor = SvgIconProtocolProcessor.Instance;
        Assert.AreEqual(contrast, processor.GetCacheContext(value, new IconRenderContext(ElementTheme.Default, contrast)).Contrast);
        Assert.IsTrue(processor.TryPrepareSynchronously(value, 20, new IconRenderContext(theme, contrast), out var prepared));
        using (prepared)
        {
            var svg = Encoding.UTF8.GetString(prepared.SvgData!);
            Assert.IsTrue(svg.Contains("fill=\"#12AB34\"", StringComparison.Ordinal));
            Assert.IsFalse(svg.Contains("{{", StringComparison.Ordinal));
            Assert.AreEqual(2, svg.Split("#12AB34").Length - 1);
        }

        var plain = $"|Svg|{Template}";
        Assert.AreEqual(default(IconContrast), processor.GetCacheContext(plain, new IconRenderContext(ElementTheme.Default, contrast)).Contrast);
        Assert.IsTrue(SvgIconProtocol.TryCreateSvg(plain, new IconRenderContext(theme, contrast), out var bytes));
        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(Template), bytes);
    }

    [TestMethod]
    public void PlainInlineSvgIsNotTransformed()
    {
        var value = $"|Svg|{Template}";

        Assert.AreEqual(SvgIconProtocol.Kind.PlainInline, SvgIconProtocol.Classify(value));
        Assert.IsTrue(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Light, default), out var lightSvg));
        Assert.IsTrue(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Dark, default), out var darkSvg));

        var expected = Encoding.UTF8.GetBytes(Template);
        CollectionAssert.AreEqual(expected, lightSvg);
        CollectionAssert.AreEqual(expected, darkSvg);
    }

    [TestMethod]
    public void PlainInlineSvgDropsAStaleEncodingDeclaration()
    {
        const string svg = "<svg xmlns=\"http://www.w3.org/2000/svg\"><title>Žluťoučký kůň</title></svg>";
        var value = $"|Svg|<?xml version=\"1.0\" encoding=\"ISO-8859-1\"?>{svg}";

        Assert.IsTrue(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Light, default), out var result));

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(svg), result);
    }

    [TestMethod]
    public void PlainInlineSvgPreservesXmlStylesheetProcessingInstruction()
    {
        const string svg = "<?xml-stylesheet href=\"icon.css\"?><svg xmlns=\"http://www.w3.org/2000/svg\" />";
        var value = $"|Svg|{svg}";

        Assert.IsTrue(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Light, default), out var result));

        CollectionAssert.AreEqual(Encoding.UTF8.GetBytes(svg), result);
    }

    [TestMethod]
    public void PlainSvgFilePreservesOriginalBytes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"CmdPal-{Guid.NewGuid():N}.svg");
        try
        {
            var template = $"<?xml version=\"1.0\" encoding=\"utf-16\"?>{Template}";
            var content = Encoding.Unicode.GetBytes(template);
            var preamble = Encoding.Unicode.GetPreamble();
            var original = new byte[preamble.Length + content.Length];
            Buffer.BlockCopy(preamble, 0, original, 0, preamble.Length);
            Buffer.BlockCopy(content, 0, original, preamble.Length, content.Length);
            File.WriteAllBytes(path, original);

            var value = $"|Svg|{path}";
            Assert.AreEqual(SvgIconProtocol.Kind.PlainFile, SvgIconProtocol.Classify(value));
            Assert.IsTrue(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Dark, default), out var svg));

            CollectionAssert.AreEqual(original, svg);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ThemedInlineSvgReplacesThemeAndDefaultInfoAccent()
    {
        var value = $"|ThemedSvg|{Template}";

        Assert.AreEqual(SvgIconProtocol.Kind.ThemedInline, SvgIconProtocol.Classify(value));
        Assert.IsTrue(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Light, default), out var lightSvg));
        Assert.IsTrue(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Dark, default), out var darkSvg));

        var light = Encoding.UTF8.GetString(lightSvg);
        var dark = Encoding.UTF8.GetString(darkSvg);
        StringAssert.Contains(light, "id=\"theme\" fill=\"#000000\"");
        StringAssert.Contains(dark, "id=\"theme\" fill=\"#FFFFFF\"");
        StringAssert.Contains(light, "id=\"accent\" fill=\"#0067C0\"");
        StringAssert.Contains(dark, "id=\"accent\" fill=\"#60CDFF\"");
        Assert.IsFalse(light.Contains("{{", StringComparison.Ordinal));
        Assert.IsFalse(dark.Contains("{{", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ThemedSvgCanSetInheritedCurrentColorWithoutRewritingKeyword()
    {
        var value = $"|ThemedSvg|success|{CurrentColorTemplate}";

        Assert.IsTrue(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Light, default), out var lightSvg));
        Assert.IsTrue(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Dark, default), out var darkSvg));

        var light = Encoding.UTF8.GetString(lightSvg);
        var dark = Encoding.UTF8.GetString(darkSvg);
        StringAssert.Contains(light, "color=\"#000000\"");
        StringAssert.Contains(dark, "color=\"#FFFFFF\"");
        StringAssert.Contains(light, "id=\"base\" fill=\"currentColor\"");
        StringAssert.Contains(dark, "id=\"base\" fill=\"currentColor\"");
        StringAssert.Contains(light, "id=\"overlay\" fill=\"#0F7B0F\"");
        StringAssert.Contains(dark, "id=\"overlay\" fill=\"#6CCB5F\"");
    }

    [DataTestMethod]
    [DataRow("danger", "#C42B1C", "#FF99A4")]
    [DataRow("subtle", "#616161", "#C5C5C5")]
    [DataRow("info", "#0067C0", "#60CDFF")]
    [DataRow("warning", "#9D5D00", "#FCE100")]
    [DataRow("success", "#0F7B0F", "#6CCB5F")]
    [DataRow("neutral", "#8A8A8A", "#9D9D9D")]
    [DataRow("dark", "#1B1A19", "#1B1A19")]
    [DataRow("normal", "#000000", "#FFFFFF")]
    public void SemanticAccentUsesLightAndDarkPalette(
        string semanticAccent,
        string expectedLight,
        string expectedDark)
    {
        var value = $"|ThemedSvg|{semanticAccent}|{Template}";

        Assert.IsTrue(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Light, default), out var lightSvg));
        Assert.IsTrue(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Dark, default), out var darkSvg));

        StringAssert.Contains(Encoding.UTF8.GetString(lightSvg), $"id=\"accent\" fill=\"{expectedLight}\"");
        StringAssert.Contains(Encoding.UTF8.GetString(darkSvg), $"id=\"accent\" fill=\"{expectedDark}\"");
    }

    [DataTestMethod]
    [DataRow("#A4C")]
    [DataRow("#7A3E9D")]
    public void OpaqueCustomSvgHexAccentIsUsedVerbatim(string customAccent)
    {
        var value = $"|ThemedSvg|{customAccent}|{Template}";

        Assert.IsTrue(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Light, default), out var lightSvg));
        Assert.IsTrue(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Dark, default), out var darkSvg));

        StringAssert.Contains(Encoding.UTF8.GetString(lightSvg), $"id=\"accent\" fill=\"{customAccent}\"");
        StringAssert.Contains(Encoding.UTF8.GetString(darkSvg), $"id=\"accent\" fill=\"{customAccent}\"");
    }

    [DataTestMethod]
    [DataRow("transparent")]
    [DataRow("#A4C8")]
    [DataRow("#7A3E9DCC")]
    [DataRow("unknown")]
    public void UnsupportedAccentIsNotStrippedDuringClassification(string unsupportedAccent)
    {
        var value = $"|ThemedSvg|{unsupportedAccent}|{Template}";

        Assert.AreEqual(SvgIconProtocol.Kind.ThemedFile, SvgIconProtocol.Classify(value));
        Assert.IsFalse(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Light, default), out var lightSvg));
        Assert.IsFalse(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Dark, default), out var darkSvg));
        Assert.AreEqual(0, lightSvg.Length);
        Assert.AreEqual(0, darkSvg.Length);
    }

    [TestMethod]
    public void ThemedSvgFileIsReadAndResolvedAsUtf8()
    {
        var path = Path.Combine(Path.GetTempPath(), $"CmdPal-{Guid.NewGuid():N}.svg");
        try
        {
            var template = $"<?xml version=\"1.0\" encoding=\"utf-16\"?>{Template}";
            File.WriteAllText(path, template, Encoding.Unicode);

            var value = $"|ThemedSvg|success|{path}";
            Assert.AreEqual(SvgIconProtocol.Kind.ThemedFile, SvgIconProtocol.Classify(value));
            Assert.IsTrue(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Dark, default), out var svg));

            var resolved = Encoding.UTF8.GetString(svg);
            Assert.IsFalse(resolved.Contains("<?xml", StringComparison.OrdinalIgnoreCase));
            StringAssert.Contains(resolved, "id=\"theme\" fill=\"#FFFFFF\"");
            StringAssert.Contains(resolved, "id=\"accent\" fill=\"#6CCB5F\"");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [TestMethod]
    public void ThemedSvgFileHonorsBomlessXmlEncodingDeclaration()
    {
        var path = Path.Combine(Path.GetTempPath(), $"CmdPal-{Guid.NewGuid():N}.svg");
        try
        {
            const string title = "Café – déjà vu";
            var template = $"<?xml version=\"1.0\" encoding=\"windows-1252\"?>" +
                $"<svg xmlns=\"http://www.w3.org/2000/svg\"><title>{title}</title>" +
                "<path id=\"theme\" fill=\"{{ThemeColor}}\" />" +
                "<path id=\"accent\" fill=\"{{AccentColor}}\" /></svg>";
            var sourceEncoding = CodePagesEncodingProvider.Instance.GetEncoding(1252);
            Assert.IsNotNull(sourceEncoding);
            File.WriteAllBytes(path, sourceEncoding.GetBytes(template));

            var value = $"|ThemedSvg|success|{path}";
            Assert.IsTrue(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Dark, default), out var svg));

            var resolved = Encoding.UTF8.GetString(svg);
            Assert.IsFalse(resolved.Contains("<?xml", StringComparison.OrdinalIgnoreCase));
            StringAssert.Contains(resolved, $"<title>{title}</title>");
            StringAssert.Contains(resolved, "id=\"theme\" fill=\"#FFFFFF\"");
            StringAssert.Contains(resolved, "id=\"accent\" fill=\"#6CCB5F\"");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [DataTestMethod]
    [DataRow("|Svg|C:\\Icons\\plain.svg", "PlainFile")]
    [DataRow("|Svg|<svg />", "PlainInline")]
    [DataRow("|ThemedSvg|C:\\Icons\\themed.svg", "ThemedFile")]
    [DataRow("|ThemedSvg|<svg />", "ThemedInline")]
    [DataRow("|ThemedSvg|warning|C:\\Icons\\themed.svg", "ThemedFile")]
    [DataRow("|ThemedSvg|#7A3E9D|<svg />", "ThemedInline")]
    public void SvgProtocolClassifiesContractAndPayload(string value, string expected) =>
        Assert.AreEqual(expected, SvgIconProtocol.Classify(value).ToString());

    [TestMethod]
    public void OnlyThemedSvgUsesThemeInCacheIdentity()
    {
        var plain = $"|Svg|{Template}";
        var themed = $"|ThemedSvg|danger|{Template}";

        Assert.AreEqual(ElementTheme.Default, SvgIconProtocol.GetCacheContext(plain, new IconRenderContext(ElementTheme.Light, default)).Theme);
        Assert.AreEqual(ElementTheme.Default, SvgIconProtocol.GetCacheContext(plain, new IconRenderContext(ElementTheme.Dark, default)).Theme);
        Assert.AreEqual(ElementTheme.Light, SvgIconProtocol.GetCacheContext(themed, new IconRenderContext(ElementTheme.Default, default)).Theme);
        Assert.AreEqual(ElementTheme.Light, SvgIconProtocol.GetCacheContext(themed, new IconRenderContext(ElementTheme.Light, default)).Theme);
        Assert.AreEqual(ElementTheme.Dark, SvgIconProtocol.GetCacheContext(themed, new IconRenderContext(ElementTheme.Dark, default)).Theme);
        Assert.AreEqual(ElementTheme.Default, SvgIconProtocol.GetCacheContext("ordinary.svg", new IconRenderContext(ElementTheme.Dark, default)).Theme);
    }

    [TestMethod]
    public void ThemedSvgCacheIdentityCanonicalizesOnlyExplicitAccentCasing()
    {
        var semantic = $"|ThemedSvg|info|{Template}";
        var semanticVariant = $"|ThemedSvg|INFO|{Template}";
        var custom = $"|ThemedSvg|#A4C|{Template}";
        var customVariant = $"|ThemedSvg|#a4c|{Template}";
        var plain = $"|Svg|<svg><path id=\"INFO\" fill=\"#a4c\" /></svg>";

        Assert.AreSame(semantic, SvgIconProtocol.GetCacheIdentity(semantic));
        Assert.AreEqual(semantic, SvgIconProtocol.GetCacheIdentity(semanticVariant));
        Assert.AreSame(custom, SvgIconProtocol.GetCacheIdentity(custom));
        Assert.AreEqual(custom, SvgIconProtocol.GetCacheIdentity(customVariant));
        Assert.AreSame(plain, SvgIconProtocol.GetCacheIdentity(plain));
    }

    [DataTestMethod]
    [DataRow("|ThemedSvg||<svg />")]
    [DataRow("|ThemedSvg|unknown|<svg />")]
    [DataRow("|ThemedSvg|UNKNOWN|<svg />")]
    [DataRow("|ThemedSvg|#ggg|<svg />")]
    [DataRow("|ThemedSvg|TRANSPARENT|<svg />")]
    [DataRow("|ThemedSvg|<svg><path id=\"A|B\" /></svg>")]
    public void UnrecognizedAccentsAndSvgPayloadsKeepTheirCacheIdentity(string value) =>
        Assert.AreSame(value, SvgIconProtocol.GetCacheIdentity(value));

    [DataTestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow("|svg|<svg />")]
    [DataRow("|Svg|")]
    [DataRow("|Svg|not-an-svg-file.txt")]
    [DataRow("|Svg|Z:\\this-file-should-not-exist\\icon.svg")]
    [DataRow("|ThemedSvg|")]
    [DataRow("|ThemedSvg|unknown|<svg />")]
    [DataRow("|ThemedSvg|#12|<svg />")]
    [DataRow("|ThemedSvg|not-an-svg-file.txt")]
    public void InvalidSvgProtocolIsRejected(string? value)
    {
        Assert.IsFalse(SvgIconProtocol.TryCreateSvg(value, new IconRenderContext(ElementTheme.Light, default), out var svg));
        Assert.AreEqual(0, svg.Length);
    }
}
