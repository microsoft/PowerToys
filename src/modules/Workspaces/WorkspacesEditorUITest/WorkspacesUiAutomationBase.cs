// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
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
            Task.Delay(1000).Wait();

            // Create workspace
            AttachWorkspacesEditor();
            var createButton = Find<Button>("Create Workspace");
            createButton.Click();
            Task.Delay(500).Wait();

            // Capture
            AttachSnapshotWindow();
            var captureButton = Find<Button>("Capture");
            captureButton.Click();
            Task.Delay(5000).Wait();

            // Set name
            var nameTextBox = Find<TextBox>("EditNameTextBox");
            nameTextBox.SetText(name);

            // Save
            Find<Button>("Save Workspace").Click();

            // Close notepad
            CloseNotepad();
        }

        // DO NOT USE UNTIL FRAMEWORK AVAILABLE, CAN'T FIND BUTTON FOR SECOND LOOP
        protected void ClearWorkspaces()
        {
            while (true)
            {
                try
                {
                    var root = Find<Element>(By.AccessibilityId("WorkspacesItemsControl"));
                    var buttons = root.FindAll<Button>(By.AccessibilityId("MoreButton"));

                    Debug.WriteLine($"Found {buttons.Count} button");

                    var button = buttons[^1];

                    button.Click();

                    Task.Delay(500).Wait();

                    var remove = Find<Button>(By.Name("Remove"));
                    remove.Click();

                    Task.Delay(500).Wait();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex.Message);
                    break;
                }
            }
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
            Task.Delay(1000).Wait();
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

        private void AttachPowertoySetting()
        {
            Task.Delay(200).Wait();
            this.Session.Attach(PowerToysModule.PowerToysSettings);
        }

        protected void AttachWorkspacesEditor()
        {
            Task.Delay(200).Wait();
            this.Session.Attach(PowerToysModule.Workspaces);
        }

        protected void AttachSnapshotWindow()
        {
            Task.Delay(5000).Wait();
            this.Session.Attach("Snapshot Creator");
        }

        protected void OpenCalculator()
        {
            Process.Start("calc.exe");
            Task.Delay(1000).Wait();
        }

        protected void CloseCalculator()
        {
            foreach (var process in Process.GetProcessesByName("CalculatorApp"))
            {
                process.Kill();
            }

            foreach (var process in Process.GetProcessesByName("Calculator"))
            {
                process.Kill();
            }
        }

        protected void OpenWindowsSettings()
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "ms-settings:",
                UseShellExecute = true,
            });
            Task.Delay(500).Wait();
        }

        protected void CloseWindowsSettings()
        {
            foreach (var process in Process.GetProcessesByName("SystemSettings"))
            {
                process.Kill();
            }
        }
    }
}
