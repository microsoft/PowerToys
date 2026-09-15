// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.CmdPal.UI.ViewModels.UnitTests;

[TestClass]
public class SettingsModelPinnedCommandTests
{
    [TestMethod]
    public void TryPlacePinnedCommand_MovesBeforeTargetWithoutDroppingOtherPins()
    {
        var settings = CreateSettings(
            new("provider-a", "first"),
            new("missing-provider", "stale"),
            new("provider-b", "target"),
            new("provider-c", "last"));

        var updated = settings.TryPlacePinnedCommand(
            "provider-c",
            "last",
            "provider-b",
            "target",
            placeAfter: false);

        string[] expectedOrder = ["first", "stale", "last", "target"];
        CollectionAssert.AreEqual(expectedOrder, updated.PinnedCommands.Select(pin => pin.CommandId).ToArray());
    }

    [TestMethod]
    public void TryPlacePinnedCommand_MovesAfterTargetAndUpdatesProviderOrder()
    {
        var settings = CreateSettings(
            new("provider-a", "first"),
            new("provider-a", "second"),
            new("provider-b", "target"));

        var updated = settings.TryPlacePinnedCommand(
            "provider-a",
            "first",
            "provider-b",
            "target",
            placeAfter: true);

        string[] expectedOrder = ["second", "target", "first"];
        string[] expectedProviderOrder = ["second", "first"];
        CollectionAssert.AreEqual(expectedOrder, updated.PinnedCommands.Select(pin => pin.CommandId).ToArray());
        CollectionAssert.AreEqual(expectedProviderOrder, updated.GetPinnedCommandIds("provider-a").ToArray());
    }

    [TestMethod]
    public void TryPlacePinnedCommand_NoOpReturnsSameSettingsInstance()
    {
        var settings = CreateSettings(
            new("provider-a", "first"),
            new("provider-b", "target"));

        Assert.AreSame(
            settings,
            settings.TryPlacePinnedCommand(
                "provider-a",
                "first",
                "provider-b",
                "target",
                placeAfter: false));
        Assert.AreSame(
            settings,
            settings.TryPlacePinnedCommand(
                "missing",
                "source",
                "provider-b",
                "target",
                placeAfter: true));
    }

    private static SettingsModel CreateSettings(params PinnedCommandSettings[] pins)
    {
        var settings = new SettingsModel();
        foreach (var pin in pins)
        {
            settings = settings.TryPinCommand(pin.ProviderId, pin.CommandId);
        }

        return settings;
    }
}
