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
}
