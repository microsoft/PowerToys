// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using PowerDisplay.Models;

namespace ViewModelTests
{
    [TestClass]
    public class VcpValueBlockEditorViewModelTests
    {
        [TestMethod]
        public void Editor_UsesFormattedNamesAndSavesNumericCodeAndValue()
        {
            var monitor = CreateMonitor("DISPLAY#TEST001#1");
            monitor.VcpCodesFormatted[0].Title = "Localized power mode (0xD6)";
            monitor.VcpCodesFormatted[0].ValueList[1].Name = "My panel off option";
            var editor = new VcpValueBlockEditorViewModel(monitor);

            Assert.AreEqual("Localized power mode (0xD6)", editor.AvailableCodes.Single().Title);
            var hardOff = editor.AvailableValues.Single(value => value.Value == 0x05);
            Assert.AreEqual("My panel off option (0x05)", hardOff.DisplayName);

            hardOff.IsDisabled = true;
            var saved = editor.CreateValueBlocks().Single();

            Assert.AreEqual((byte)0xD6, saved.VcpCode);
            Assert.AreEqual(0x05, saved.Values.Single());
        }

        [TestMethod]
        public void Editor_OnlyOffersThisMonitorsReportedDiscreteOptions()
        {
            var first = CreateMonitor("DISPLAY#TEST001#1");
            first.DisabledVcpValues = new List<VcpValueBlock> { new() { VcpCode = 0xD6, Values = new() { 0x05 } } };
            var second = CreateMonitor("DISPLAY#TEST001#2");
            second.VcpCodesFormatted = new List<VcpCodeDisplayInfo>
            {
                new() { Code = "0x10", Title = "Brightness" },
                new()
                {
                    Code = "0x60",
                    Title = "Input source (0x60)",
                    ValueList = new() { new() { Value = "0x11", Name = "HDMI-1" } },
                },
            };

            var firstEditor = new VcpValueBlockEditorViewModel(first);
            var secondEditor = new VcpValueBlockEditorViewModel(second);

            Assert.IsTrue(firstEditor.AvailableValues.Single(value => value.Value == 0x05).IsDisabled);
            Assert.AreEqual((byte)0x60, secondEditor.AvailableCodes.Single().Code);
            Assert.AreEqual(0x11, secondEditor.AvailableValues.Single().Value);
            Assert.IsFalse(secondEditor.AvailableValues.Single().IsDisabled);
            Assert.AreEqual(0, secondEditor.CreateValueBlocks().Count);
        }

        [TestMethod]
        public void Editor_CancelLeavesOriginalRulesUnchangedAndResultDoesNotAliasThem()
        {
            var monitor = CreateMonitor("DISPLAY#TEST001#1");
            monitor.DisabledVcpValues = new List<VcpValueBlock> { new() { VcpCode = 0xD6, Values = new() { 0x05 } } };
            var editor = new VcpValueBlockEditorViewModel(monitor);

            editor.AvailableValues.Single(value => value.Value == 0x05).IsDisabled = false;
            editor.AvailableValues.Single(value => value.Value == 0x01).IsDisabled = true;

            Assert.AreEqual(0x05, monitor.DisabledVcpValues.Single().Values.Single());
            var saved = editor.CreateValueBlocks();
            saved.Single().Values.Clear();
            Assert.AreEqual(0x05, monitor.DisabledVcpValues.Single().Values.Single());
            Assert.AreEqual(0x01, editor.CreateValueBlocks().Single().Values.Single());
        }

        [TestMethod]
        public void Editor_SavePreservesRulesMissingFromCurrentCapabilities()
        {
            var monitor = CreateMonitor("DISPLAY#TEST001#1");
            monitor.DisabledVcpValues = new List<VcpValueBlock>
            {
                new() { VcpCode = 0xD6, Values = new() { 0x04, 0x05 } },
                new() { VcpCode = 0x60, Values = new() { 0x11 } },
            };
            var editor = new VcpValueBlockEditorViewModel(monitor);
            editor.AvailableValues.Single(value => value.Value == 0x05).IsDisabled = false;

            var saved = editor.CreateValueBlocks();

            Assert.AreEqual(0x04, saved.Single(block => block.VcpCode == 0xD6).Values.Single());
            Assert.AreEqual(0x11, saved.Single(block => block.VcpCode == 0x60).Values.Single());
        }

        [TestMethod]
        public void Editor_ChangingSelectedCodeRetainsEditsForAllCodes()
        {
            var monitor = CreateMonitor("DISPLAY#TEST001#1");
            monitor.VcpCodesFormatted.Add(new VcpCodeDisplayInfo
            {
                Code = "0x60",
                Title = "Input source (0x60)",
                ValueList = new() { new() { Value = "0x11", Name = "HDMI-1" } },
            });
            var editor = new VcpValueBlockEditorViewModel(monitor);
            editor.AvailableValues.Single(value => value.Value == 0x05).IsDisabled = true;

            editor.SelectedCode = editor.AvailableCodes.Single(code => code.Code == 0x60);
            editor.AvailableValues.Single().IsDisabled = true;
            editor.SelectedCode = editor.AvailableCodes.Single(code => code.Code == 0xD6);

            Assert.IsTrue(editor.AvailableValues.Single(value => value.Value == 0x05).IsDisabled);
            var saved = editor.CreateValueBlocks();
            Assert.AreEqual(0x05, saved.Single(block => block.VcpCode == 0xD6).Values.Single());
            Assert.AreEqual(0x11, saved.Single(block => block.VcpCode == 0x60).Values.Single());
        }

        [TestMethod]
        public void Editor_HardwareRestrictionIsCheckedLockedAndNotPersistedAsUserPreference()
        {
            var monitor = CreateMonitor("DISPLAY#SAM105C#1");
            var editor = new VcpValueBlockEditorViewModel(monitor);
            var hardOff = editor.AvailableValues.Single(value => value.Value == 0x05);

            Assert.IsTrue(editor.HasHardwareBlocks);
            Assert.IsTrue(hardOff.IsDisabled);
            Assert.IsTrue(hardOff.IsHardwareBlocked);
            Assert.IsFalse(string.IsNullOrWhiteSpace(hardOff.HardwareBlockReason));

            hardOff.IsDisabled = false;

            Assert.IsTrue(hardOff.IsDisabled);
            Assert.AreEqual(0, editor.CreateValueBlocks().Count);
        }

        [TestMethod]
        public void Editor_NoDiscreteCapabilitiesHasEmptyStateAndRetainsExistingRules()
        {
            var monitor = new MonitorInfo
            {
                Id = "DISPLAY#TEST001#1",
                DisabledVcpValues = new() { new() { VcpCode = 0xD6, Values = new() { 0x05 } } },
            };

            var editor = new VcpValueBlockEditorViewModel(monitor);

            Assert.IsFalse(editor.HasOptions);
            Assert.IsNull(editor.SelectedCode);
            Assert.AreEqual(0, editor.AvailableValues.Count);
            Assert.AreEqual(0x05, editor.CreateValueBlocks().Single().Values.Single());
        }

        private static MonitorInfo CreateMonitor(string id)
        {
            return new MonitorInfo
            {
                Id = id,
                Name = "Monitor",
                VcpCodesFormatted = new()
                {
                    new()
                    {
                        Code = "0xD6",
                        Title = "Power mode (0xD6)",
                        ValueList = new()
                        {
                            new() { Value = "0x01", Name = "On" },
                            new() { Value = "0x05", Name = "Off (Hard)" },
                        },
                    },
                },
            };
        }
    }
}
