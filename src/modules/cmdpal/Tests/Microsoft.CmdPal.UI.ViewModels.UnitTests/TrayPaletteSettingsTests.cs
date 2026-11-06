// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Text.Json;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.CmdPal.UI.ViewModels.Settings;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class TrayPaletteSettingsTests
{
    [TestMethod]
    public void ExistingSettingsGetQuickActionsAndDefaultPins()
    {
        var settings = JsonSerializer.Deserialize("{}", JsonSerializationContext.Default.SettingsModel)!;
        Assert.AreEqual(TrayIconClickAction.OpenQuickActions, settings.TrayIconClickAction);
        Assert.AreEqual(2, settings.TrayPalette.Commands.Count);
        Assert.AreEqual("Windows.ClipboardHistory", settings.TrayPalette.Commands[0].ProviderId);
        Assert.AreEqual("com.microsoft.cmdpal.clipboardHistory", settings.TrayPalette.Commands[0].CommandId);
        Assert.AreEqual("com.microsoft.cmdpal.builtin.core", settings.TrayPalette.Commands[1].ProviderId);
        Assert.AreEqual("com.microsoft.cmdpal.opengallerysettings", settings.TrayPalette.Commands[1].CommandId); /* #no-spell-check-line */
    }

    [TestMethod]
    public void EmptyPinsAndClickActionSurviveRoundTrip()
    {
        var settings = new SettingsModel
        {
            TrayIconClickAction = TrayIconClickAction.OpenCommandPalette,
            TrayPalette = new() { Commands = [] },
        };
        var json = JsonSerializer.Serialize(settings, JsonSerializationContext.Default.SettingsModel);
        var restored = JsonSerializer.Deserialize(json, JsonSerializationContext.Default.SettingsModel)!;
        Assert.AreEqual(TrayIconClickAction.OpenCommandPalette, restored.TrayIconClickAction);
        Assert.IsEmpty(restored.TrayPalette.Commands);
    }

    [TestMethod]
    public void PinsUseProviderAndCommandIdentity()
    {
        var first = new PinnedCommandSettings("one", "command");
        var second = new PinnedCommandSettings("two", "command");
        var settings = new TrayPaletteSettings { Commands = [] }.Pin(first).Pin(first).Pin(second);
        CollectionAssert.AreEqual(new[] { first, second }, settings.Commands);
        CollectionAssert.AreEqual(new[] { second }, settings.Unpin(first).Commands);
    }

    [TestMethod]
    public void ReorderPreservesUnavailablePins()
    {
        var first = new PinnedCommandSettings("one", "first");
        var hidden = new PinnedCommandSettings("missing", "hidden");
        var last = new PinnedCommandSettings("one", "last");
        var settings = new TrayPaletteSettings { Commands = [first, hidden, last] };
        var reordered = settings.Reorder([last, first]);
        CollectionAssert.AreEqual(new[] { last, hidden, first }, reordered.Commands);
        CollectionAssert.AreEqual(new[] { first, hidden, last }, settings.Commands);
    }

    [TestMethod]
    public void ReorderRejectsDuplicateOrUnknownPins()
    {
        var pin = new PinnedCommandSettings("one", "command");
        var settings = new TrayPaletteSettings { Commands = [pin] };
        Assert.ThrowsExactly<ArgumentException>(() => settings.Reorder([pin, pin]));
        Assert.ThrowsExactly<ArgumentException>(() => settings.Reorder([new("unknown", "command")]));
    }
}
