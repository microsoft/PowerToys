// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class ExtensionOrderTests
{
    private static readonly string[] OrderAB = ["a", "b"];
    private static readonly string[] OrderABC = ["a", "b", "c"];
    private static readonly string[] ExpectedXYZ = ["x", "y", "z"];
    private static readonly string[] ExpectedABC = ["a", "b", "c"];
    private static readonly string[] ExpectedABXY = ["a", "b", "x", "y"];
    private static readonly string[] OrderExternalThenBuiltIns = ["external.foo", "builtin.apps", "builtin.calc"];
    private static readonly string[] OrderExternalThenApps = ["external.foo", "builtin.apps"];
    private static readonly string[] ExpectedExternalFirst = ["external.foo", "builtin.apps", "builtin.calc"];
    private static readonly string[] ExpectedNewProviderLast = ["external.foo", "builtin.apps", "newly.installed"];

    [TestMethod]
    public void ExtensionOrder_DefaultIsEmpty()
    {
        var settings = DeserializeSettings("{}");
        Assert.IsNotNull(settings.ExtensionOrder);
        Assert.AreEqual(0, settings.ExtensionOrder.Length);
    }

    [TestMethod]
    public void ExtensionOrder_RoundTrips()
    {
        var order = new[] { "provider.b", "provider.a", "provider.c" };
        var settings = DeserializeSettings("{}") with { ExtensionOrder = order };

        var json = JsonSerializer.Serialize(settings, JsonSerializationContext.Default.SettingsModel);
        var deserialized = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel)!;

        CollectionAssert.AreEqual(order, deserialized.ExtensionOrder);
    }

    [TestMethod]
    public void ExtensionOrder_NullDeserializesToEmpty()
    {
        var json = """{"ExtensionOrder": null}""";
        var settings = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel)!;
        Assert.IsNotNull(settings.ExtensionOrder);
        Assert.AreEqual(0, settings.ExtensionOrder.Length);
    }

    [TestMethod]
    public void ExtensionOrder_PreservesOrderInJson()
    {
        var order = new[] { "z.ext", "a.ext", "m.ext" };
        var settings = DeserializeSettings("{}") with { ExtensionOrder = order };

        var json = JsonSerializer.Serialize(settings, JsonSerializationContext.Default.SettingsModel);
        var deserialized = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel)!;

        Assert.AreEqual("z.ext", deserialized.ExtensionOrder[0]);
        Assert.AreEqual("a.ext", deserialized.ExtensionOrder[1]);
        Assert.AreEqual("m.ext", deserialized.ExtensionOrder[2]);
    }

    [TestMethod]
    public void SortByExtensionOrder_EmptyList_ReturnsEmpty()
    {
        var result = ExtensionOrderHelper.SortByExtensionOrder(
            new List<string>(),
            OrderAB,
            s => s);

        Assert.AreEqual(0, result.Count);
    }

    [TestMethod]
    public void SortByExtensionOrder_EmptyOrder_PreservesOriginalOrder()
    {
        var items = new List<string> { "x", "y", "z" };
        var result = ExtensionOrderHelper.SortByExtensionOrder(items, [], s => s);

        CollectionAssert.AreEqual(ExpectedXYZ, result);
    }

    [TestMethod]
    public void SortByExtensionOrder_AllInOrder_SortsAccordingly()
    {
        var items = new List<string> { "c", "a", "b" };

        var result = ExtensionOrderHelper.SortByExtensionOrder(items, OrderABC, s => s);

        CollectionAssert.AreEqual(ExpectedABC, result);
    }

    [TestMethod]
    public void SortByExtensionOrder_NoneInOrder_PreservesOriginalOrder()
    {
        var items = new List<string> { "x", "y", "z" };

        var result = ExtensionOrderHelper.SortByExtensionOrder(items, OrderABC, s => s);

        CollectionAssert.AreEqual(ExpectedXYZ, result);
    }

    [TestMethod]
    public void SortByExtensionOrder_Mixed_OrderedFirstThenUnordered()
    {
        var items = new List<string> { "x", "b", "y", "a" };

        var result = ExtensionOrderHelper.SortByExtensionOrder(items, OrderAB, s => s);

        CollectionAssert.AreEqual(ExpectedABXY, result);
    }

    [TestMethod]
    public void SortByExtensionOrder_DuplicateProviderIds_AllGroupedInOrder()
    {
        var items = new List<string> { "b", "a", "b", "c", "a" };

        var result = ExtensionOrderHelper.SortByExtensionOrder(items, OrderAB, s => s);

        Assert.AreEqual("a", result[0]);
        Assert.AreEqual("a", result[1]);
        Assert.AreEqual("b", result[2]);
        Assert.AreEqual("b", result[3]);
        Assert.AreEqual("c", result[4]);
    }

    [TestMethod]
    public void SortByExtensionOrder_SingleElement_ReturnsIt()
    {
        var items = new List<string> { "a" };

        var result = ExtensionOrderHelper.SortByExtensionOrder(items, OrderAB, s => s);

        Assert.AreEqual(1, result.Count);
        Assert.AreEqual("a", result[0]);
    }

    [TestMethod]
    public void SortByExtensionOrder_SameProviderItems_KeepRelativeOrder()
    {
        // Two providers ("a" and "b"), each contributing several commands. The sort
        // must be stable so a single provider's commands are never shuffled.
        var items = new List<(string Provider, int Command)>
        {
            ("b", 0),
            ("a", 0),
            ("b", 1),
            ("a", 1),
            ("a", 2),
        };

        var result = ExtensionOrderHelper.SortByExtensionOrder(items, OrderAB, x => x.Provider);

        var expected = new List<(string, int)>
        {
            ("a", 0),
            ("a", 1),
            ("a", 2),
            ("b", 0),
            ("b", 1),
        };
        CollectionAssert.AreEqual(expected, result);
    }

    [TestMethod]
    public void SortByExtensionOrder_ExternalCanOutrankBuiltIn_WhenBothInOrder()
    {
        // Built-ins load before external extensions, but the configured order lists the
        // external provider first. Sorting the full list lets the external outrank the
        // built-in so the result matches what the reorder dialog showed (WYSIWYG).
        var items = new List<string> { "builtin.apps", "builtin.calc", "external.foo" };

        var result = ExtensionOrderHelper.SortByExtensionOrder(items, OrderExternalThenBuiltIns, s => s);

        CollectionAssert.AreEqual(ExpectedExternalFirst, result);
    }

    [TestMethod]
    public void SortByExtensionOrder_NewProviderNotInOrder_GoesToEnd()
    {
        // A provider installed after the last reorder isn't in the saved order, so it
        // keeps its natural load position at the end rather than jumping to the front.
        var items = new List<string> { "external.foo", "builtin.apps", "newly.installed" };

        var result = ExtensionOrderHelper.SortByExtensionOrder(items, OrderExternalThenApps, s => s);

        CollectionAssert.AreEqual(ExpectedNewProviderLast, result);
    }

    private static SettingsModel DeserializeSettings(string json)
    {
        return JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel) ?? new SettingsModel();
    }
}
