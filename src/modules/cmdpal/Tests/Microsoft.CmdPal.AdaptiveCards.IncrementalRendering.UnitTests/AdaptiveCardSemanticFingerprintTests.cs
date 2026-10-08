// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.AdaptiveCards.IncrementalRendering.UnitTests;

[TestClass]
public sealed class AdaptiveCardSemanticFingerprintTests
{
    [TestMethod]
    public void PropertyOrderingDoesNotAffectFingerprint()
    {
        var left = """{"type":"AdaptiveCard","version":"1.5","body":[]}""";
        var right = """{"body":[],"version":"1.5","type":"AdaptiveCard"}""";

        Assert.AreEqual(
            AdaptiveCardSemanticFingerprint.Create(left),
            AdaptiveCardSemanticFingerprint.Create(right));
    }

    [TestMethod]
    public void AuthoredTextBlockTextIsPatchable()
    {
        var left = """{"type":"AdaptiveCard","body":[{"type":"TextBlock","text":"old"}]}""";
        var right = """{"type":"AdaptiveCard","body":[{"type":"TextBlock","text":"new"}]}""";

        Assert.AreEqual(
            AdaptiveCardSemanticFingerprint.Create(left),
            AdaptiveCardSemanticFingerprint.Create(right));
    }

    [TestMethod]
    public void ActionChangesRemainReplacementSensitive()
    {
        var left = """{"type":"AdaptiveCard","actions":[{"type":"Action.Submit","title":"Go","data":{"mode":1}}]}""";
        var right = """{"type":"AdaptiveCard","actions":[{"type":"Action.Submit","title":"Go","data":{"mode":2}}]}""";

        Assert.AreNotEqual(
            AdaptiveCardSemanticFingerprint.Create(left),
            AdaptiveCardSemanticFingerprint.Create(right));
    }

    [TestMethod]
    public void TextInsideActionSubtreeRemainsReplacementSensitive()
    {
        var left = """{"type":"AdaptiveCard","actions":[{"type":"Action.ShowCard","card":{"type":"AdaptiveCard","body":[{"type":"TextBlock","text":"old"}]}}]}""";
        var right = """{"type":"AdaptiveCard","actions":[{"type":"Action.ShowCard","card":{"type":"AdaptiveCard","body":[{"type":"TextBlock","text":"new"}]}}]}""";

        Assert.AreNotEqual(
            AdaptiveCardSemanticFingerprint.Create(left),
            AdaptiveCardSemanticFingerprint.Create(right));
    }

    [TestMethod]
    public void InputChangesRemainReplacementSensitive()
    {
        var left = """{"type":"AdaptiveCard","body":[{"type":"Input.Text","id":"name","value":"one"}]}""";
        var right = """{"type":"AdaptiveCard","body":[{"type":"Input.Text","id":"name","value":"two"}]}""";

        Assert.AreNotEqual(
            AdaptiveCardSemanticFingerprint.Create(left),
            AdaptiveCardSemanticFingerprint.Create(right));
    }

    [TestMethod]
    public void ImageResourceChangesRemainReplacementSensitive()
    {
        var left = """{"type":"AdaptiveCard","body":[{"type":"Image","url":"one.png"}]}""";
        var right = """{"type":"AdaptiveCard","body":[{"type":"Image","url":"two.png"}]}""";

        Assert.AreNotEqual(
            AdaptiveCardSemanticFingerprint.Create(left),
            AdaptiveCardSemanticFingerprint.Create(right));
    }

    [TestMethod]
    public void InlineSvgContentIsPatchable()
    {
        var left = """{"type":"AdaptiveCard","body":[{"type":"Image","url":"data:image/svg+xml;utf8,<svg><path d='M 0 0'/></svg>"}]}""";
        var right = """{"type":"AdaptiveCard","body":[{"type":"Image","url":"data:image/svg+xml;utf8,<svg><path d='M 1 1'/></svg>"}]}""";

        Assert.AreEqual(
            AdaptiveCardSemanticFingerprint.Create(left),
            AdaptiveCardSemanticFingerprint.Create(right));
    }

    [TestMethod]
    public void InlineSvgLayoutChangeRemainsReplacementSensitive()
    {
        var left = """{"type":"AdaptiveCard","body":[{"type":"Image","url":"data:image/svg+xml;utf8,<svg/>","width":"100px"}]}""";
        var right = """{"type":"AdaptiveCard","body":[{"type":"Image","url":"data:image/svg+xml;utf8,<svg/>","width":"200px"}]}""";

        Assert.AreNotEqual(
            AdaptiveCardSemanticFingerprint.Create(left),
            AdaptiveCardSemanticFingerprint.Create(right));
    }

