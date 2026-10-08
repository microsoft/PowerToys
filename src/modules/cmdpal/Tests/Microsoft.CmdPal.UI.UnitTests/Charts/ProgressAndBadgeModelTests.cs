// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.Controls.AdaptiveCards.Charts;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.UnitTests.Charts;

[TestClass]
public class ProgressAndBadgeModelTests
{
    [TestMethod]
    public void ProgressBarParsesValueMaxAndColor()
    {
        var warnings = new List<string>();
        var model = ProgressBarModel.Parse("""{ "type": "ProgressBar", "value": 30, "max": 60, "color": "warning" }""", warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.IsFalse(model.IsIndeterminate);
        Assert.AreEqual(30d, model.Value);
        Assert.AreEqual(60d, model.Max);
        Assert.AreEqual("warning", model.Color);
    }

    [TestMethod]
    public void ProgressBarWithoutValueIsIndeterminate()
    {
        var warnings = new List<string>();
        var model = ProgressBarModel.Parse("""{ "type": "ProgressBar" }""", warnings);

        Assert.IsTrue(model.IsIndeterminate);
        Assert.AreEqual(100d, model.Max);
    }

    [TestMethod]
    public void ProgressBarValueIsClamped()
    {
        Assert.AreEqual(100d, new ProgressBarModel { Value = 140 }.ClampedValue);
        Assert.AreEqual(0d, new ProgressBarModel { Value = -1 }.ClampedValue);
    }

    [TestMethod]
    public void ProgressBarMaxMustBePositive()
    {
        var warnings = new List<string>();
        var model = ProgressBarModel.Parse("""{ "type": "ProgressBar", "value": 1, "max": 0 }""", warnings);

        Assert.AreEqual(1, warnings.Count);
        Assert.AreEqual(100d, model.Max);
    }

    [TestMethod]
    public void BadgeParsesEveryProperty()
    {
        var warnings = new List<string>();
        var model = BadgeModel.Parse(
            """{ "type": "Badge", "text": "Charging", "style": "good", "appearance": "tint", "shape": "circular", "size": "extraLarge", "tooltip": "Plugged in" }""",
            warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.AreEqual("Charging", model.Text);
        Assert.AreEqual("good", model.Style);
        Assert.AreEqual(BadgeAppearance.Tint, model.Appearance);
        Assert.AreEqual(BadgeShape.Circular, model.Shape);
        Assert.AreEqual(BadgeSize.ExtraLarge, model.Size);
        Assert.AreEqual("Plugged in", model.Tooltip);
    }

    [TestMethod]
    public void BadgeWarnsAboutUnknownValues()
    {
        var warnings = new List<string>();
        var model = BadgeModel.Parse("""{ "type": "Badge", "appearance": "glow", "shape": "hexagon", "size": "tiny" }""", warnings);

        Assert.AreEqual(3, warnings.Count);
        Assert.AreEqual(string.Empty, model.Text);
        Assert.AreEqual(BadgeAppearance.Filled, model.Appearance);
        Assert.AreEqual(BadgeShape.Rounded, model.Shape);
        Assert.AreEqual(BadgeSize.Medium, model.Size);
    }

    [TestMethod]
    public void BadgeParsesItsIcon()
    {
        var warnings = new List<string>();
        var model = BadgeModel.Parse("""{ "type": "Badge", "text": "Done", "icon": "CheckmarkCircle,filled", "iconPosition": "After" }""", warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.AreEqual("\uEC61", model.IconGlyph);
        Assert.AreEqual(BadgeIconPosition.After, model.IconPosition);
    }

    [TestMethod]
    public void BadgeIconIsBeforeTheTextAndRegularByDefault()
    {
        var warnings = new List<string>();
        var model = BadgeModel.Parse("""{ "type": "Badge", "text": "Wi-Fi", "icon": "Wifi1" }""", warnings);

        Assert.AreEqual(0, warnings.Count, string.Join(", ", warnings));
        Assert.AreEqual("\uE701", model.IconGlyph);
        Assert.AreEqual(BadgeIconPosition.Before, model.IconPosition);
    }

    [TestMethod]
    public void BadgeWithAnUnknownIconShowsOnlyText()
    {
        var warnings = new List<string>();
        var model = BadgeModel.Parse("""{ "type": "Badge", "text": "New", "icon": "NotAnIcon" }""", warnings);

        Assert.AreEqual(1, warnings.Count);
        Assert.IsNull(model.IconGlyph);
        Assert.AreEqual("New", model.Text);
    }
}
