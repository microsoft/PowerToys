// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using KeyboardManagerEditorUI.Helpers;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyboardManagerEditorUI.UnitTests
{
    [TestClass]
    public class MappingAccessibilityTests
    {
        private static readonly string[] SingleKey = ["A"];

        private static readonly Dictionary<string, string> Strings = new()
        {
            [MappingAccessibility.KeyRemapFormatKey] = "{0} maps to {1}",
            [MappingAccessibility.DisabledFormatKey] = "{0} is disabled",
            [MappingAccessibility.TextFormatKey] = "{0} inserts text {1}",
            [MappingAccessibility.ProgramFormatKey] = "{0} opens program {1}",
            [MappingAccessibility.UrlFormatKey] = "{0} opens {1}",
            [MappingAccessibility.AloneSuffixFormatKey] = "{0}, only when pressed alone",
            [MappingAccessibility.AppSuffixFormatKey] = "{0}, in {1}",
            [MappingAccessibility.EnableToggleFormatKey] = "Enable {0}",
            [MappingAccessibility.MoreOptionsFormatKey] = "More options for {0}",
        };

        private static string GetString(string key) => Strings.TryGetValue(key, out var value) ? value : string.Empty;

        [TestMethod]
        public void ForRemapping_DescribesTriggerAndTarget()
        {
            var remapping = new Remapping
            {
                Shortcut = new List<string> { "Ctrl", "A" },
                RemappedKeys = new List<string> { "Ctrl", "B" },
            };

            Assert.AreEqual("Ctrl + A maps to Ctrl + B", MappingAccessibility.ForRemapping(remapping, GetString));
        }

        [TestMethod]
        public void ForRemapping_IncludesAloneConditionAndApp()
        {
            var remapping = new Remapping
            {
                Shortcut = new List<string> { "Caps Lock" },
                RemappedKeys = new List<string> { "Esc" },
                Condition = Interop.SingleKeyRemapCondition.Alone,
                AppName = "notepad.exe",
            };

            Assert.AreEqual(
                "Caps Lock maps to Esc, only when pressed alone, in notepad.exe",
                MappingAccessibility.ForRemapping(remapping, GetString));
        }

        [TestMethod]
        public void ForRemapping_WithoutTarget_DescribesDisabledKey()
        {
            var remapping = new Remapping { Shortcut = new List<string> { "Insert" } };

            Assert.AreEqual("Insert is disabled", MappingAccessibility.ForRemapping(remapping, GetString));
        }

        [TestMethod]
        public void ForText_ForProgram_ForUrl_DescribeAction()
        {
            var keys = new List<string> { "Win", "Alt", "T" };

            Assert.AreEqual(
                "Win + Alt + T inserts text Hello",
                MappingAccessibility.ForText(new TextMapping { Shortcut = keys, Text = "Hello" }, GetString));
            Assert.AreEqual(
                "Win + Alt + T opens program notepad.exe",
                MappingAccessibility.ForProgram(new ProgramShortcut { Shortcut = keys, AppToRun = "notepad.exe" }, GetString));
            Assert.AreEqual(
                "Win + Alt + T opens https://aka.ms/powertoys",
                MappingAccessibility.ForUrl(new URLShortcut { Shortcut = keys, URL = "https://aka.ms/powertoys" }, GetString));
        }

        [TestMethod]
        public void RowControlNames_IncludeRowContext()
        {
            Assert.AreEqual("Enable A maps to B", MappingAccessibility.EnableToggleName("A maps to B", GetString));
            Assert.AreEqual("More options for A maps to B", MappingAccessibility.MoreOptionsName("A maps to B", GetString));
        }

        [TestMethod]
        public void MissingResource_StillProducesNonEmptyName()
        {
            string name = MappingAccessibility.Describe(_ => string.Empty, "missing", SingleKey, "B", false, null);

            Assert.AreEqual("A, B", name);
        }

        [TestMethod]
        public void JoinKeys_SkipsEmptyEntries()
        {
            Assert.AreEqual("Ctrl + A", MappingAccessibility.JoinKeys(new[] { "Ctrl", string.Empty, " ", "A" }));
            Assert.AreEqual(string.Empty, MappingAccessibility.JoinKeys(null));
        }

        [TestMethod]
        public void KeyNameEntry_ToString_IsDisplayName()
        {
            Assert.AreEqual("Z", new Interop.KeyNameEntry(90, "Z").ToString());
        }
    }
}
