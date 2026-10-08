// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests.Charts;

[TestClass]
public class IconModelTests
{
    [TestMethod]
    public void IconParsesEveryProperty()
    {
        var warnings = new List<string>();
        var model = IconModel.Parse(
            """{ "type": "Icon", "name": "Calendar", "size": "xLarge", "style": "Filled", "color": "Accent", "horizontalAlignment": "center" }""",
            warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.AreEqual("Calendar", model.Name);
        Assert.AreEqual("\uEA89", model.Glyph);
        Assert.AreEqual(IconSize.XLarge, model.Size);
        Assert.AreEqual(40d, model.PixelSize);
        Assert.AreEqual(IconStyle.Filled, model.Style);
        Assert.AreEqual(IconColor.Accent, model.Color);
        Assert.AreEqual(IconAlignment.Center, model.HorizontalAlignment);
    }

    [TestMethod]
    public void IconDefaultsToAStandardRegularIcon()
    {
        var warnings = new List<string>();
        var model = IconModel.Parse("""{ "type": "Icon", "name": "calendar" }""", warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.AreEqual("\uE787", model.Glyph);
        Assert.AreEqual(IconSize.Standard, model.Size);
        Assert.AreEqual(24d, model.PixelSize);
        Assert.AreEqual(IconColor.Default, model.Color);
        Assert.AreEqual(IconAlignment.Left, model.HorizontalAlignment);
    }

    [TestMethod]
    public void IconSizesFollowTheFluentSizes()
    {
        var sizes = new Dictionary<string, double>
        {
            ["xxSmall"] = 12,
            ["xSmall"] = 16,
            ["Small"] = 20,
            ["Standard"] = 24,
            ["Medium"] = 28,
            ["Large"] = 32,
            ["xLarge"] = 40,
            ["xxLarge"] = 48,
        };

        foreach (var (size, pixels) in sizes)
        {
            var warnings = new List<string>();
            var model = IconModel.Parse($$"""{ "type": "Icon", "name": "Add", "size": "{{size}}" }""", warnings);

            Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
            Assert.AreEqual(pixels, model.PixelSize, size);
        }
    }

    [TestMethod]
    public void FilledIconsWithoutAFilledGlyphUseTheRegularOne()
    {
        var warnings = new List<string>();
        var model = IconModel.Parse("""{ "type": "Icon", "name": "Add", "style": "Filled" }""", warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.AreEqual("\uE710", model.Glyph);
    }

    [TestMethod]
    public void UnknownOrMissingNamesDrawNothing()
    {
        var warnings = new List<string>();
        Assert.IsNull(IconModel.Parse("""{ "type": "Icon", "name": "NotAnIcon" }""", warnings).Glyph);
        Assert.IsNull(IconModel.Parse("""{ "type": "Icon" }""", warnings).Glyph);

        Assert.AreEqual(2, warnings.Count);
    }

    [TestMethod]
    public void EveryGlyphIsAnIconFontGlyph()
    {
        foreach (var name in FluentIconGlyphs.Names)
        {
            foreach (var filled in new[] { false, true })
            {
                Assert.IsTrue(FluentIconGlyphs.TryGetGlyph(name, filled, out var glyph), name);
                Assert.AreEqual(1, glyph.Length, name);
                Assert.IsTrue(glyph[0] is >= '\uE700' and <= '\uF8FF', name);
            }
        }
    }

    [TestMethod]
    public void IconReferencesCanAskForTheFilledStyle()
    {
        Assert.AreEqual(("Checkmark", true), FluentIconGlyphs.ParseReference("Checkmark,filled"));
        Assert.AreEqual(("Checkmark", false), FluentIconGlyphs.ParseReference("Checkmark, Regular"));
        Assert.AreEqual(("Checkmark", false), FluentIconGlyphs.ParseReference(" Checkmark "));
    }
}
