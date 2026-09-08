// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests
{
    public sealed partial class WorkspacesTests
    {
        [TestMethod]
        public void SettingsLaunchButtonOpensEditor()
        {
            Assert.IsTrue(ModuleToggle().IsOn, "Workspaces should start enabled.");
            OpenEditor();
            AssertWorkspaceNames();
        }

        [TestMethod]
        public void QuickAccessLaunchesEditor()
        {
            var runners = Process.GetProcessesByName("PowerToys");
            int runnerId;
            try
            {
                Assert.HasCount(1, runners, "Expected one Runner to own Quick Access.");
                runnerId = runners[0].Id;
            }
            finally
            {
                foreach (var runner in runners)
                {
                    runner.Dispose();
                }
            }

            Step("Showing the Runner-owned Quick Access flyout.");
            Assert.IsTrue(
                NamedEventHelper.WaitAndSignal($@"Local\PowerToysQuickAccess_{runnerId}_Show", 30_000),
                "The Runner did not expose its Quick Access show event.");
            var quickAccess = WindowsFinder.WaitForWindowByApp("PowerToys.QuickAccess", window => window.Width > 200 && window.Height > 100, 30_000);
            Assert.IsNotNull(quickAccess, "Quick Access did not appear.");
            Step("Invoking the Workspaces tile in Quick Access.");
            quickAccess.Find<Button>(By.Name("Workspaces"), 20_000).Invoke(msPostAction: 0);
            AttachEditor();
        }

        [TestMethod]
        public void ActivationShortcutOpensEditor()
        {
            ActivateWithShortcut(ReadActivationShortcut());
        }

        [TestMethod]
        public void CustomizedShortcutReachesRunnerAndReplacesTheOldChord()
        {
            var original = ReadActivationShortcut();
            var letter = original.Contains(Key.K) ? Key.J : Key.K;
            Key[] replacement = [Key.LWin, Key.Ctrl, Key.Shift, letter];
            Step("Opening the activation-shortcut editor.");
            SettingsUi.Find<Element>(By.AccessibilityId("WorkspacesActivationShortcut"))
                .Find<Button>(By.AccessibilityId("EditButton")).Invoke(msPostAction: 0);
            var settings = SettingsUi;
            Assert.IsTrue(settings.Has(By.AccessibilityId("PrimaryButton"), 15_000), "The shortcut dialog did not open.");
            SettingsUi.EnsureForeground();
            Assert.IsTrue(
                WindowControl.WaitForForeground(SettingsWindow(), 10_000),
                $"Settings did not acquire foreground for shortcut capture: {WindowControl.GetForegroundWindowInfo()}.");
            Step("Entering the replacement shortcut.");
            KeyboardHelper.SendKeys(replacement);
            Assert.IsTrue(
                settings.WaitFor(() => settings.FindAll<Element>(By.Name(letter.ToString()), 0).Any(element => element.Name == letter.ToString()), 15_000),
                "The shortcut dialog did not display the replacement key.");
            var save = settings.Find<Button>(By.AccessibilityId("PrimaryButton"));
            Assert.IsTrue(save.IsEnabled, "The replacement shortcut was rejected.");
            save.Invoke(msPostAction: 0);
            Assert.IsTrue(save.WaitForGone(15_000), "The shortcut dialog did not close.");
            CollectionAssert.AreEquivalent(replacement, ReadActivationShortcut(), "Settings did not display the saved shortcut.");
            ActivateWithShortcut(replacement);
            CloseEditor();
            KeyboardHelper.SendKeys(original);
            Assert.IsNull(
                WindowsFinder.WaitForWindowByApp(EditorProcess, window => window.Width > 400 && window.Height > 300, 5_000),
                "The previous shortcut remained registered after customization.");
        }

        [TestMethod]
        public void DisabledModuleRejectsShortcutUntilReenabled()
        {
            var shortcut = ReadActivationShortcut();
            ActivateWithShortcut(shortcut);
            CloseEditor();
            var toggle = ModuleToggle();
            try
            {
                Step("Disabling Workspaces through the real Settings switch.");
                toggle.Invoke(msPostAction: 0);
                Assert.IsTrue(toggle.WaitForProperty("ToggleState", "Off", 15_000), "The Settings switch did not turn off.");
                WaitForModuleEnabled(false);
                Assert.IsFalse(SettingsUi.Find<Element>(By.AccessibilityId("WorkspacesLaunchEditorButtonControl")).IsEnabled, "The launch action remained enabled.");

                Step("Sending the previously working shortcut while Workspaces is disabled.");
                KeyboardHelper.SendKeys(shortcut);
                var forbidden = WindowsFinder.WaitForWindowByApp(EditorProcess, window => window.Width > 400 && window.Height > 300, 5_000);
                Assert.IsNull(forbidden, "Disabled Workspaces still opened from its activation shortcut.");
            }
            finally
            {
                if (!toggle.IsOn)
                {
                    Step("Restoring Workspaces through Settings.");
                    toggle.Invoke(msPostAction: 0);
                    Assert.IsTrue(toggle.WaitForProperty("ToggleState", "On", 15_000), "Could not restore the module's enabled state.");
                    WaitForModuleEnabled(true);
                }
            }

            ActivateWithShortcut(shortcut);
        }

        private static void WaitForModuleEnabled(bool expected)
        {
            var path = Path.Combine(SettingsConfigHelper.PowerToysSettingsRoot, "settings.json");
            var result = WaitHelper.WaitForStable(
                () => WorkspaceTestState.ReadObject(path)["enabled"]!["Workspaces"]!.GetValue<bool>(),
                enabled => enabled == expected,
                15_000,
                requiredConsecutiveMatches: 2,
                shouldRetryException: error => error is IOException or JsonException);
            Assert.IsTrue(result.Succeeded, $"The Runner did not persist the Settings command enabling Workspaces={expected}. Last error: {result.LastException?.Message}");
        }

        private Key[] ReadActivationShortcut()
        {
            var card = SettingsUi.Find<Element>(By.AccessibilityId("WorkspacesActivationShortcut"), 15_000);
            var text = card.Find<Button>(By.AccessibilityId("EditButton"), 15_000).HelpText;
            Assert.IsFalse(string.IsNullOrWhiteSpace(text), "The activation shortcut has no accessible HelpText.");
            Step($"Reading the current activation shortcut: {text}");
            return text.Split(['+', ' '], StringSplitOptions.RemoveEmptyEntries).Select(part => part.ToUpperInvariant() switch
            {
                "WIN" or "WINDOWS" => Key.LWin,
                "CTRL" or "CONTROL" => Key.Ctrl,
                "SHIFT" => Key.Shift,
                "ALT" => Key.Alt,
                var key when key.Length == 1 && key[0] is >= 'A' and <= 'Z' => Enum.Parse<Key>(key),
                _ => throw new InvalidOperationException($"Unsupported shortcut token '{part}' in '{text}'."),
            }).ToArray();
        }

        private void ActivateWithShortcut(Key[] shortcut)
        {
            for (var attempt = 0; attempt < 3; attempt++)
            {
                Step($"Sending the activation shortcut, attempt {attempt + 1}.");
                KeyboardHelper.SendKeys(shortcut);
                var window = WindowsFinder.WaitForWindowByApp(EditorProcess, candidate => candidate.Width > 400 && candidate.Height > 300, 5_000);
                if (window is not null)
                {
                    AttachEditor();
                    return;
                }
            }

            Assert.Fail("The activation shortcut did not open Workspaces Editor.");
        }
    }
}
