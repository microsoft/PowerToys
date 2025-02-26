// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hosts.UITests
{
    [TestClass]
    public class HostModuleTests : UITestBase
    {
        public HostModuleTests()
            : base(PowerToysModule.Hosts)
        {
        }

        /// <summary>
        /// Test if Empty-view is shown when no entries are present.
        /// And 'Add an entry' button from Empty-view is functional.
        /// </summary>
        [TestMethod]
        public void TestEmptyView()
        {
            this.CloseWarningDialog();
            this.RemoveAllEntries();

            // 'Add an entry' button (only show-up when list is empty) should be visible
            Assert.IsTrue(this.FindAll<Button>(By.Name("Add an entry")).Count == 1, "'Add an entry' button should be visible in the empty view");

            // Click 'Add an entry' from empty-view for adding Host override rule
            this.Find<Button>(By.Name("Add an entry")).Click();

            this.AddEntry("192.168.0.1", "localhost", false, false);

            // Should have one row now and not more empty view
            Assert.IsTrue(this.FindAll<Button>(By.Name("Delete")).Count == 1, "Should have one row now");
            Assert.IsTrue(this.FindAll<Button>(By.Name("Add an entry")).Count == 0, "'Add an entry' button should be invisible if not empty view");
        }

        /// <summary>
        /// Test if 'New entry' button is functional
        /// </summary>
        [TestMethod]
        public void TestAddingEntry()
        {
            this.CloseWarningDialog();
            this.RemoveAllEntries();

            Assert.IsTrue(this.FindAll<Button>(By.Name("Delete")).Count == 0, "Should have no row after removing all");

            this.AddEntry("192.168.0.1", "localhost", true);

            Assert.IsTrue(this.FindAll<Button>(By.Name("Delete")).Count == 1, "Should have one row now");
        }

        private void AddEntry(string ip, string host, bool active = true, bool clickAddEntryButton = true)
        {
            if (clickAddEntryButton)
            {
                // Click 'Add an entry' for adding Host override rule
                this.Find<Button>(By.Name("New entry")).Click();
            }

            // Adding a new host override localhost -> 192.168.0.1
            Assert.IsFalse(this.Find<Button>(By.Name("Add")).Enabled, "Add button should be Disabled by default");

            Assert.IsTrue(this.Find<TextBox>(By.Name("Address")).SetText(ip, false).Text == ip);
            Assert.IsTrue(this.Find<TextBox>(By.Name("Hosts")).SetText(host, false).Text == host);

            this.Find<ToggleSwitch>(By.Name("Active")).Toggle(active);

            Assert.IsTrue(this.Find<Button>(By.Name("Add")).Enabled, "Add button should be Enabled after providing valid inputs");

            // Add the entry
            this.Find<Button>(By.Name("Add")).Click();

            // 0.5 second delay after adding an entry
            Task.Delay(500).Wait();
        }

        private void CloseWarningDialog()
        {
            // Find 'Accept' button which come in 'Warning' dialog
            if (this.FindAll<Window>(By.Name("Warning")).Count > 0 &&
                this.FindAll<Button>(By.Name("Accept")).Count > 0)
            {
                // Hide Warning dialog if any
                this.Find<Button>(By.Name("Accept")).Click();
            }
        }

        private void RemoveAllEntries()
        {
            // Delete all existing host-override rules
            foreach (var deleteBtn in this.FindAll<Button>(By.Name("Delete")))
            {
                deleteBtn.Click();
                this.Find<Button>(By.Name("Yes")).Click();
            }

            // Should have no row left, and no more delete button
            Assert.IsTrue(this.FindAll<Button>(By.Name("Delete")).Count == 0);
        }
    }
}
