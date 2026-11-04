// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.AdaptiveCards.Polyfills.UnitTests;

[TestClass]
public class ChartPaletteTests
{
    private static readonly ChartColor Accent = new(0xFF, 1, 2, 3);
    private static readonly double[] DashedPattern = [3, 3];
    private static readonly double[] DottedPattern = [0, 3];
    private static readonly double[] DashDotPattern = [3, 3, 0, 3];

    [TestMethod]
    public void NamedColorsDifferByTheme()
    {
        Assert.IsTrue(ChartPalette.TryResolve("categoricalBlue", isDarkTheme: false, Accent, out var light));
        Assert.IsTrue(ChartPalette.TryResolve("CategoricalBlue", isDarkTheme: true, Accent, out var dark));

        Assert.AreNotEqual(light, dark);
        Assert.AreEqual(0xFF, light.A);
    }

    [TestMethod]
    public void AccentResolvesToTheSystemAccent()
    {
        Assert.IsTrue(ChartPalette.TryResolve("accent", isDarkTheme: true, Accent, out var color));
        Assert.AreEqual(Accent, color);
    }

    [TestMethod]
    [DataRow("#123", 0xFF, 0x11, 0x22, 0x33)]
    [DataRow("#102030", 0xFF, 0x10, 0x20, 0x30)]
    [DataRow("#80102030", 0x80, 0x10, 0x20, 0x30)]
    public void HexColorsAreAccepted(string value, int a, int r, int g, int b)
    {
        Assert.IsTrue(ChartPalette.TryResolve(value, isDarkTheme: false, Accent, out var color));
        Assert.AreEqual(new ChartColor((byte)a, (byte)r, (byte)g, (byte)b), color);
    }

    [TestMethod]
    [DataRow("")]
    [DataRow("notAColor")]
    [DataRow("#12")]
    [DataRow("#GGGGGG")]
    public void UnknownColorsAreRejected(string value)
    {
        Assert.IsFalse(ChartPalette.TryResolve(value, isDarkTheme: false, Accent, out _));
    }

    [TestMethod]
    public void ItemColorWinsOverChartColorAndColorSet()
    {
        ChartPalette.TryResolve("good", isDarkTheme: false, Accent, out var good);
        ChartPalette.TryResolve("attention", isDarkTheme: false, Accent, out var attention);

        Assert.AreEqual(good, ChartPalette.ResolveSeriesColor("good", "attention", "diverging", 3, isDarkTheme: false, Accent));
        Assert.AreEqual(attention, ChartPalette.ResolveSeriesColor(null, "attention", "diverging", 3, isDarkTheme: false, Accent));
    }

    [TestMethod]
    public void ColorSetCyclesBySeriesIndex()
    {
        var set = ChartPalette.GetColorSet(null);
        var first = ChartPalette.ResolveSeriesColor(null, null, null, 0, isDarkTheme: true, Accent);
        var wrapped = ChartPalette.ResolveSeriesColor(null, null, null, set.Count, isDarkTheme: true, Accent);
        var second = ChartPalette.ResolveSeriesColor(null, null, null, 1, isDarkTheme: true, Accent);

        Assert.AreEqual(first, wrapped);
        Assert.AreNotEqual(first, second);
    }

    [TestMethod]
    public void SequentialSetStartsWithTheMostProminentColor()
    {
        Assert.AreEqual("sequential8", ChartPalette.GetColorSet("sequential")[0]);
        Assert.AreEqual("divergingBlue", ChartPalette.GetColorSet("Diverging")[0]);
        Assert.AreEqual("categoricalBlue", ChartPalette.GetColorSet("unknown")[0]);
    }

    [TestMethod]
    public void WithOpacityScalesAlpha()
    {
        Assert.AreEqual(0x80, new ChartColor(0xFF, 0, 0, 0).WithOpacity(0.5).A);
        Assert.AreEqual(0, new ChartColor(0xFF, 0, 0, 0).WithOpacity(0).A);
    }

    [TestMethod]
    [DataRow(5.0, "5")]
    [DataRow(0.25, "0.25")]
    [DataRow(1234.0, "1,234")]
    [DataRow(12345.0, "12.3K")]
    [DataRow(2500000.0, "2.5M")]
    [DataRow(3000000000.0, "3B")]
    [DataRow(1200000000000.0, "1.2T")]
    public void LargeNumbersUseCompactSuffixes(double value, string expected)
    {
        Assert.AreEqual(expected, ChartValueFormatter.FormatCompact(value, CultureInfo.InvariantCulture, CompactNumberFormats.English));
    }

    [TestMethod]
    [DataRow(12345.0, "12,3 Tsd.")]
    [DataRow(2500000.0, "2,5 Mio.")]
    [DataRow(-3000000000.0, "-3 Mrd.")]
    [DataRow(1234.0, "1.234")]
    public void CompactSuffixesComeFromTheFormats(double value, string expected)
    {
        var german = new CompactNumberFormats(
            CompositeFormat.Parse("{0} Tsd."),
            CompositeFormat.Parse("{0} Mio."),
            CompositeFormat.Parse("{0} Mrd."),
            CompositeFormat.Parse("{0} Bio."));

        Assert.AreEqual(expected, ChartValueFormatter.FormatCompact(value, CultureInfo.GetCultureInfo("de-DE"), german));
    }

    [TestMethod]
    public void HighContrastLinesTakeDashPatternsInTurn()
    {
        Assert.AreEqual(0, ChartPalette.GetHighContrastDashPattern(0).Count);
        CollectionAssert.AreEqual(DashedPattern, ChartPalette.GetHighContrastDashPattern(1).ToArray());
        CollectionAssert.AreEqual(DottedPattern, ChartPalette.GetHighContrastDashPattern(2).ToArray());
        CollectionAssert.AreEqual(DashDotPattern, ChartPalette.GetHighContrastDashPattern(3).ToArray());
        Assert.AreSame(ChartPalette.GetHighContrastDashPattern(1), ChartPalette.GetHighContrastDashPattern(5));
    }

    [TestMethod]
    [DataRow(0, false)]
    [DataRow(1, false)]
    [DataRow(2, true)]
    [DataRow(3, true)]
    [DataRow(4, false)]
    [DataRow(6, true)]
    public void HighContrastPartsAlternateFillsAndOutlines(int index, bool outline)
    {
        Assert.AreEqual(outline, ChartPalette.IsHighContrastOutline(index));
    }
}
