// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesCsharpLibrary.Data;

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
            OpenNotepad();
            try
            {
                StartCapture();
                CaptureWorkspace();
                Find<TextBox>(By.AccessibilityId("EditNameTextBox")).SetText(name);
                SaveWorkspace();
                Assert.AreEqual(name, GetWorkspaceCard(name).Name, "The captured workspace should appear in the list.");
            }
            finally
            {
                CloseNotepad();
            }
        }

        protected string CreateWorkspaceWithApps()
        {
            string name = "LegacyLaunch_" + Guid.NewGuid().ToString("N");
            OpenTestApplications();
            try
            {
                StartCapture();
                CaptureWorkspace();
                Find<TextBox>(By.AccessibilityId("EditNameTextBox")).SetText(name);
                SaveWorkspace();
                return name;
            }
            finally
            {
                CloseTestApplications();
            }
        }

        protected void StartCapture()
        {
            AttachWorkspacesEditor();
            Find<Button>(By.AccessibilityId("NewProjectButton")).Click();
            AttachSnapshotWindow();
            Assert.IsTrue(Find<Button>(By.AccessibilityId("SnapshotButton")).Displayed, "The snapshot control window should be ready.");
        }

        protected void CaptureWorkspace()
        {
            Find<Button>(By.AccessibilityId("SnapshotButton")).Click();
            AttachWorkspacesEditor();
            Assert.IsTrue(Find<TextBox>(By.AccessibilityId("EditNameTextBox"), timeoutMS: 15000).Displayed, "Capture should open the editing page.");
        }

        protected void SaveWorkspace()
        {
            var save = Find<Button>(By.AccessibilityId("SaveButton"));
            Assert.IsTrue(save.Enabled, "The workspace should be saveable.");
            save.Click();
            AssertMainPage();
        }

        protected void ReturnToMainPage()
        {
            Find<Button>(By.AccessibilityId("PART_BackButton")).Click();
            AssertMainPage();
        }

        protected void AssertMainPage()
        {
            Assert.IsTrue(Find<Button>(By.AccessibilityId("NewProjectButton"), timeoutMS: 15000).Displayed, "The workspace list should be visible.");
            Assert.IsFalse(Has(By.AccessibilityId("EditNameTextBox"), timeoutMS: 500), "An old editing page should not remain active after returning to the list.");
        }

        protected List<Element> GetWorkspaceCards()
        {
            AssertMainPage();
            var storage = new WorkspacesData();
            List<ProjectWrapper> saved = File.Exists(storage.File) ? storage.Read(storage.File).Workspaces ?? [] : [];
            var namesById = saved.Where(project => Guid.TryParse(project.Id, out _))
                .ToDictionary(project => Guid.Parse(project.Id), project => project.Name);

            // ItemsRepeater is absent from the WinUI UIA tree. Its SettingsCards remain Button peers
            // with stable GUID IDs; require their persisted names so labels and suggestions cannot match.
            return FindAll<Button>(By.XPath("//*[@AutomationId]"), timeoutMS: 1000)
                .Where(button => button.ControlType == "ControlType.Button"
                    && Guid.TryParse(button.AutomationId, out var id)
                    && namesById.TryGetValue(id, out var name)
                    && string.Equals(button.Name, name, StringComparison.Ordinal))
                .Cast<Element>()
                .OrderBy(element => element.Rect?.Top)
                .ToList();
        }

        protected Element GetWorkspaceCard(string name)
        {
            var cards = GetWorkspaceCards().Where(card => card.Name == name).ToList();
            Assert.AreEqual(1, cards.Count, $"The workspace '{name}' should have one addressable card.");
            return cards[0];
        }

        protected void OpenWorkspace(string name)
        {
            AttachWorkspacesEditor();
            GetWorkspaceCard(name).Click();
            Assert.AreEqual(name, Find<TextBox>(By.AccessibilityId("EditNameTextBox")).Text, "The card should open its workspace for editing.");
        }

        protected void DeleteWorkspace(string name)
        {
            OpenWorkspace(name);
            Find<Button>(By.AccessibilityId("DeleteWorkspaceButton")).Click();
            Find<Button>(By.Name("Remove")).Click();
            AssertMainPage();
            Assert.IsFalse(GetWorkspaceCards().Any(card => card.Name == name), "The workspace should be removed from the list.");
        }

        protected void SelectSortOrder(string automationId)
        {
            Find<Button>(By.AccessibilityId("WorkspaceSortButton")).Click();
            Find<Element>(By.AccessibilityId(automationId)).Click();
        }

        protected void AssertSortOrderSelected(string automationId)
        {
            Find<Button>(By.AccessibilityId("WorkspaceSortButton")).Click();
            var item = Find<Element>(By.AccessibilityId(automationId));
            Assert.IsTrue(item.Selected || string.Equals(item.GetAttribute("Toggle.ToggleState"), "1", StringComparison.Ordinal), "The sort flyout should mark the selected order.");
            SendKeys(Key.Esc);
        }

        protected TextBox GetSearchBox()
        {
            var search = Find<Element>(By.AccessibilityId("SearchTextBox"));
            return search.ControlType == "ControlType.Edit"
                ? Find<TextBox>(By.AccessibilityId("SearchTextBox"))
                : search.Find<TextBox>(By.ClassName("TextBox"));
        }

        protected void CancelSnapshot()
        {
            Find<Button>(By.AccessibilityId("CancelButton")).Click();
            AttachWorkspacesEditor();
            AssertMainPage();
        }

        protected Element GetPreviewImage()
        {
            var image = Find<Element>(By.AccessibilityId("WorkspacePreviewImage"));
            Assert.IsTrue(image.Displayed, "The workspace layout preview should be visible.");
            Assert.IsTrue(image.Rect is { Width: > 0, Height: > 0 }, "The workspace layout preview should have visible geometry.");
            return image;
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
