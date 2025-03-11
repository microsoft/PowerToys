// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
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
        /// Test Empty-view in the Hosts-File-Editor
        /// <list type="bullet">
        /// <item>
        /// <description>Validating Empty-view is shown if no entries in the list.</description>
        /// </item>
        /// <item>
        /// <description>Validating Empty-view is NOT shown if 1 or more entries in the list.</description>
        /// </item>
        /// <item>
        /// <description>Validating Add-an-entry HyperlinkButton in Empty-view works correctly.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestEmptyView()
        {
            this.CloseWarningDialog();
            this.RemoveAllEntries();

            // 'Add an entry' button (only show-up when list is empty) should be visible
            Assert.IsTrue(this.FindAll<HyperlinkButton>("Add an entry").Count == 1, "'Add an entry' button should be visible in the empty view");

            // Click 'Add an entry' from empty-view for adding Host override rule
            this.Find<HyperlinkButton>("Add an entry").Click();

            this.AddEntry("192.168.0.1", "localhost", false, false);

            // Should have one row now and not more empty view
            Assert.IsTrue(this.FindAll<Button>("Delete").Count == 1, "Should have one row now");
            Assert.IsTrue(this.FindAll<HyperlinkButton>("Add an entry").Count == 0, "'Add an entry' button should be invisible if not empty view");
        }

        /// <summary>
        /// Test Adding-entry Button in the Hosts-File-Editor
        /// <list type="bullet">
        /// <item>
        /// <description>Validating Adding-entry Button works correctly.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestAddingEntry()
        {
            this.CloseWarningDialog();
            this.RemoveAllEntries();

            Assert.IsTrue(this.FindAll<Button>("Delete").Count == 0, "Should have no row after removing all");

            this.AddEntry("192.168.0.1", "localhost", true);

            Assert.IsTrue(this.FindAll<Button>("Delete").Count == 1, "Should have one row now");
        }

        /// <summary>
        /// Test Multiple-hosts validation logic
        /// <list type="bullet">
        /// <item>
        /// <description>Validating the Add button should be Disabled if more than 9 hosts in one entry.</description>
        /// </item>
        /// <item>
        /// <description>Validating the Add button should be Enabled if less or equal 9 hosts in one entry.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestTooManyHosts()
        {
            this.CloseWarningDialog();

            // only at most 9 hosts allowed in one entry
            string validHosts = string.Join(" ", "host_1", "host_2", "host_3", "host_4", "host_5", "host_6", "host_7", "host_8", "host_9");

            // should not allow to add more than 9 hosts in one entry, hosts are separated by space
            string inValidHosts = validHosts + " more_host";

            this.Find<Button>("New entry").Click();

            Assert.IsFalse(this.Find<Button>("Add").Enabled, "Add button should be Disabled by default");

            this.Find<TextBox>("Address").SetText("127.0.0.1");

            this.Find<TextBox>("Hosts").SetText(validHosts);
            Assert.IsTrue(this.Find<Button>("Add").Enabled, "Add button should be Enabled with validHosts");

            this.Find<TextBox>("Hosts").SetText(inValidHosts);
            Assert.IsFalse(this.Find<Button>("Add").Enabled, "Add button should be Disabled with inValidHosts");

            this.Find<Button>("Cancel").Click();
        }

        /// <summary>
        /// Test Error-message in the Hosts-File-Editor
        /// <list type="bullet">
        /// <item>
        /// <description>Validating error message should be shown if not run as admin.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestErrorMessageWithNonAdminPermission()
        {
            this.CloseWarningDialog();
            this.RemoveAllEntries();

            // Add new URL override and a warning tip should be shown
            this.AddEntry("192.168.0.1", "localhost", true);

            Assert.IsTrue(
                this.FindAll<TextBlock>("The hosts file cannot be saved because the program isn't running as administrator.").Count == 1,
                "Should display host-file saving error if not run as administrator");
        }

        /// <summary>
        /// Test Filter-panel function in the Hosts-File-Editor
        /// <list type="bullet">
        /// <item>
        /// <description>Validating Address filter matching pattern: contains, endsWith, startsWith, exactly-match.</description>
        /// </item>
        /// <item>
        /// <description>Validating Hosts filter matching pattern: contains, endsWith, startsWith, exactly-match.</description>
        /// </item>
        /// <item>
        /// <description>Validating click Filters Button to open filter-panel, and click Filter Button again to close filter-panel.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod]
        public void TestFilterControl()
        {
            this.CloseWarningDialog();
            this.RemoveAllEntries();

            for (int i = 0; i < 10; i++)
            {
                this.AddEntry("192.168.0." + i, "localhost_" + i, true);
            }

            // Open-filter-panel
            this.Find<Button>("Filters").Click();
            Assert.IsTrue(this.FindAll<Button>("Clear filters").Count == 1, "Filter panel should be opened afer click Filter Button");

            var addressFilterCases = new KeyValuePair<string, int>[]
            {
                // contains text, expected matched more rows
                new("168.0", 10),

                // ends with text, expected matched 1 row
                new("168.0.1", 1),

                // starts with text, expected matched more rows
                new("192.168.", 10),

                // full text, expected matched 1 row
                new("192.168.0.1", 1),

                // no-matching text, expected matched no row
                new("127.0.0", 0),

                // empty filter, should display all rows
                new(string.Empty, 10),
            };

            foreach (var (addressFilter, expectedCount) in addressFilterCases)
            {
                this.Find<TextBox>("Address").SetText(addressFilter);
                Assert.IsTrue(this.Find("Entries").FindAll<Button>("Delete").Count == expectedCount);
            }

            var hostFilterCases = new KeyValuePair<string, int>[]
            {
                // contains text, expected matched more rows
                new("host_", 10),

                // ends with text, expected matched 1 row
                new("host_4", 1),

                // starts with text, expected matched more rows
                new("localhost", 10),

                // full text, expected matched 1 row
                new("localhost_5", 1),

                // empty filter, should display all rows
                new(string.Empty, 10),
            };

            foreach (var (hostFilterCase, expectedCount) in hostFilterCases)
            {
                this.Find<TextBox>("Hosts").SetText(hostFilterCase);
                Assert.IsTrue(this.Find("Entries").FindAll<Button>("Delete").Count == expectedCount);
            }

            // Close-filter-panel
            this.Find<Button>("Filters").Click();
            Assert.IsFalse(this.FindAll<Button>("Clear filters").Count == 1, "Filter panel should be closed afer click Filter Button");
        }

        private void AddEntry(string ip, string host, bool active = true, bool clickAddEntryButton = true)
        {
            if (clickAddEntryButton)
            {
                // Click 'Add an entry' for adding Host override rule
                this.Find<Button>("New entry").Click();
            }

            // Adding a new host override localhost -> 192.168.0.1
            Assert.IsFalse(this.Find<Button>("Add").Enabled, "Add button should be Disabled by default");

            Assert.IsTrue(this.Find<TextBox>("Address").SetText(ip).Text == ip);
            Assert.IsTrue(this.Find<TextBox>("Hosts").SetText(host).Text == host);

            this.Find<ToggleSwitch>("Active").Toggle(active);

            Assert.IsTrue(this.Find<Button>("Add").Enabled, "Add button should be Enabled after providing valid inputs");

            // Add the entry
            this.Find<Button>("Add").Click();

            // 0.5 second delay after adding an entry
            Task.Delay(500).Wait();
        }

        private void CloseWarningDialog()
        {
            // Find 'Accept' button which come in 'Warning' dialog
            if (this.FindAll("Warning").Count > 0 &&
                this.FindAll<Button>("Accept").Count > 0)
            {
                // Hide Warning dialog if any
                this.Find<Button>("Accept").Click();
            }
        }

        private void RemoveAllEntries()
        {
            // Delete all existing host-override rules
            foreach (var deleteBtn in this.FindAll<Button>("Delete"))
            {
                deleteBtn.Click();
                this.Find<Button>("Yes").Click();
            }

            // Should have no row left, and no more delete button
            Assert.IsTrue(this.FindAll<Button>("Delete").Count == 0);
        }
    }
}
