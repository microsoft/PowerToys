// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hosts.UITests
{
    [TestClass]
    public class HostModuleTests : UITestBase
    {
        // %WinDir%\System32\drivers\etc\hosts - same path HostsService computes.
        private static readonly string HostsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "drivers",
            "etc",
            "hosts");

        // ClassCleanup restores the real hosts file after the module has stopped. A forced test-host
        // termination can bypass managed cleanup, so pipeline/VM isolation remains the final safeguard.
        private static IDisposable? hostsFileSnapshot;
        private static IDisposable? moduleSettingsSnapshot;

        public HostModuleTests()
            : base(PowerToysModule.Hosts)
        {
        }

        protected override IReadOnlyList<string> StaleProcessNames { get; } =
        [
            "PowerToys",
            "PowerToys.Settings",
            "PowerToys.FancyZonesEditor",
            "PowerToys.Hosts",
            "notepad",
        ];

        [ClassInitialize]
        public static void PreserveHostsFile(TestContext testContext)
        {
            _ = testContext;
            moduleSettingsSnapshot = HostsTestHelper.PreserveSettingsAndDisableBackups();
            if (ElevationHelper.IsCurrentProcessElevated())
            {
                hostsFileSnapshot = SettingsConfigHelper.PreserveFile(HostsFilePath);
            }
        }

        [ClassCleanup]
        public static void RestoreHostsFile()
        {
            hostsFileSnapshot?.Dispose();
            hostsFileSnapshot = null;
            moduleSettingsSnapshot?.Dispose();
            moduleSettingsSnapshot = null;
        }

        /// <summary>
        /// Test entry buttons and empty/non-empty states in the Hosts File Editor.
        /// <list type="bullet">
        /// <item>
        /// <description>Validating Empty-view is shown if no entries in the list.</description>
        /// </item>
        /// <item>
        /// <description>Validating Empty-view is NOT shown if 1 or more entries in the list.</description>
        /// </item>
        /// <item>
        /// <description>Validating Add-an-entry Button in Empty-view works correctly.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod("Hosts.Next.Basic.EntryButtonsAndEmptyViewShouldWork")]
        [TestCategory("Hosts File Editor #4")]
        public void TestEntryButtonsAndEmptyView()
        {
            CloseWarningDialog();
            RemoveAllEntries();

            Assert.IsTrue(Session.Find(By.AccessibilityId("Entries")).Displayed, "The entries list should be visible.");
            Assert.IsTrue(Session.FindExact<Button>("New entry").Displayed, "The toolbar New entry button should be visible.");

            // 'Add an entry' link is shown only when the list is empty.
            // .Next has no HyperlinkButton wrapper, and a WinUI HyperlinkButton reports UIA
            // ControlType=Hyperlink (not Button), so match it with the untyped Element instead.
            Assert.IsTrue(Session.FindExact<Element>("Add an entry").Displayed, "'Add an entry' should be visible in the empty view.");

            // Add through the empty-view link and verify the non-empty state.
            Session.FindExact<Element>("Add an entry").Invoke(msPostAction: 0);
            Assert.IsTrue(
                Session.WaitForExactCount<Button>("Add", 1, 10_000),
                "The Add entry dialog did not open after invoking the empty-view link.");

            AddEntry("192.168.0.1", "localhost.powertoys.uitest", false, false);

            // Should have one row now and not more empty view.
            Assert.IsTrue(Session.FindExact<Button>("Delete").Displayed, "The added row should expose a Delete button.");
            Assert.AreEqual(0, Session.CountExact<Element>("Add an entry"), "'Add an entry' should be hidden in the non-empty view.");

            // Return to the empty state, then verify the toolbar New entry path too.
            RemoveAllEntries();
            Assert.AreEqual(0, Session.CountExact<Button>("Delete"), "No rows should remain after removal.");
            Assert.IsTrue(Session.FindExact<Element>("Add an entry").Displayed, "The empty-view link should return after removing all rows.");

            AddEntry("192.168.0.2", "toolbar.powertoys.uitest", true);
            Assert.IsTrue(Session.FindExact<Button>("Delete").Displayed, "The toolbar New entry button should add a row.");
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
        [TestMethod("Hosts.Next.Basic.CanNotAddMoreThenNighHosts")]
        [TestCategory("Hosts File Editor #5")]
        public void TestTooManyHosts()
        {
            CloseWarningDialog();

            // Only at most 9 hosts allowed in one entry.
            string validHosts = string.Join(" ", "host_1", "host_2", "host_3", "host_4", "host_5", "host_6", "host_7", "host_8", "host_9");

            // Should not allow more than 9 hosts in one entry, hosts are separated by space.
            string inValidHosts = validHosts + " more_host";
            string splitHosts = validHosts + " host_10";

            Session.FindExact<Button>("New entry").Click();

            Assert.IsFalse(Session.FindExact<Button>("Add").IsEnabled, "Add button should be Disabled by default");

            Session.FindExact<TextBox>("Address").SetText("127.0.0.1");

            Session.FindExact<TextBox>("Hosts").SetText(validHosts);
            Assert.IsTrue(
                Session.WaitFor(() => Session.FindExact<Button>("Add", 500).IsEnabled, 5_000, pollIntervalMS: 250),
                "Add button should be Enabled with validHosts");

            Session.FindExact<TextBox>("Hosts").SetText(inValidHosts);
            Assert.IsTrue(
                Session.WaitFor(() => !Session.FindExact<Button>("Add", 500).IsEnabled, 5_000, pollIntervalMS: 250),
                "Add button should be Disabled with inValidHosts");

            Session.FindExact<Button>("Cancel").Click();

            // An elevated run also covers loading a manually authored overlong hosts line:
            // the editor must split it into a 9-host row plus a 1-host row and explain why.
            if (Session.IsElevated == true)
            {
                byte[]? originalHostsFile = File.Exists(HostsFilePath) ? File.ReadAllBytes(HostsFilePath) : null;
                int initialEntryCount = Session.CountExact<Button>("Delete");
                try
                {
                    File.AppendAllText(
                        HostsFilePath,
                        $"{Environment.NewLine}127.0.0.1 {splitHosts}{Environment.NewLine}");

                    Assert.IsTrue(
                        Session.WaitForExactCount<Button>("Reload", 1),
                        "The editor did not detect the externally modified hosts file.");
                    Session.FindExact<Button>("Reload").Click();

                    Assert.IsTrue(
                        Session.WaitForExactCount<Button>("Delete", initialEntryCount + 2),
                        "The overlong hosts line was not split into exactly two entries.");
                    Assert.IsTrue(Session.Has(By.Name(validHosts), 5_000), "The first split entry should contain hosts 1 through 9.");
                    Assert.IsTrue(Session.Has(By.Name("host_10"), 5_000), "The second split entry should contain host 10.");
                    Assert.IsTrue(
                        Session.Has(By.Name("Entries contain too many hosts"), 5_000),
                        "The split-entry teaching tip should be shown.");
                }
                finally
                {
                    if (originalHostsFile is null)
                    {
                        File.Delete(HostsFilePath);
                    }
                    else
                    {
                        File.WriteAllBytes(HostsFilePath, originalHostsFile);
                    }
                }
            }
        }

        /// <summary>
        /// Test Error-message in the Hosts-File-Editor
        /// <list type="bullet">
        /// <item>
        /// <description>Validating error message should be shown if not run as admin.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod("Hosts.Next.Basic.ErrorMessageShowupIfNotRunAsAdmin")]
        [TestCategory("Hosts File Editor #8")]
        public void TestErrorMessageWithNonAdminPermission()
        {
            CloseWarningDialog();
            RemoveAllEntries();
            AddEntry("192.168.0.1", "save-error.powertoys.uitest", true);
            AssertSaveFeedbackMatchesElevation();
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
        [TestMethod("Hosts.Next.Basic.FiltersControlShouldWork")]
        [TestCategory("Hosts File Editor #6")]
        public void TestFilterControl()
        {
            CloseWarningDialog();
            RemoveAllEntries();

            for (int i = 0; i < 10; i++)
            {
                AddEntry("192.168.0." + i, "host_" + i + ".powertoys.uitest", true);
            }

            // Open filter panel.
            Session.FindExact<Button>("Filters").Click();
            Assert.IsTrue(
                Session.WaitForExactCount<Button>("Clear filters", 1),
                "Filter panel should be opened after clicking the Filters button.");

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
                Session.FindExact<TextBox>("Address").SetText(addressFilter);

                // All 'Delete' buttons in the window live inside the Entries list, so an unscoped
                // count is equivalent to a scoped one (.Next Element has no scoped FindAll).
                Assert.IsTrue(
                    Session.WaitForExactCount<Button>("Delete", expectedCount),
                    $"Address filter '{addressFilter}' did not reach {expectedCount} matching rows.");
            }

            var hostFilterCases = new KeyValuePair<string, int>[]
            {
                // contains text, expected matched more rows
                new("powertoys", 10),

                // ends with text, expected matched 1 row
                new("4.powertoys.uitest", 1),

                // starts with text, expected matched more rows
                new("host_", 10),

                // full text, expected matched 1 row
                new("host_5.powertoys.uitest", 1),

                // empty filter, should display all rows
                new(string.Empty, 10),
            };

            foreach (var (hostFilterCase, expectedCount) in hostFilterCases)
            {
                Session.FindExact<TextBox>("Hosts").SetText(hostFilterCase);
                Assert.IsTrue(
                    Session.WaitForExactCount<Button>("Delete", expectedCount),
                    $"Hosts filter '{hostFilterCase}' did not reach {expectedCount} matching rows.");
            }

            // Close filter panel.
            Session.FindExact<Button>("Filters").Click();
            Assert.IsTrue(
                Session.WaitForExactCount<Button>("Clear filters", 0),
                "Filter panel should be closed after clicking the Filters button.");
        }

        /// <summary>
        /// Covers Release-checklist items:
        /// <list type="bullet">
        /// <item>
        /// <description>#2 - Open the hosts file in a text editor that auto-refreshes so changes applied
        /// by the editor can be seen in real time - i.e. writes actually land in the hosts file.</description>
        /// </item>
        /// <item>
        /// <description>#3 - Enable and disable lines and verify they are applied to the file.</description>
        /// </item>
        /// </list>
        /// Writing the hosts file requires the module to run elevated, so the file assertions only run
        /// when the current session is elevated (mirrors <see cref="HostsSettingTests.TestOpenAsAdministrator"/>).
        /// </summary>
        [TestMethod("Hosts.Next.Basic.EntryTogglesAreAppliedToHostsFile")]
        [TestCategory("Hosts File Editor #2")]
        [TestCategory("Hosts File Editor #3")]
        public void TestEntryTogglesAreAppliedToHostsFile()
        {
            CloseWarningDialog();
            RemoveAllEntries();

            const string address = "192.168.123.123";
            const string host = "powertoys.uitest.next";

            AddEntry(address, host, true);
            var activeToggle = Session.FindExact<ToggleSwitch>("Active");
            Assert.IsTrue(activeToggle.IsOn, "A newly added active entry should be shown as active.");

            if (Session.IsElevated == true)
            {
                Assert.IsTrue(
                    WaitForHostsLine(host, line => line.Contains(address, StringComparison.Ordinal) && !line.TrimStart().StartsWith('#'), 5_000),
                    $"Expected an active line containing '{address} {host}' in the hosts file.");
            }
            else
            {
                AssertSaveFeedbackMatchesElevation();
            }

            // Disable the entry via the row's own 'Active' toggle (no need to re-open the edit dialog).
            activeToggle = Session.FindExact<ToggleSwitch>("Active");
            activeToggle.Toggle(false);
            Assert.IsTrue(
                activeToggle.WaitForProperty("ToggleState", "Off", 5_000),
                "The entry toggle did not reach the Off state.");

            if (Session.IsElevated == true)
            {
                Assert.IsTrue(
                    WaitForHostsLine(host, line => line.TrimStart().StartsWith('#'), 5_000),
                    $"Expected a commented line containing '{host}' after disabling the entry.");
            }

            // Re-enable the entry and verify the file is updated again.
            activeToggle = Session.FindExact<ToggleSwitch>("Active");
            activeToggle.Toggle(true);
            Assert.IsTrue(
                activeToggle.WaitForProperty("ToggleState", "On", 5_000),
                "The entry toggle did not return to the On state.");

            if (Session.IsElevated == true)
            {
                Assert.IsTrue(
                    WaitForHostsLine(host, line => !line.TrimStart().StartsWith('#'), 5_000),
                    $"Expected an uncommented line containing '{host}' after re-enabling the entry.");
            }
        }

        /// <summary>
        /// Covers Release-checklist item #7 - Click the "Open hosts file" button and verify it opens in
        /// the default editor (Notepad).
        /// </summary>
        [TestMethod("Hosts.Next.Basic.OpenHostsFileButtonShouldOpenNotepad")]
        [TestCategory("Hosts File Editor #7")]
        public void TestOpenHostsFileButtonOpensNotepad()
        {
            CloseWarningDialog();
            Assert.IsTrue(
                WindowControl.TryKillProcessTreeByNameAndWait("notepad", 10_000),
                "A stale Notepad process could not be stopped before validating the hosts document launch.");

            try
            {
                Session.FindExact<Button>("Open hosts file").Click();

                var notepad = WindowsFinder.WaitForWindowByApp(
                    "notepad",
                    candidate => candidate.Title.Contains("hosts", StringComparison.OrdinalIgnoreCase),
                    timeoutMS: 30_000);
                Assert.IsNotNull(
                    notepad,
                    $"Notepad did not open the expected hosts document from '{HostsFilePath}'.");
            }
            finally
            {
                WindowControl.TryKillProcessTreeByNameAndWait("notepad", 10_000);
            }
        }

        private static string? ReadHostsFileLineContaining(string needle)
        {
            if (!File.Exists(HostsFilePath))
            {
                return null;
            }

            foreach (var line in File.ReadAllLines(HostsFilePath))
            {
                if (line.Contains(needle, StringComparison.Ordinal))
                {
                    return line;
                }
            }

            return null;
        }

        private static bool WaitForHostsLine(string needle, Func<string, bool> predicate, int timeoutMS)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
            while (DateTime.UtcNow < deadline)
            {
                var line = ReadHostsFileLineContaining(needle);
                if (line is not null && predicate(line))
                {
                    return true;
                }

                Thread.Sleep(200);
            }

            return false;
        }

        private void AssertSaveFeedbackMatchesElevation()
        {
            bool shouldShowError = Session.IsElevated != true;
            bool errorShown = shouldShowError
                ? Session.WaitForExactCount<TextBlock>(HostsTestHelper.SaveErrorMessage, 1)
                : Session.CountExact<TextBlock>(HostsTestHelper.SaveErrorMessage) > 0;
            string failureMessage = shouldShowError
                ? "A non-elevated Hosts process should show the save error."
                : "An elevated Hosts process should save without showing the non-admin error.";

            Assert.AreEqual(shouldShowError, errorShown, failureMessage);
        }

        private void AddEntry(string ip, string host, bool active = true, bool clickAddEntryButton = true)
        {
            if (clickAddEntryButton)
            {
                // Click 'New entry' for adding a Host override rule.
                Session.FindExact<Button>("New entry").Click();
            }

            // Add a new host override.
            var addButton = Session.FindExact<Button>("Add");
            Assert.IsFalse(addButton.IsEnabled, "Add button should be Disabled by default");

            Assert.AreEqual(ip, Session.FindExact<TextBox>("Address").SetText(ip).Value);
            Assert.AreEqual(host, Session.FindExact<TextBox>("Hosts").SetText(host).Value);

            HostsTestHelper.FindEntryDialogActiveToggle(Session).Toggle(active);

            Assert.IsTrue(
                Session.WaitFor(() => Session.FindExact<Button>("Add", 500).IsEnabled, 5_000, pollIntervalMS: 250),
                "Add button should be Enabled after providing valid inputs");

            // Add the entry.
            Session.FindExact<Button>("Add").Click();

            Assert.IsTrue(
                Session.WaitFor(() => Session.CountExact<Button>("Delete") > 0, 5_000, pollIntervalMS: 250),
                "The new entry did not appear after clicking Add");
        }

        private void CloseWarningDialog()
        {
            if (Session.WaitForExactCount<Button>("Accept", 1, 1_000))
            {
                Session.FindExact<Button>("Accept").Invoke(msPostAction: 0);
                Assert.IsTrue(
                    Session.WaitForExactCount<Button>("Accept", 0, 10_000),
                    "The startup warning did not close after invoking Accept.");
            }
        }

        private void RemoveAllEntries()
        {
            while (true)
            {
                int countBefore = Session.CountExact<Button>("Delete");
                if (countBefore == 0)
                {
                    return;
                }

                var deleteButton = Session.FindAll<Button>(By.Name("Delete"), 1_000)
                    .First(button => string.Equals(button.Name, "Delete", StringComparison.Ordinal));
                deleteButton.Focus();

                // Focusing selects the row and rebuilds its automation subtree, so invoke a freshly
                // resolved button rather than the now-stale element used for Focus().
                Session.FindAll<Button>(By.Name("Delete"), 1_000)
                    .First(button => string.Equals(button.Name, "Delete", StringComparison.Ordinal))
                    .Invoke(msPostAction: 0);

                Session.FindExact<Button>("Yes").Invoke(msPostAction: 0);
                Assert.IsTrue(
                    Session.WaitFor(() => Session.CountExact<Button>("Delete") < countBefore, 5_000, pollIntervalMS: 250),
                    "The entry count did not decrease after confirming deletion.");
            }
        }
    }
}
