// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hosts.UITests
{
    [TestClass]
    public class HostModuleTests : UITestBase
    {
        private const string SaveErrorMessage = "The hosts file cannot be saved because the program isn't running as administrator.";

        // %WinDir%\System32\drivers\etc\hosts - same path HostsService computes.
        private static readonly string HostsFilePath = Path.Combine(
            System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows),
            "System32",
            "drivers",
            "etc",
            "hosts");

        private static IDisposable? hostsFileSnapshot;
        private static IDisposable? moduleSettingsSnapshot;

        public HostModuleTests()
            : base(PowerToysModule.Hosts, WindowSize.Small_Vertical)
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
        /// Test Empty-view in the Hosts-File-Editor
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
        [TestMethod("Hosts.Basic.EmptyViewShouldWork")]
        [TestCategory("Hosts File Editor #4")]
        public void TestEmptyView()
        {
            CloseWarningDialog();
            RemoveAllEntries();

            // 'Add an entry' link (only shown when the list is empty) should be visible.
            // .Next has no HyperlinkButton wrapper, and a WinUI HyperlinkButton reports UIA
            // ControlType=Hyperlink (not Button), so match it with the untyped Element instead.
            Assert.IsTrue(Session.HasOne<Element>(By.Name("Add an entry")), "'Add an entry' button should be visible in the empty view");
            VisualAssert.AreEqual(TestContext, Session.Find(By.AccessibilityId("Entries")), "EmptyView");

            // Click 'Add an entry' from empty-view for adding a Host override rule.
            Find<Element>("Add an entry").Click();

            AddEntry("192.168.0.1", "localhost", false, false);

            // Should have one row now and not more empty view.
            Assert.IsTrue(Session.Has<Button>(By.Name("Delete")), "Should have one row now");
            Assert.IsFalse(Session.Has<Element>(By.Name("Add an entry")), "'Add an entry' button should be invisible if not empty view");
            VisualAssert.AreEqual(TestContext, Session.Find(By.AccessibilityId("Entries")), "NonEmptyView");
        }

        /// <summary>
        /// Test Adding-entry Button in the Hosts-File-Editor
        /// <list type="bullet">
        /// <item>
        /// <description>Validating Adding-entry Button works correctly.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod("Hosts.Basic.AddEntryButtonShouldWork")]
        [TestCategory("Hosts File Editor #4")]
        public void TestAddingEntry()
        {
            CloseWarningDialog();
            RemoveAllEntries();

            Assert.IsFalse(Session.Has<Button>(By.Name("Delete")), "Should have no row after removing all");

            AddEntry("192.168.0.1", "localhost", true);

            Assert.IsTrue(Session.Has<Button>(By.Name("Delete")), "Should have one row now");
            VisualAssert.AreEqual(TestContext, Session.Find(By.AccessibilityId("Entries")));
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
        [TestMethod("Hosts.Basic.CanNotAddMoreThenNighHosts")]
        [TestCategory("Hosts File Editor #5")]
        public void TestTooManyHosts()
        {
            CloseWarningDialog();

            // Only at most 9 hosts allowed in one entry.
            string validHosts = string.Join(" ", "host_1", "host_2", "host_3", "host_4", "host_5", "host_6", "host_7", "host_8", "host_9");

            // Should not allow more than 9 hosts in one entry, hosts are separated by space.
            string inValidHosts = validHosts + " more_host";
            string splitHosts = validHosts + " host_10";

            Find<Button>("New entry").Click();

            Assert.IsFalse(FindExact<Button>("Add").IsEnabled, "Add button should be Disabled by default");

            Find<TextBox>("Address").SetText("127.0.0.1");

            Find<TextBox>("Hosts").SetText(validHosts);
            Assert.IsTrue(
                Session.WaitFor(() => FindExact<Button>("Add", 500).IsEnabled, 5_000),
                "Add button should be Enabled with validHosts");

            Find<TextBox>("Hosts").SetText(inValidHosts);
            Assert.IsTrue(
                Session.WaitFor(() => !FindExact<Button>("Add", 500).IsEnabled, 5_000),
                "Add button should be Disabled with inValidHosts");

            Find<Button>("Cancel").Click();

            // An elevated run also covers loading a manually authored overlong hosts line:
            // the editor must split it into a 9-host row plus a 1-host row and explain why.
            if (Session.IsElevated == true)
            {
                byte[]? originalHostsFile = File.Exists(HostsFilePath) ? File.ReadAllBytes(HostsFilePath) : null;
                int initialEntryCount = CountExact<Button>("Delete");
                try
                {
                    File.AppendAllText(
                        HostsFilePath,
                        $"{Environment.NewLine}127.0.0.1 {splitHosts}{Environment.NewLine}");

                    Assert.IsTrue(
                        Session.WaitFor(() => CountExact<Button>("Reload") == 1, 5_000),
                        "The editor did not detect the externally modified hosts file.");
                    FindExact<Button>("Reload").Click();

                    Assert.IsTrue(
                        Session.WaitFor(() => CountExact<Button>("Delete") == initialEntryCount + 2, 5_000),
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
        [TestMethod("Hosts.Basic.ErrorMessageShowupIfNotRunAsAdmin")]
        [TestCategory("Hosts File Editor #8")]
        public void TestErrorMessageWithNonAdminPermission()
        {
            CloseWarningDialog();
            RemoveAllEntries();
            AddEntry("192.168.0.1", "localhost", true);
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
        [TestMethod("Hosts.Basic.FiltersControlShouldWork")]
        [TestCategory("Hosts File Editor #6")]
        public void TestFilterControl()
        {
            CloseWarningDialog();
            RemoveAllEntries();

            for (int i = 0; i < 10; i++)
            {
                AddEntry("192.168.0." + i, "localhost_" + i, true);
            }

            // Open filter panel.
            FindExact<Button>("Filters").Click();
            Assert.IsTrue(
                Session.WaitFor(() => CountExact<Button>("Clear filters") == 1, 5_000),
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
                Find<TextBox>("Address").SetText(addressFilter);

                // All 'Delete' buttons in the window live inside the Entries list, so an unscoped
                // count is equivalent to a scoped one (.Next Element has no scoped FindAll).
                Assert.IsTrue(
                    Session.WaitFor(() => CountExact<Button>("Delete") == expectedCount, 5_000),
                    $"Address filter '{addressFilter}' did not reach {expectedCount} matching rows.");
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
                Find<TextBox>("Hosts").SetText(hostFilterCase);
                Assert.IsTrue(
                    Session.WaitFor(() => CountExact<Button>("Delete") == expectedCount, 5_000),
                    $"Hosts filter '{hostFilterCase}' did not reach {expectedCount} matching rows.");
            }

            // Close filter panel.
            FindExact<Button>("Filters").Click();
            Assert.IsTrue(
                Session.WaitFor(() => CountExact<Button>("Clear filters") == 0, 5_000),
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
        /// when the current session is elevated (mirrors <see cref="TestNoErrorMessageWithNonAdminPermission"/>).
        /// </summary>
        [TestMethod("Hosts.Basic.EntryTogglesAreAppliedToHostsFile")]
        [TestCategory("Hosts File Editor #2")]
        [TestCategory("Hosts File Editor #3")]
        public void TestEntryTogglesAreAppliedToHostsFile()
        {
            CloseWarningDialog();
            RemoveAllEntries();

            const string address = "192.168.123.123";
            const string host = "powertoys.uitest.next";

            AddEntry(address, host, true);
            var activeToggle = Find<ToggleSwitch>("Active");
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
        [TestMethod("Hosts.Basic.OpenHostsFileButtonShouldOpenNotepad")]
        [TestCategory("Hosts File Editor #7")]
        public void TestOpenHostsFileButtonOpensNotepad()
        {
            CloseWarningDialog();
            var existingProcessIds = Process.GetProcessesByName("notepad")
                .Select(process => process.Id)
                .ToHashSet();

            try
            {
                Find<Button>("Open hosts file").Click();

                var notepad = WindowsFinder.WaitForWindowByApp(
                    "notepad",
                    candidate =>
                        !existingProcessIds.Contains(candidate.ProcessId) &&
                        candidate.Title.Contains("hosts", StringComparison.OrdinalIgnoreCase),
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
                if (line.Contains(needle))
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
                ? Session.WaitFor(() => CountExact<TextBlock>(SaveErrorMessage) == 1, 5_000)
                : CountExact<TextBlock>(SaveErrorMessage) > 0;
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
                Find<Button>("New entry").Click();
            }

            // Adding a new host override localhost -> 192.168.0.1
            var addButton = FindExact<Button>("Add");
            Assert.IsFalse(addButton.IsEnabled, "Add button should be Disabled by default");

            Assert.AreEqual(ip, Find<TextBox>("Address").SetText(ip).Value);
            Assert.AreEqual(host, Find<TextBox>("Hosts").SetText(host).Value);

            Find<ToggleSwitch>("Active").Toggle(active);

            Assert.IsTrue(
                Session.WaitFor(() => FindExact<Button>("Add", 500).IsEnabled, 5_000),
                "Add button should be Enabled after providing valid inputs");

            // Add the entry.
            FindExact<Button>("Add").Click();

            Assert.IsTrue(
                Session.WaitFor(() => CountExact<Button>("Delete") > 0, 5_000),
                "The new entry did not appear after clicking Add");
        }

        private T FindExact<T>(string name, int timeoutMS = 5_000)
            where T : Element, new()
        {
            T? result = null;
            bool found = Session.WaitFor(
                () =>
                {
                    var matches = Session.FindAll<T>(By.Name(name), 0)
                        .Where(element => string.Equals(element.Name, name, System.StringComparison.Ordinal))
                        .ToList();
                    Assert.IsTrue(matches.Count <= 1, $"Expected at most one exact {typeof(T).Name} named '{name}', found {matches.Count}.");
                    result = matches.SingleOrDefault();
                    return result is not null;
                },
                timeoutMS);

            Assert.IsTrue(found, $"Exact {typeof(T).Name} named '{name}' was not found within {timeoutMS} ms.");
            return result!;
        }

        private int CountExact<T>(string name)
            where T : Element, new() =>
            Session.FindAll<T>(By.Name(name), 0)
                .Count(element => string.Equals(element.Name, name, System.StringComparison.Ordinal));

        private void CloseWarningDialog()
        {
            // Find 'Accept' button which comes up in the 'Warning' dialog.
            if (Session.FindAll<Element>(By.Name("Warning"), 1000).Count > 0 &&
                Session.FindAll<Button>(By.Name("Accept"), 1000).Count > 0)
            {
                // Hide Warning dialog if any.
                Session.Find<Button>(By.Name("Accept"), 1000).Click();
            }
        }

        private void RemoveAllEntries()
        {
            while (true)
            {
                int countBefore = CountExact<Button>("Delete");
                if (countBefore == 0)
                {
                    return;
                }

                var deleteButton = Session.FindAll<Button>(By.Name("Delete"), 1_000)
                    .First(button => string.Equals(button.Name, "Delete", StringComparison.Ordinal));
                deleteButton.Click();

                FindExact<Button>("Yes").Click();
                Assert.IsTrue(
                    Session.WaitFor(() => CountExact<Button>("Delete") < countBefore, 5_000),
                    "The entry count did not decrease after confirming deletion.");
            }
        }
    }
}
