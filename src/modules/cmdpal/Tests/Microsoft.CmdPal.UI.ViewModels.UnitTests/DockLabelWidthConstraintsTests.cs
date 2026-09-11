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
    [DataRow("1sqh", 0.01d, true)]
    [DataRow("100sqh", 1d, true)]
    [DataRow("1200sqh", 12d, true)]
    [DataRow("2.5sqh", 0.025d, true)]
    [DataRow("0sqh", 0d, true)]
    public void Parse_AcceptsDipsAndFontRelativeWidths(object value, double amount, bool inCharacters)
    {
        Assert.AreEqual(new DockLabelLength(amount, inCharacters), DockLabelLength.Parse(value));
    }

    [TestMethod]
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
    [DataRow("sqh")]
    [DataRow("10SQH")]
    [DataRow("10 sqh")]
    [DataRow("-1sqh")]
    [DataRow("NaNsqh")]
    [DataRow("Infinitysqh")]
    public void Parse_IgnoresUnsupportedOrInvalidValues(object value)
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
            Assert.AreEqual(new DockLabelLength(0.015, InCharacters: true), DockLabelLength.Parse("1.5sqh"));
            Assert.IsNull(DockLabelLength.Parse("1,5sqh"));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [TestMethod]
    [DataRow("10ch", "10ch")]
    [DataRow("1000sqh", "1000sqh")]
    [DataRow("1000sqh", "10ch")]
    public void Resolve_EqualFontRelativeBoundsReserveWidthAtEachTextScale(string minimum, string maximum)
    {
        var constraints = new DockLabelWidthConstraints(DockLabelLength.Parse(minimum), DockLabelLength.Parse(maximum));

        Assert.IsTrue(constraints.UsesCharacters);
        Assert.AreEqual((60d, 60d), constraints.Resolve(6, 5, 24, 100));
        Assert.AreEqual((120d, 120d), constraints.Resolve(12, 10, 24, 100));
        Assert.AreEqual((60d, 60d), constraints.Resolve(6, 5, 24, 100, showTitle: false));
    }

    [TestMethod]
    public void Resolve_DipBoundsDoNotScaleWithTheFont()
    {
        var constraints = new DockLabelWidthConstraints(new(80, false), new(80, false));

        Assert.AreEqual((80d, 80d), constraints.Resolve(6, 6, 24, 100));
        Assert.AreEqual((80d, 80d), constraints.Resolve(12, 12, 24, 100));
    }

    [TestMethod]
    [DataRow("10ch")]
    [DataRow("1000sqh")]
    public void Resolve_MixedUnitsRejectContradictoryBoundsAfterScaling(string minimum)
    {
        var constraints = new DockLabelWidthConstraints(DockLabelLength.Parse(minimum), new(80, false));

        Assert.AreEqual((60d, 80d), constraints.Resolve(6, 6, 24, 100));
        Assert.AreEqual((24d, 100d), constraints.Resolve(12, 12, 24, 100));
    }

    [TestMethod]
    public void Resolve_ExplicitBoundsTakePrecedenceOverDefaults()
    {
        var minimumOnly = new DockLabelWidthConstraints(new(120, false), null);
        var maximumOnly = new DockLabelWidthConstraints(null, new(10, false));

        Assert.AreEqual((120d, 120d), minimumOnly.Resolve(6, 6, 24, 100));
        Assert.AreEqual((10d, 10d), maximumOnly.Resolve(6, 6, 24, 100));
        Assert.AreEqual((0d, 10d), maximumOnly.Resolve(6, 6, 0, 100));
    }

    [TestMethod]
    public void Resolve_CharacterWidthOverflowFallsBackToDefaults()
    {
        var constraints = new DockLabelWidthConstraints(new(float.MaxValue, true), new(float.MaxValue, true));

        Assert.AreEqual((24d, 100d), constraints.Resolve(12, 12, 24, 100));
    }

    [TestMethod]
    public void Resolve_MissingHintsPreserveTitleAndSubtitleDefaults()
    {
        Assert.AreSame(DockLabelWidthConstraints.Default, DockLabelWidthConstraints.FromProperties(null));
        Assert.AreEqual((24d, 100d), DockLabelWidthConstraints.Default.Resolve(6, 6, 24, 100));
        Assert.AreEqual((0d, 100d), DockLabelWidthConstraints.Default.Resolve(6, 6, 0, 100));
    }

    [TestMethod]
    public void Resolve_UsesTheLargerEnabledRowReservationAtEachTextScale()
    {
        var properties = new Dictionary<string, object?>
        {
            [WellKnownExtensionAttributes.DockMinLabelWidth] = 80d,
            [WellKnownExtensionAttributes.DockMaxLabelWidth] = 80d,
            [WellKnownExtensionAttributes.DockTitleWidth] = "5ch",
            [WellKnownExtensionAttributes.DockSubtitleWidth] = "12ch",
        };
        var constraints = DockLabelWidthConstraints.FromProperties(properties);

        Assert.AreEqual((60d, 60d), constraints.Resolve(6, 5, 24, 100));
        Assert.AreEqual((30d, 30d), constraints.Resolve(6, 5, 24, 100, showSubtitle: false));
        Assert.AreEqual((60d, 60d), constraints.Resolve(12, 10, 24, 100, showSubtitle: false));
        Assert.AreEqual((120d, 120d), constraints.Resolve(12, 10, 24, 100));
        Assert.AreEqual((60d, 60d), constraints.Resolve(6, 5, 24, 100));
        Assert.AreEqual((60d, 60d), constraints.Resolve(6, 5, 24, 100, showTitle: false));
        Assert.AreEqual((0d, 100d), constraints.Resolve(6, 5, 24, 100, showTitle: false, showSubtitle: false));
    }

    [TestMethod]
    [DataRow("10ch", "12ch", 6d, 4d, 60d)]
    [DataRow("10ch", "12ch", 4d, 6d, 72d)]
    [DataRow("10ch", "12ch", 12d, 4d, 120d)]
    [DataRow("1000sqh", "1200sqh", 6d, 4d, 60d)]
    public void Resolve_ComparesRowWidthsAfterApplyingEachFont(
        string titleWidth,
        string subtitleWidth,
        double titleCharacterWidth,
        double subtitleCharacterWidth,
        double expectedWidth)
    {
        var constraints = new DockLabelWidthConstraints(null, null, DockLabelLength.Parse(titleWidth), DockLabelLength.Parse(subtitleWidth));

        Assert.AreEqual((expectedWidth, expectedWidth), constraints.Resolve(titleCharacterWidth, subtitleCharacterWidth, 24, 100));
    }

    [TestMethod]
    [DataRow(null, null, 36d, 72d, 36d, 72d)]
    [DataRow("4ch", null, 24d, 24d, 24d, 24d)]
    [DataRow(null, "8ch", 48d, 48d, 36d, 72d)]
    [DataRow("invalid", -1d, 36d, 72d, 36d, 72d)]
    [DataRow("10ch", "5ch", 60d, 60d, 60d, 60d)]
    [DataRow("0ch", "0ch", 0d, 0d, 0d, 0d)]
    public void Resolve_MissingOrInvalidRowHintsUseOnlyApplicableReservations(
        object? titleWidth,
        object? subtitleWidth,
        double expectedMinimum,
        double expectedMaximum,
        double expectedTitleMinimum,
        double expectedTitleMaximum)
    {
        var properties = new Dictionary<string, object?>
        {
            [WellKnownExtensionAttributes.DockMinLabelWidth] = "6ch",
            [WellKnownExtensionAttributes.DockMaxLabelWidth] = "12ch",
            [WellKnownExtensionAttributes.DockTitleWidth] = titleWidth,
            [WellKnownExtensionAttributes.DockSubtitleWidth] = subtitleWidth,
        };
        var constraints = DockLabelWidthConstraints.FromProperties(properties);

        Assert.AreEqual((expectedMinimum, expectedMaximum), constraints.Resolve(6, 6, 24, 100));
        Assert.AreEqual((expectedTitleMinimum, expectedTitleMaximum), constraints.Resolve(6, 6, 24, 100, showSubtitle: false));
    }

    [TestMethod]
    public void Resolve_MixedRowUnitsAreComparedAfterTextScaling()
    {
        var properties = new Dictionary<string, object?>
        {
            [WellKnownExtensionAttributes.DockTitleWidth] = "1000sqh",
            [WellKnownExtensionAttributes.DockSubtitleWidth] = 80d,
        };
        var constraints = DockLabelWidthConstraints.FromProperties(properties);

        Assert.IsTrue(constraints.UsesCharacters);
        Assert.AreEqual((80d, 80d), constraints.Resolve(6, 6, 24, 100));
        Assert.AreEqual((120d, 120d), constraints.Resolve(12, 12, 24, 100));
        Assert.AreEqual((60d, 60d), constraints.Resolve(6, 6, 24, 100, showSubtitle: false));
        Assert.AreEqual((80d, 80d), constraints.Resolve(12, 12, 24, 100, showTitle: false));
    }

    [TestMethod]
    public void Resolve_RowCharacterWidthOverflowFallsBackToSharedBounds()
    {
        var constraints = new DockLabelWidthConstraints(new(80, false), new(80, false), new(float.MaxValue, true), new(float.MaxValue, true));

        Assert.AreEqual((80d, 80d), constraints.Resolve(12, 12, 24, 100));
        Assert.AreEqual((80d, 80d), constraints.Resolve(12, 12, 24, 100, showSubtitle: false));
    }

    [TestMethod]
    [DataRow("Arbeitsspeicher")]
    [DataRow("\u010cas aktivity")]
    [DataRow("12ch")]
    [DataRow("")]
    public void FromProperties_PreservesLiteralSamplesWithoutParsingUnits(string sample)
    {
        var constraints = DockLabelWidthConstraints.FromProperties(new Dictionary<string, object?>
        {
            [WellKnownExtensionAttributes.DockTitleWidthSample] = sample,
            [WellKnownExtensionAttributes.DockSubtitleWidthSample] = sample,
        });

        Assert.AreEqual(sample, constraints.TitleWidthSample);
        Assert.AreEqual(sample, constraints.SubtitleWidthSample);
        Assert.IsTrue(constraints.UsesFontMeasurements);
        Assert.IsFalse(constraints.UsesCharacters);
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(80d)]
    [DataRow(true)]
    public void FromProperties_IgnoresNonStringSamples(object? sample)
    {
        var constraints = DockLabelWidthConstraints.FromProperties(new Dictionary<string, object?>
        {
            [WellKnownExtensionAttributes.DockTitleWidthSample] = sample,
            [WellKnownExtensionAttributes.DockSubtitleWidthSample] = sample,
        });

        Assert.AreSame(DockLabelWidthConstraints.Default, constraints);
        Assert.IsFalse(constraints.UsesFontMeasurements);
    }

    [TestMethod]
    public void Resolve_SamplesOverrideRowWidthsAndRespectRowVisibility()
    {
        var constraints = new DockLabelWidthConstraints(new(80, false), new(80, false), new(10, true), new(12, true), "100%", "Arbeitsspeicher");

        Assert.AreEqual((45d, 45d), constraints.Resolve(6, 5, 24, 100, titleSampleWidth: 30, subtitleSampleWidth: 45));
        Assert.AreEqual((30d, 30d), constraints.Resolve(6, 5, 24, 100, showSubtitle: false, titleSampleWidth: 30, subtitleSampleWidth: 45));
        Assert.AreEqual((45d, 45d), constraints.Resolve(6, 5, 24, 100, showTitle: false, titleSampleWidth: 30, subtitleSampleWidth: 45));
        Assert.AreEqual((0d, 100d), constraints.Resolve(6, 5, 24, 100, showTitle: false, showSubtitle: false, titleSampleWidth: 30, subtitleSampleWidth: 45));
        Assert.AreEqual((90d, 90d), constraints.Resolve(12, 10, 24, 100, titleSampleWidth: 60, subtitleSampleWidth: 90));
    }

    [TestMethod]
    public void Resolve_MixesSampleAndNumericRowsWithoutTreatingSamplesAsAMinimum()
    {
        var constraints = new DockLabelWidthConstraints(null, null, new(5, true), new(12, true), SubtitleWidthSample: "CPU");

        Assert.AreEqual((30d, 30d), constraints.Resolve(6, 5, 24, 100, subtitleSampleWidth: 18));
        Assert.AreEqual((18d, 18d), constraints.Resolve(6, 5, 24, 100, showTitle: false, subtitleSampleWidth: 18));
        Assert.AreEqual((30d, 30d), constraints.Resolve(6, 5, 24, 100, showSubtitle: false, subtitleSampleWidth: 80));
        Assert.AreEqual((80d, 80d), constraints.Resolve(6, 5, 24, 100, subtitleSampleWidth: 80));
        Assert.AreEqual((80d, 80d), constraints.Resolve(6, 5, 24, 100, titleSampleWidth: 100, subtitleSampleWidth: 80));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow(-1d)]
    [DataRow(double.NaN)]
    [DataRow(double.PositiveInfinity)]
    [DataRow(double.MaxValue)]
    public void Resolve_InvalidSampleMeasurementsFallBackToWidthHints(double? measuredWidth)
    {
        var constraints = new DockLabelWidthConstraints(new(80, false), new(80, false), new(5, true), TitleWidthSample: "100%", SubtitleWidthSample: "CPU");

        Assert.AreEqual((30d, 30d), constraints.Resolve(6, 5, 24, 100, titleSampleWidth: measuredWidth, subtitleSampleWidth: measuredWidth));
        Assert.AreEqual((80d, 80d), constraints.Resolve(6, 5, 24, 100, showTitle: false, subtitleSampleWidth: measuredWidth));
    }

    [TestMethod]
    public void Resolve_EmptySampleCanReserveZeroWithoutNumericWidths()
    {
        var constraints = new DockLabelWidthConstraints(null, null, TitleWidthSample: string.Empty);

        Assert.AreEqual((0d, 0d), constraints.Resolve(6, 5, 24, 100, titleSampleWidth: 0));
        Assert.AreEqual((0d, 100d), constraints.Resolve(6, 5, 0, 100, showTitle: false, titleSampleWidth: 0));
    }
}
