// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Workspaces.UITests;

public sealed partial class WorkspacesTests
{
    [TestMethod]
    public void DisablingWorkspacesDuringCaptureSavesEditorPlacement()
    {
        string placementPath = WorkspaceTestState.WindowPlacementPath;
        var toggle = ModuleToggle();
        try
        {
            OpenEditor();
            var firstWindow = new IntPtr(Editor.WindowHandle);
            WindowHelper.MinimizeWindow(firstWindow);
            Assert.IsTrue(NativeMethods.IsIconic(firstWindow), "The editor did not minimize before reactivation.");
            SettingsUi.EnsureForeground();
            Assert.IsTrue(WindowControl.WaitForForeground(SettingsWindow(), 10_000), "Settings did not receive focus before reactivation.");
            var launchCard = SettingsUi.Find<Button>(By.AccessibilityId("WorkspacesLaunchEditorButtonControl"), 15_000);
            launchCard.Focus();
            Assert.IsTrue(launchCard.WaitForProperty("HasKeyboardFocus", "True"), "The Settings launch card did not receive keyboard focus.");
            KeyboardHelper.SendKeys(Key.Enter);
            Assert.IsTrue(
                WaitHelper.WaitForStable(
                    WindowControl.GetForegroundWindowHandle,
                    foreground => foreground == firstWindow && !NativeMethods.IsIconic(firstWindow),
                    10_000,
                    requiredConsecutiveMatches: 2).Succeeded,
                "The second Settings activation did not restore the original editor to the foreground.");
            _ = StartCapture();
            File.Delete(placementPath);

            toggle.Invoke(msPostAction: 0);
            WaitForModuleEnabled(false);
            Assert.IsTrue(WaitForProcess(EditorProcess, false), "Disabling Workspaces did not close the editor and capture windows.");
            Assert.IsTrue(File.Exists(placementPath), "The editor exited before saving its window placement.");
            Assert.IsTrue(WorkspaceTestState.ReadObject(placementPath).Count > 0, "The editor saved an empty placement.");
        }
        finally
        {
            if (!toggle.IsOn)
            {
                toggle.Invoke(msPostAction: 0);
                WaitForModuleEnabled(true);
            }
        }
    }

    [TestMethod]
    public void ActivationShortcutReturnsToCaptureAndKeepsMainWindowMinimized()
    {
        var shortcut = ReadActivationShortcut();
        OpenEditor();
        var mainWindow = new IntPtr(Editor.WindowHandle);
        var capture = StartCapture();

        SettingsUi.EnsureForeground();
        Assert.IsTrue(WindowControl.WaitForForeground(SettingsWindow(), 10_000), "Settings did not receive focus during capture.");
        KeyboardHelper.SendKeys(shortcut);
        Assert.IsTrue(
            WaitHelper.WaitForStable(
                WindowControl.GetForegroundWindowHandle,
                window => window == new IntPtr(capture.WindowHandle),
                10_000,
                requiredConsecutiveMatches: 2).Succeeded,
            "The activation shortcut did not return focus to the capture window.");
        Assert.IsTrue(NativeMethods.IsIconic(mainWindow), "The main editor covered the desktop being captured.");
    }
}
