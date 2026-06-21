// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class ExtensionOrderTests
{
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

    private static SettingsModel DeserializeSettings(string json)
    {
        return JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel) ?? new SettingsModel();
    }
}
