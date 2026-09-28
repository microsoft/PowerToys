// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Globalization;
using Microsoft.CmdPal.UI.ViewModels.Dock;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class DockLabelWidthConstraintsTests
{
    [TestMethod]
    [DataRow(80d, 80d, false)]
    [DataRow(0d, 0d, false)]
    [DataRow("10ch", 10d, true)]
    [DataRow("2.5ch", 2.5d, true)]
    [DataRow("0ch", 0d, true)]
    [DataRow("5E-324ch", double.Epsilon, true)]
    public void Parse_AcceptsDipsAndCharacterWidths(object value, double amount, bool inCharacters)
    {
        Assert.AreEqual(new DockLabelLength(amount, inCharacters), DockLabelLength.Parse(value));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(-1d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.MaxValue)]
    [DataRow(80)]
    [DataRow(true)]
    [DataRow("")]
    [DataRow("80")]
    [DataRow("10px")]
    [DataRow("10CH")]
    [DataRow("10 ch")]
    [DataRow("-1ch")]
    [DataRow("NaNch")]
    [DataRow("1,5ch")]
    [DataRow("1200sqh")]
    [DataRow("text")]
    [DataRow("Text:CPU")]
    public void Parse_IgnoresUnsupportedOrInvalidValues(object? value)
    {
        Assert.IsNull(DockLabelLength.Parse(value));
    }

    [TestMethod]
    public void Parse_UsesInvariantDecimalSeparator()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("cs-CZ");
            Assert.AreEqual(new DockLabelLength(1.5, InCharacters: true), DockLabelLength.Parse("1.5ch"));
            Assert.IsNull(DockLabelLength.Parse("1,5ch"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    [DataRow(4.6d)]
    [DataRow(double.Epsilon)]
    [DataRow((double)float.MaxValue)]
    [DataRow(-0d)]
    public void ToolkitCharacterWidths_RoundTripThroughThePropertyBag(double characters)
    {
        var item = new ListItem().SetDockLabelReservations(DockLabelWidth.Characters(characters), null);
        var width = DockLabelLength.Parse(item.GetProperties()[WellKnownExtensionAttributes.DockTitleWidth]);

        Assert.AreEqual(new DockLabelLength(characters, InCharacters: true), width);
    }

    [TestMethod]
    [DataRow("Arbeitsspeicher")]
    [DataRow("\u010cas aktivity")]
    [DataRow("12ch")]
    [DataRow("text:CPU")]
    [DataRow("")]
    public void FromProperties_PreservesLiteralSamplesInEveryWidth(string sample)
    {
        var constraints = DockLabelWidthConstraints.FromProperties(new Dictionary<string, object?>
        {
            [WellKnownExtensionAttributes.DockMinLabelWidth] = "text:" + sample,
            [WellKnownExtensionAttributes.DockMaxLabelWidth] = "text:" + sample,
            [WellKnownExtensionAttributes.DockTitleWidth] = "text:" + sample,
            [WellKnownExtensionAttributes.DockSubtitleWidth] = "text:" + sample,
        });

        Assert.AreEqual(sample, constraints.Minimum?.Sample);
        Assert.AreEqual(sample, constraints.Maximum?.Sample);
        Assert.AreEqual(sample, constraints.TitleWidth?.Sample);
        Assert.AreEqual(sample, constraints.SubtitleWidth?.Sample);
        Assert.IsTrue(constraints.UsesFontMeasurements);
        Assert.IsFalse(constraints.UsesCharacters);
    }

    [TestMethod]
    public void FromProperties_AllInvalidWidthsUseTheDefaultSnapshot()
    {
        var constraints = DockLabelWidthConstraints.FromProperties(new Dictionary<string, object?>
        {
            [WellKnownExtensionAttributes.DockMinLabelWidth] = true,
            [WellKnownExtensionAttributes.DockMaxLabelWidth] = double.NaN,
            [WellKnownExtensionAttributes.DockTitleWidth] = "invalid",
            [WellKnownExtensionAttributes.DockSubtitleWidth] = new object(),
        });

        Assert.AreSame(DockLabelWidthConstraints.Default, constraints);
        Assert.IsFalse(constraints.UsesFontMeasurements);
    }

    [TestMethod]
    public void Resolve_EqualCharacterLimitsUseTheTitleFontAtEachTextScale()
    {
        var constraints = new DockLabelWidthConstraints(new(10, true), new(10, true));

        Assert.IsTrue(constraints.UsesCharacters);
        Assert.AreEqual((60d, 60d), constraints.Resolve(6, 5, 24, 100));
        Assert.AreEqual((120d, 120d), constraints.Resolve(12, 10, 24, 100));
        Assert.AreEqual((60d, 60d), constraints.Resolve(6, 5, 24, 100, showTitle: false));
    }

    [TestMethod]
    public void Resolve_DipLimitsDoNotScaleWithTheFont()
    {
        var constraints = new DockLabelWidthConstraints(new(80, false), new(80, false));

        Assert.AreEqual((80d, 80d), constraints.Resolve(6, 5, 24, 100));
        Assert.AreEqual((80d, 80d), constraints.Resolve(12, 10, 24, 100));
    }

    [TestMethod]
    public void Resolve_ExplicitLimitsTakePrecedenceOverConflictingDefaults()
    {
        var minimumOnly = new DockLabelWidthConstraints(new(120, false), null);
        var maximumOnly = new DockLabelWidthConstraints(null, new(10, false));

        Assert.AreEqual((120d, 120d), minimumOnly.Resolve(6, 5, 24, 100));
        Assert.AreEqual((10d, 10d), maximumOnly.Resolve(6, 5, 24, 100));
        Assert.AreEqual((0d, 10d), maximumOnly.Resolve(6, 5, 0, 100));
    }

    [TestMethod]
    public void Resolve_InvertedLimitsAreIgnoredAfterMeasurement()
    {
        var constraints = new DockLabelWidthConstraints(new(10, true), new(80, false));

        Assert.AreEqual((60d, 80d), constraints.Resolve(6, 5, 24, 100));
        Assert.AreEqual((24d, 100d), constraints.Resolve(12, 10, 24, 100));

        var withReservation = constraints with { TitleWidth = new(5, true) };

        Assert.AreEqual((60d, 60d), withReservation.Resolve(6, 5, 24, 100));
        Assert.AreEqual((60d, 60d), withReservation.Resolve(12, 10, 24, 100));
    }

    [TestMethod]
    public void Resolve_CharacterOverflowIsIgnoredIndependently()
    {
        var constraints = new DockLabelWidthConstraints(new(float.MaxValue, true), new(80, false));

        Assert.AreEqual((24d, 80d), constraints.Resolve(12, 10, 24, 100));
        Assert.AreEqual((24d, 100d), (constraints with { Maximum = new(float.MaxValue, true) }).Resolve(12, 10, 24, 100));

        var rows = constraints with { TitleWidth = new(float.MaxValue, true), SubtitleWidth = new(40, false) };

        Assert.AreEqual((40d, 40d), rows.Resolve(12, 10, 24, 100));
        Assert.AreEqual((24d, 80d), rows.Resolve(12, 10, 24, 100, showSubtitle: false));
    }

    [TestMethod]
    public void Resolve_MissingHintsPreserveTitleAndSubtitleDefaults()
    {
        Assert.AreSame(DockLabelWidthConstraints.Default, DockLabelWidthConstraints.FromProperties(null));
        Assert.AreEqual((24d, 100d), DockLabelWidthConstraints.Default.Resolve(6, 5, 24, 100));
        Assert.AreEqual((0d, 100d), DockLabelWidthConstraints.Default.Resolve(6, 5, 0, 100));
    }

    [TestMethod]
    public void Resolve_UsesTheLargerEnabledRowReservationAtEachTextScale()
    {
        var constraints = new DockLabelWidthConstraints(null, null, new(5, true), new(12, true));

        Assert.AreEqual((60d, 60d), constraints.Resolve(6, 5, 24, 100));
        Assert.AreEqual((30d, 30d), constraints.Resolve(6, 5, 24, 100, showSubtitle: false));
        Assert.AreEqual((60d, 60d), constraints.Resolve(12, 10, 24, 100, showSubtitle: false));
        Assert.AreEqual((120d, 120d), constraints.Resolve(12, 10, 24, 100));
        Assert.AreEqual((60d, 60d), constraints.Resolve(6, 5, 24, 100, showTitle: false));
        Assert.AreEqual((0d, 100d), constraints.Resolve(6, 5, 24, 100, showTitle: false, showSubtitle: false));
    }

    [TestMethod]
    [DataRow(null, null, 20d, 80d, 20d, 80d)]
    [DataRow("4ch", null, 24d, 24d, 24d, 24d)]
    [DataRow(null, "8ch", 48d, 48d, 20d, 80d)]
    [DataRow("invalid", -1d, 20d, 80d, 20d, 80d)]
    [DataRow("20ch", "5ch", 80d, 80d, 80d, 80d)]
    [DataRow("0ch", "0ch", 20d, 20d, 20d, 20d)]
    public void Resolve_ClampsOnlyApplicableRowReservations(
        object? titleWidth,
        object? subtitleWidth,
        double expectedMinimum,
        double expectedMaximum,
        double expectedTitleMinimum,
        double expectedTitleMaximum)
    {
        var constraints = DockLabelWidthConstraints.FromProperties(new Dictionary<string, object?>
        {
            [WellKnownExtensionAttributes.DockMinLabelWidth] = 20d,
            [WellKnownExtensionAttributes.DockMaxLabelWidth] = 80d,
            [WellKnownExtensionAttributes.DockTitleWidth] = titleWidth,
            [WellKnownExtensionAttributes.DockSubtitleWidth] = subtitleWidth,
        });

        Assert.AreEqual((expectedMinimum, expectedMaximum), constraints.Resolve(6, 6, 24, 100));
        Assert.AreEqual((expectedTitleMinimum, expectedTitleMaximum), constraints.Resolve(6, 6, 24, 100, showSubtitle: false));
    }

    [TestMethod]
    public void Resolve_MixedRowUnitsAreComparedAfterTextScaling()
    {
        var constraints = new DockLabelWidthConstraints(null, null, new(10, true), new(80, false));

        Assert.AreEqual((80d, 80d), constraints.Resolve(6, 5, 24, 100));
        Assert.AreEqual((120d, 120d), constraints.Resolve(12, 10, 24, 100));
        Assert.AreEqual((60d, 60d), constraints.Resolve(6, 5, 24, 100, showSubtitle: false));
        Assert.AreEqual((80d, 80d), constraints.Resolve(12, 10, 24, 100, showTitle: false));
    }

    [TestMethod]
    public void Resolve_SamplesRespectVisibilityAndAreNotCappedByDefaults()
    {
        var constraints = new DockLabelWidthConstraints(null, null, new(0, false, "100%"), new(0, false, "Arbeitsspeicher"));

        Assert.AreEqual((45d, 45d), constraints.Resolve(6, 5, 24, 100, titleSampleWidth: 30, subtitleSampleWidth: 45));
        Assert.AreEqual((30d, 30d), constraints.Resolve(6, 5, 24, 100, showSubtitle: false, titleSampleWidth: 30, subtitleSampleWidth: 45));
        Assert.AreEqual((45d, 45d), constraints.Resolve(6, 5, 24, 100, showTitle: false, titleSampleWidth: 30, subtitleSampleWidth: 45));
        Assert.AreEqual((0d, 100d), constraints.Resolve(6, 5, 24, 100, showTitle: false, showSubtitle: false, titleSampleWidth: 30, subtitleSampleWidth: 45));
        Assert.AreEqual((200d, 200d), constraints.Resolve(12, 10, 24, 100, titleSampleWidth: 60, subtitleSampleWidth: 200));
    }

    [TestMethod]
    public void Resolve_SamplesAndCharacterReservationsCanBeMixed()
    {
        var constraints = new DockLabelWidthConstraints(null, null, new(5, true), new(0, false, "CPU"));

        Assert.AreEqual((30d, 30d), constraints.Resolve(6, 5, 24, 100, subtitleSampleWidth: 18));
        Assert.AreEqual((18d, 18d), constraints.Resolve(6, 5, 24, 100, showTitle: false, subtitleSampleWidth: 18));
        Assert.AreEqual((30d, 30d), constraints.Resolve(6, 5, 24, 100, showSubtitle: false, subtitleSampleWidth: 80));
        Assert.AreEqual((80d, 80d), constraints.Resolve(6, 5, 24, 100, titleSampleWidth: 100, subtitleSampleWidth: 80));
    }

    [TestMethod]
    public void Resolve_SampleLimitsClampTheReservationWithoutResizingWithContent()
    {
        var constraints = new DockLabelWidthConstraints(new(0, false, "100%"), new(0, false, "Longest"), new(0, false, "0%"), new(12, true));

        Assert.AreEqual((50d, 50d), constraints.Resolve(6, 5, 24, 100, titleSampleWidth: 18, minimumSampleWidth: 30, maximumSampleWidth: 50));
        Assert.AreEqual((30d, 30d), constraints.Resolve(6, 5, 24, 100, showSubtitle: false, titleSampleWidth: 18, minimumSampleWidth: 30, maximumSampleWidth: 50));
        Assert.AreEqual((30d, 50d), (constraints with { TitleWidth = null, SubtitleWidth = null }).Resolve(6, 5, 24, 100, minimumSampleWidth: 30, maximumSampleWidth: 50));
        Assert.AreEqual((60d, 60d), constraints.Resolve(6, 5, 24, 100, titleSampleWidth: 18, minimumSampleWidth: 50, maximumSampleWidth: 30));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(-1d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.MaxValue)]
    public void Resolve_UnmeasurableSamplesAreIgnored(double? measuredWidth)
    {
        var constraints = new DockLabelWidthConstraints(new(20, false), new(80, false), new(0, false, "100%"), new(0, false, "CPU"));

        Assert.AreEqual((20d, 80d), constraints.Resolve(6, 5, 24, 100, titleSampleWidth: measuredWidth, subtitleSampleWidth: measuredWidth));
        Assert.AreEqual((30d, 30d), constraints.Resolve(6, 5, 24, 100, titleSampleWidth: 30, subtitleSampleWidth: measuredWidth));

        var invalidLimits = constraints with { Minimum = new(0, false, "0%"), Maximum = new(0, false, "100%") };

        Assert.AreEqual((18d, 18d), invalidLimits.Resolve(6, 5, 24, 100, titleSampleWidth: 18, minimumSampleWidth: measuredWidth, maximumSampleWidth: measuredWidth));
    }

    [TestMethod]
    public void Resolve_EmptySampleReservesZeroUnlessAMinimumApplies()
    {
        var constraints = new DockLabelWidthConstraints(null, null, new(0, false, string.Empty));

        Assert.AreEqual((0d, 0d), constraints.Resolve(6, 5, 24, 100, titleSampleWidth: 0));
        Assert.AreEqual((20d, 20d), (constraints with { Minimum = new(20, false) }).Resolve(6, 5, 24, 100, titleSampleWidth: 0));
        Assert.AreEqual((0d, 100d), constraints.Resolve(6, 5, 0, 100, showTitle: false, titleSampleWidth: 0));
    }
}