    [TestMethod]
    public void InlineSvgContentRemainsReplacementSensitiveWhenMappingIsIncomplete()
    {
        var left = """{"type":"AdaptiveCard","body":[{"type":"Image","url":"data:image/svg+xml;utf8,<svg id='old'/>"}]}""";
        var right = """{"type":"AdaptiveCard","body":[{"type":"Image","url":"data:image/svg+xml;utf8,<svg id='new'/>"}]}""";

        Assert.AreNotEqual(
            AdaptiveCardSemanticFingerprint.Create(left, mappedTextBlockCount: 0, mappedInlineSvgImageCount: 0),
            AdaptiveCardSemanticFingerprint.Create(right, mappedTextBlockCount: 0, mappedInlineSvgImageCount: 0));
    }

    [TestMethod]
    public void TextRemainsReplacementSensitiveWhenMappingIsIncomplete()
    {
        var left = """{"type":"AdaptiveCard","body":[{"type":"TextBlock","text":"old"}]}""";
        var right = """{"type":"AdaptiveCard","body":[{"type":"TextBlock","text":"new"}]}""";

        Assert.AreNotEqual(
            AdaptiveCardSemanticFingerprint.Create(left, mappedTextBlockCount: 0, mappedInlineSvgImageCount: 0),
            AdaptiveCardSemanticFingerprint.Create(right, mappedTextBlockCount: 0, mappedInlineSvgImageCount: 0));
    }

    [TestMethod]
    public void ActionTextCannotCompensateForUnmappedBodyText()
    {
        var left = """
            {
              "type":"AdaptiveCard",
              "body":[{"type":"TextBlock","text":"[label](https://old.example)"}],
              "actions":[{
                "type":"Action.ShowCard",
                "title":"Details",
                "card":{"type":"AdaptiveCard","body":[{"type":"TextBlock","text":"mapped action text"}]}
              }]
            }
            """;
        var right = left.Replace("https://old.example", "https://new.example", StringComparison.Ordinal);

        Assert.AreNotEqual(
            AdaptiveCardSemanticFingerprint.Create(left, mappedTextBlockCount: 1, mappedInlineSvgImageCount: 0),
            AdaptiveCardSemanticFingerprint.Create(right, mappedTextBlockCount: 1, mappedInlineSvgImageCount: 0));
    }

    [TestMethod]
    public void RegisteredCustomElementDataIsPatchable()
    {
        var left = """{"type":"AdaptiveCard","body":[{"type":"Chart.Line","yMax":100,"data":[{"values":[{"y":1}]}]}]}""";
        var right = """{"type":"AdaptiveCard","body":[{"type":"Chart.Line","yMax":50,"data":[{"values":[{"y":2}]}]}]}""";

        Assert.AreEqual(
            CreateWithChart(left, mappedCustomElementCount: 1),
            CreateWithChart(right, mappedCustomElementCount: 1));
    }

    [TestMethod]
    public void UnregisteredCustomElementDataRemainsReplacementSensitive()
    {
        var left = """{"type":"AdaptiveCard","body":[{"type":"Chart.Line","data":[{"values":[{"y":1}]}]}]}""";
        var right = """{"type":"AdaptiveCard","body":[{"type":"Chart.Line","data":[{"values":[{"y":2}]}]}]}""";

        Assert.AreNotEqual(
            AdaptiveCardSemanticFingerprint.Create(left, 0, 0, 1, new IncrementalPatchableElements().Add("Chart.Gauge")),
            AdaptiveCardSemanticFingerprint.Create(right, 0, 0, 1, new IncrementalPatchableElements().Add("Chart.Gauge")));
    }

    [TestMethod]
    public void CustomElementHostOwnedPropertiesRemainReplacementSensitive()
    {
        var left = """{"type":"AdaptiveCard","body":[{"type":"Chart.Line","spacing":"small","data":[]}]}""";
        var right = """{"type":"AdaptiveCard","body":[{"type":"Chart.Line","spacing":"large","data":[]}]}""";

        Assert.AreNotEqual(
            CreateWithChart(left, mappedCustomElementCount: 1),
            CreateWithChart(right, mappedCustomElementCount: 1));
    }

