// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.PowerToys.Settings.UI;
using Microsoft.PowerToys.Settings.UI.Views;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using MonitorPower;

namespace CommonLibTest;

[TestClass]
public sealed class MonitorPowerTests
{
    [TestMethod]
    public void ProfilePersistence_RoundTripsAndRequiresExplicitOverwrite()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"MonitorPowerTests-{Guid.NewGuid():N}");
        var store = new MonitorPowerProfilePersistence(directory);
        var target = new DisplayHelpers.DisplayTargetId(new LUID { LowPart = 12, HighPart = 34 }, 5);
        var profile = new MonitorPowerProfile { Name = "Desk", Targets = [target] };

        try
        {
            store.Save("Desk.json", profile, overwrite: false);

            var loaded = store.Load("Desk.json");
            Assert.IsNotNull(loaded);
            Assert.AreEqual("Desk", loaded.Name);
            CollectionAssert.AreEqual(profile.Targets, loaded.Targets);
            Assert.ThrowsException<IOException>(() => store.Save("Desk.json", profile, overwrite: false));

            var replacement = new MonitorPowerProfile { Name = "Desk", Targets = [] };
            store.Save("Desk.json", replacement, overwrite: true);
            Assert.AreEqual(0, store.Load("Desk.json")!.Targets.Count);
            Assert.AreEqual("Desk", store.List().Single().Name);

            store.Delete("Desk.json");
            Assert.IsNull(store.Load("Desk.json"));
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    [TestMethod]
    public void TopologyMapping_AssignsOneUniqueDeviceNamePerTarget()
    {
        var first = new DisplayHelpers.DisplayTargetId(new LUID { LowPart = 1 }, 1);
        var second = new DisplayHelpers.DisplayTargetId(new LUID { LowPart = 2 }, 2);
        var map = DisplayHelpers.AssignUniqueDeviceNames(
        [
            new DisplayHelpers.DisplayNameCandidate(first, @"\\.\DISPLAY3", false, 0),
            new DisplayHelpers.DisplayNameCandidate(first, @"\\.\DISPLAY2", true, 1),
            new DisplayHelpers.DisplayNameCandidate(second, @"\\.\DISPLAY1", false, 2),
        ]);

        Assert.AreEqual(2, map.Count);
        Assert.AreEqual(@"\\.\DISPLAY2", map[first]);
        Assert.AreEqual(@"\\.\DISPLAY1", map[second]);
    }

    [TestMethod]
    public void TopologyMapping_ClassifiesInternalAndExternalSelections()
    {
        var internalTarget = new DisplayHelpers.DisplayTargetId(new LUID { LowPart = 1 }, 1);
        var externalTarget = new DisplayHelpers.DisplayTargetId(new LUID { LowPart = 2 }, 2);
        var technologies = new Dictionary<DisplayHelpers.DisplayTargetId, DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY>
        {
            [internalTarget] = DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.Internal,
            [externalTarget] = DISPLAYCONFIG_VIDEO_OUTPUT_TECHNOLOGY.HDMI,
        };

        Assert.AreEqual(
            DISPLAYCONFIG_TOPOLOGY_ID.Internal,
            DisplayHelpers.ClassifyTopology([internalTarget], technologies));
        Assert.AreEqual(
            DISPLAYCONFIG_TOPOLOGY_ID.Extend,
            DisplayHelpers.ClassifyTopology([internalTarget, externalTarget], technologies));
    }

    [TestMethod]
    public void ControllerChord_ParsesTwoDistinctSupportedButtons()
    {
        Assert.IsTrue(ControllerChord.TryParse("Guide + View", out var chord));
        Assert.IsTrue(chord.IsPressed(0x0420));
        Assert.IsFalse(chord.IsPressed(0x0400));
        Assert.IsFalse(ControllerChord.TryParse("Guide + Guide", out _));
        Assert.IsFalse(ControllerChord.TryParse("Guide + Unknown", out _));
    }

    [TestMethod]
    public void KeyboardActivationShortcut_ParsesAndRejectsReservedShortcuts()
    {
        Assert.IsTrue(KeyboardActivationShortcut.TryParse("Win + Shift + P", out var shortcut, out _));
        Assert.AreEqual("Win + Shift + P", shortcut!.Text);
        Assert.IsTrue(shortcut.Win);
        Assert.IsTrue(shortcut.Shift);
        Assert.IsFalse(KeyboardActivationShortcut.TryParse("Win + P", out _, out _));
        Assert.IsFalse(KeyboardActivationShortcut.TryParse("Alt + F4", out _, out _));
        Assert.IsFalse(KeyboardActivationShortcut.TryParse("Ctrl + Shift + Escape", out _, out _));
        Assert.IsFalse(KeyboardActivationShortcut.TryParse("Ctrl + Ctrl + A", out _, out _));
        Assert.IsFalse(KeyboardActivationShortcut.TryParse("P", out _, out _));
    }

    [TestMethod]
    public void SettingsRouting_ResolvesMonitorPowerPage()
    {
        Assert.AreEqual(typeof(DisplayProfilesPage), App.GetPage("DisplayProfiles"));
    }
}
