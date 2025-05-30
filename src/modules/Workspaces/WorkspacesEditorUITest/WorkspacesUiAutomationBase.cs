// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using Microsoft.PowerToys.UITest;

namespace WorkspacesEditorUITest
{
    public class WorkspacesUiAutomationBase : UITestBase
    {
        public WorkspacesUiAutomationBase()
            : base(PowerToysModule.Workspaces, WindowSize.Medium)
        {
        }

        protected void CreateTestWorkspace(string name)
        {
            // Open notepad for capture
            OpenNotepad();
            Thread.Sleep(2000);

            // Create workspace
            var createButton = Find<Button>("Create Workspace");
            createButton.Click();
            Thread.Sleep(1000);

            // Capture
            var captureButton = Find<Button>("Capture");
            captureButton.Click();
            Thread.Sleep(2000);

            // Set name
            var nameTextBox = Find<TextBox>("WorkspaceName");
            nameTextBox.SetText(string.Empty);
            nameTextBox.SetText(name);
            Thread.Sleep(500);

            // Save
            Find<Button>("Save").Click();
            Thread.Sleep(1000);

            // Close notepad
            CloseNotepad();
        }

        protected void CreateWorkspaceWithApps()
        {
            // Open multiple test applications
            OpenTestApplications();
            Thread.Sleep(3000);

            // Create workspace
            var createButton = Find<Button>("Create Workspace");
            createButton.Click();
            Thread.Sleep(1000);

            // Capture
            var captureButton = Find<Button>("Capture");
            captureButton.Click();
            Thread.Sleep(2000);

            // Save
            Find<Button>("Save").Click();
            Thread.Sleep(1000);

            // Close test applications
            CloseTestApplications();
        }

        protected void OpenTestApplications()
        {
            OpenNotepad();

            // Could add more applications here
            Thread.Sleep(1000);
        }

        protected void CloseTestApplications()
        {
            CloseNotepad();
        }

        protected void CloseWorkspacesEditor()
        {
            // Find and close the Workspaces Editor window
            if (WindowHelper.IsWindowOpen("Workspaces Editor"))
            {
                var editorWindow = Find<Window>("Workspaces Editor");
                editorWindow.Close();
                Thread.Sleep(1000);
            }
        }

        protected void ResetShortcutToDefault(Custom shortcutControl)
        {
            // Right-click on the shortcut control to open context menu
            shortcutControl.Click(rightClick: true);
            Thread.Sleep(500);

            // Look for a "Reset to default" or similar option in the context menu
            try
            {
                // Try to find various possible menu item texts for reset option
                var resetOption = Find("Reset to default");
                resetOption?.Click();
            }
            catch
            {
                try
                {
                    // Try alternative text
                    var resetOption = Find("Reset");
                    resetOption?.Click();
                }
                catch
                {
                    try
                    {
                        // Try another alternative
                        var resetOption = Find("Default");
                        resetOption?.Click();
                    }
                    catch
                    {
                        // If context menu doesn't have reset option, try keyboard shortcut
                        // ESC to close any open menus first
                        SendKeys(Key.Esc);
                        Thread.Sleep(200);

                        // Click on the control and try to clear it with standard shortcuts
                        shortcutControl.Click();
                        Thread.Sleep(200);

                        // Try Ctrl+A to select all, then Delete to clear
                        SendKeys(Key.Ctrl, Key.A);
                        Thread.Sleep(100);
                        SendKeys(Key.Delete);
                        Thread.Sleep(500);
                    }
                }
            }
        }

        protected void OpenNotepad()
        {
            var process = System.Diagnostics.Process.Start("notepad.exe");
            Thread.Sleep(1000);
        }

        protected void CloseNotepad()
        {
            var processes = System.Diagnostics.Process.GetProcessesByName("notepad");
            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                    process.WaitForExit();
                }
                catch
                {
                    // ignore
                }
            }
        }
    }
}
