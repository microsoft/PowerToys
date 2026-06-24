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
    private static readonly string[] OrderXY = ["x", "y"];
    private static readonly string[] ExpectedXYZ = ["x", "y", "z"];
    private static readonly string[] ExpectedABC = ["a", "b", "c"];
    private static readonly string[] ExpectedABXY = ["a", "b", "x", "y"];

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
    public void FindInsertIndex_ProviderNotInOrder_ReturnsCount()
    {
        var items = new List<string> { "a", "b", "c" };

        var index = ExtensionOrderHelper.FindInsertIndex(items, "unknown", OrderXY, s => s);

        Assert.AreEqual(3, index);
    }

    [TestMethod]
    public void FindInsertIndex_EmptyItems_ReturnsZero()
    {
        var items = new List<string>();

        var index = ExtensionOrderHelper.FindInsertIndex(items, "a", OrderAB, s => s);

        Assert.AreEqual(0, index);
    }

    [TestMethod]
    public void FindInsertIndex_FirstInOrder_ReturnsZero()
    {
        var items = new List<string> { "b", "c", "x" };

        var index = ExtensionOrderHelper.FindInsertIndex(items, "a", OrderABC, s => s);

        Assert.AreEqual(0, index);
    }

    [TestMethod]
    public void FindInsertIndex_BetweenOrderedProviders_ReturnsCorrectPosition()
    {
        var items = new List<string> { "a", "a", "c", "c" };

        var index = ExtensionOrderHelper.FindInsertIndex(items, "b", OrderABC, s => s);

        Assert.AreEqual(2, index);
    }

    [TestMethod]
    public void FindInsertIndex_AfterLastOrderedProvider_ReturnsEnd()
    {
        var items = new List<string> { "a", "b" };

        var index = ExtensionOrderHelper.FindInsertIndex(items, "c", OrderABC, s => s);

        Assert.AreEqual(2, index);
    }

    [TestMethod]
    public void FindInsertIndex_WithUnorderedItemsAfterOrdered_InsertsCorrectly()
    {
        var items = new List<string> { "a", "x", "y" };

        var index = ExtensionOrderHelper.FindInsertIndex(items, "b", OrderAB, s => s);

        Assert.AreEqual(1, index);
    }

    private static SettingsModel DeserializeSettings(string json)
    {
        return JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel) ?? new SettingsModel();
    }
}
