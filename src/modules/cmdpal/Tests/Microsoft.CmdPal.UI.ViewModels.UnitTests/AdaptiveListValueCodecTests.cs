// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CmdPal.UI.ViewModels.AdaptiveCards;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

/// <summary>
/// Covers the host's half of the wire contract for Command Palette's custom list inputs. The
/// literals here must match what Microsoft.CommandPalette.Extensions.Toolkit emits and accepts —
/// see ListSettingTests in the toolkit's own test project.
/// </summary>
[TestClass]
public class AdaptiveListValueCodecTests
{
    private static readonly string[] _items = ["alpha", "beta"];
    private static readonly string[] _pairKeys = ["alpha", "beta"];
    private static readonly string[] _pairValues = ["one", "two=three"];

    [TestMethod]
    public void Items_RoundTripThroughTheWireShape()
    {
        Assert.IsTrue(AdaptiveListValueCodec.TryParseItems("""[{"value":"alpha"},{"value":"beta"}]""", out var parsed));

        CollectionAssert.AreEqual(_items, parsed.Select(static item => item.Value).ToArray());
        Assert.AreEqual(
            """[{"value":"alpha"},{"value":"beta"}]""",
            AdaptiveListValueCodec.ToItemsValue(parsed));
    }

    [TestMethod]
    public void Items_AcceptBareStrings()
    {
        Assert.IsTrue(AdaptiveListValueCodec.TryParseItems("""["alpha","beta"]""", out var parsed));

        CollectionAssert.AreEqual(_items, parsed.Select(static item => item.Value).ToArray());
    }

    [TestMethod]
    public void Items_EmptyValueIsAnEmptyList()
    {
        Assert.IsTrue(AdaptiveListValueCodec.TryParseItems(string.Empty, out var parsed));

        Assert.AreEqual(0, parsed.Count);
        Assert.AreEqual("[]", AdaptiveListValueCodec.ToItemsValue(parsed));
    }

    [TestMethod]
    public void Items_PreserveUnrecognizedPerItemProperties()
    {
        Assert.IsTrue(AdaptiveListValueCodec.TryParseItems(
            """[{"value":"alpha","enabled":false},{"value":"beta"}]""",
            out var parsed));

        Assert.AreEqual(
            """[{"value":"alpha","enabled":false},{"value":"beta"}]""",
            AdaptiveListValueCodec.ToItemsValue(parsed));
    }

    [TestMethod]
    public void Items_PreserveUnrecognizedPropertiesWhenTheEntryIsEdited()
    {
        Assert.IsTrue(AdaptiveListValueCodec.TryParseItems("""[{"value":"alpha","enabled":false}]""", out var parsed));

        // A new entry alongside one the user did not touch.
        var edited = parsed.Concat([new AdaptiveListItemValue("gamma")]).ToList();

        Assert.AreEqual(
            """[{"value":"alpha","enabled":false},{"value":"gamma"}]""",
            AdaptiveListValueCodec.ToItemsValue(edited));
    }

    [TestMethod]
    public void Items_MalformedValueIsRejected()
    {
        Assert.IsFalse(AdaptiveListValueCodec.TryParseItems("not an array", out var parsed));
        Assert.AreEqual(0, parsed.Count);

        Assert.IsFalse(AdaptiveListValueCodec.TryParseItems("""{"value":"alpha"}""", out _));
    }

    [TestMethod]
    public void Pairs_RoundTripThroughTheWireShape()
    {
        Assert.IsTrue(AdaptiveListValueCodec.TryParsePairs(
            """[{"key":"alpha","value":"one"},{"key":"beta","value":"two=three"}]""",
            out var parsed));

        CollectionAssert.AreEqual(_pairKeys, parsed.Select(static pair => pair.Key).ToArray());
        CollectionAssert.AreEqual(_pairValues, parsed.Select(static pair => pair.Value).ToArray());
        Assert.AreEqual(
            """[{"key":"alpha","value":"one"},{"key":"beta","value":"two=three"}]""",
            AdaptiveListValueCodec.ToPairsValue(parsed));
    }

    [TestMethod]
    public void Pairs_PreserveUnrecognizedPerItemProperties()
    {
        Assert.IsTrue(AdaptiveListValueCodec.TryParsePairs(
            """[{"key":"alpha","value":"one","enabled":false}]""",
            out var parsed));

        Assert.AreEqual(
            """[{"key":"alpha","value":"one","enabled":false}]""",
            AdaptiveListValueCodec.ToPairsValue(parsed));
    }

    [TestMethod]
    public void Pairs_PreserveDuplicateKeysAndEmptyValues()
    {
        Assert.IsTrue(AdaptiveListValueCodec.TryParsePairs(
            """[{"key":"same","value":"one"},{"key":"same","value":""}]""",
            out var parsed));

        Assert.AreEqual(2, parsed.Count);
        Assert.AreEqual(
            """[{"key":"same","value":"one"},{"key":"same","value":""}]""",
            AdaptiveListValueCodec.ToPairsValue(parsed));
    }

    [TestMethod]
    public void Pairs_MalformedValueIsRejected()
    {
        Assert.IsFalse(AdaptiveListValueCodec.TryParsePairs("{not an array}", out var parsed));
        Assert.AreEqual(0, parsed.Count);
    }

    [TestMethod]
    public void Pairs_NewEntriesSerializeWithoutASourceObject()
    {
        var pairs = new List<AdaptiveKeyValuePairValue> { new("alpha", "one") };

        Assert.AreEqual("""[{"key":"alpha","value":"one"}]""", AdaptiveListValueCodec.ToPairsValue(pairs));
    }
}
