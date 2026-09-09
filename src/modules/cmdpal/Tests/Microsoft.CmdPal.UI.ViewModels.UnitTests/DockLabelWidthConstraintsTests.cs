// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using Microsoft.CmdPal.UI.ViewModels.Dock;
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
        Assert.AreEqual((60d, 60d), constraints.Resolve(6, 24, 100));
        Assert.AreEqual((120d, 120d), constraints.Resolve(12, 24, 100));
    }

    [TestMethod]
    public void Resolve_DipBoundsDoNotScaleWithTheFont()
    {
        var constraints = new DockLabelWidthConstraints(new(80, false), new(80, false));

        Assert.AreEqual((80d, 80d), constraints.Resolve(6, 24, 100));
        Assert.AreEqual((80d, 80d), constraints.Resolve(12, 24, 100));
    }

    [TestMethod]
    [DataRow("10ch")]
    [DataRow("1000sqh")]
    public void Resolve_MixedUnitsRejectContradictoryBoundsAfterScaling(string minimum)
    {
        var constraints = new DockLabelWidthConstraints(DockLabelLength.Parse(minimum), new(80, false));

        Assert.AreEqual((60d, 80d), constraints.Resolve(6, 24, 100));
        Assert.AreEqual((24d, 100d), constraints.Resolve(12, 24, 100));
    }

    [TestMethod]
    public void Resolve_ExplicitBoundsTakePrecedenceOverDefaults()
    {
        var minimumOnly = new DockLabelWidthConstraints(new(120, false), null);
        var maximumOnly = new DockLabelWidthConstraints(null, new(10, false));

        Assert.AreEqual((120d, 120d), minimumOnly.Resolve(6, 24, 100));
        Assert.AreEqual((10d, 10d), maximumOnly.Resolve(6, 24, 100));
        Assert.AreEqual((0d, 10d), maximumOnly.Resolve(6, 0, 100));
    }

    [TestMethod]
    public void Resolve_CharacterWidthOverflowFallsBackToDefaults()
    {
        var constraints = new DockLabelWidthConstraints(new(float.MaxValue, true), new(float.MaxValue, true));

        Assert.AreEqual((24d, 100d), constraints.Resolve(12, 24, 100));
    }

    [TestMethod]
    public void Resolve_MissingHintsPreserveTitleAndSubtitleDefaults()
    {
        Assert.AreSame(DockLabelWidthConstraints.Default, DockLabelWidthConstraints.FromProperties(null));
        Assert.AreEqual((24d, 100d), DockLabelWidthConstraints.Default.Resolve(6, 24, 100));
        Assert.AreEqual((0d, 100d), DockLabelWidthConstraints.Default.Resolve(6, 0, 100));
    }
}