    [TestMethod]
    public void CustomElementDataRemainsReplacementSensitiveWhenMappingIsIncomplete()
    {
        var left = """{"type":"AdaptiveCard","body":[{"type":"Chart.Line","data":[{"values":[{"y":1}]}]}]}""";
        var right = """{"type":"AdaptiveCard","body":[{"type":"Chart.Line","data":[{"values":[{"y":2}]}]}]}""";

        Assert.AreNotEqual(
            CreateWithChart(left, mappedCustomElementCount: 0),
            CreateWithChart(right, mappedCustomElementCount: 0));
    }

    [TestMethod]
    public void CustomElementInsideActionSubtreeRemainsReplacementSensitive()
    {
        var left = """{"type":"AdaptiveCard","actions":[{"type":"Action.ShowCard","card":{"type":"AdaptiveCard","body":[{"type":"Chart.Line","data":[{"values":[{"y":1}]}]}]}}]}""";
        var right = """{"type":"AdaptiveCard","actions":[{"type":"Action.ShowCard","card":{"type":"AdaptiveCard","body":[{"type":"Chart.Line","data":[{"values":[{"y":2}]}]}]}}]}""";

        Assert.AreNotEqual(
            CreateWithChart(left, mappedCustomElementCount: 1),
            CreateWithChart(right, mappedCustomElementCount: 1));
    }

    [TestMethod]
    public void HostOwnedPropertiesAreNotPatchable()
    {
        Assert.IsFalse(IncrementalPatchableElements.IsPatchableProperty("spacing"));
        Assert.IsFalse(IncrementalPatchableElements.IsPatchableProperty("isVisible"));
        Assert.IsFalse(IncrementalPatchableElements.IsPatchableProperty("selectAction"));
        Assert.IsTrue(IncrementalPatchableElements.IsPatchableProperty("data"));
        Assert.IsTrue(IncrementalPatchableElements.IsPatchableProperty("title"));
    }

    [TestMethod]
    public void UnusedFallbackDoesNotStopTextPatching()
    {
        // The chart always renders itself, so only the first TextBlock is drawn and mapped.
        var left = """{"type":"AdaptiveCard","body":[{"type":"TextBlock","text":"old"},{"type":"Chart.Line","data":[],"fallback":{"type":"TextBlock","text":"old value"}}]}""";
        var right = """{"type":"AdaptiveCard","body":[{"type":"TextBlock","text":"new"},{"type":"Chart.Line","data":[],"fallback":{"type":"TextBlock","text":"new value"}}]}""";

        Assert.AreEqual(
            CreateWithChart(left, mappedTextBlockCount: 1, mappedCustomElementCount: 1),
            CreateWithChart(right, mappedTextBlockCount: 1, mappedCustomElementCount: 1));
    }

    [TestMethod]
    public void FallbackOfElementWithRequirementsIsStillCounted()
    {
        // A host that doesn't meet requires draws the fallback, here as unmapped markdown.
        var left = """{"type":"AdaptiveCard","body":[{"type":"Chart.Line","requires":{"charts":"2.0"},"data":[],"fallback":{"type":"TextBlock","text":"[old](https://example.com)"}}]}""";
        var right = left.Replace("[old]", "[new]", StringComparison.Ordinal);

        Assert.AreNotEqual(
            CreateWithChart(left, mappedTextBlockCount: 0, mappedCustomElementCount: 0),
            CreateWithChart(right, mappedTextBlockCount: 0, mappedCustomElementCount: 0));
    }

    [TestMethod]
    public void ElementsInsideUnusedFallbackAreNotCounted()
    {
        var left = """{"type":"AdaptiveCard","body":[{"type":"Chart.Line","data":[{"values":[{"y":1}]}],"fallback":{"type":"Chart.Line","data":[]}}]}""";
        var right = """{"type":"AdaptiveCard","body":[{"type":"Chart.Line","data":[{"values":[{"y":2}]}],"fallback":{"type":"Chart.Line","data":[]}}]}""";

        Assert.AreEqual(
            CreateWithChart(left, mappedTextBlockCount: 0, mappedCustomElementCount: 1),
            CreateWithChart(right, mappedTextBlockCount: 0, mappedCustomElementCount: 1));
    }

    private static string CreateWithChart(string cardJson, int mappedCustomElementCount) =>
        CreateWithChart(cardJson, mappedTextBlockCount: 0, mappedCustomElementCount);

    private static string CreateWithChart(string cardJson, int mappedTextBlockCount, int mappedCustomElementCount) =>
        AdaptiveCardSemanticFingerprint.Create(
            cardJson,
            mappedTextBlockCount,
            mappedInlineSvgImageCount: 0,
            mappedCustomElementCount,
            new IncrementalPatchableElements().Add("Chart.Line"));
}
