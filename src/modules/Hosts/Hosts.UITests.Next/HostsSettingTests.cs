// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest.Next;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Hosts.UITests
{
    [TestClass]
    public class HostsSettingTests : UITestBase
    {
        // %WinDir%\System32\drivers\etc\hosts - same path HostsService computes.
        private static readonly string HostsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Windows),
            "System32",
            "drivers",
            "etc",
            "hosts");

        private static readonly string[] EnabledModules = ["Hosts"];

        // ClassCleanup restores the real hosts file after the module has stopped. A forced test-host
        // termination can bypass managed cleanup, so pipeline/VM isolation remains the final safeguard.
        private static IDisposable? hostsFileSnapshot;
        private static IDisposable? moduleSettingsSnapshot;

        public HostsSettingTests()
        : base(PowerToysModule.PowerToysSettings, enableModules: EnabledModules)
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

        protected override bool ReuseScopeAcrossTests => true;

        [ClassInitialize]
        public static void PreserveHostsState(TestContext testContext)
        {
            _ = testContext;
            moduleSettingsSnapshot = HostsTestHelper.PreserveSettingsAndDisableBackups();
            if (ElevationHelper.IsCurrentProcessElevated())
            {
                hostsFileSnapshot = SettingsConfigHelper.PreserveFile(HostsFilePath);
            }
        }

        [ClassCleanup]
        public static void RestoreHostsState()
        {
            hostsFileSnapshot?.Dispose();
            hostsFileSnapshot = null;
            moduleSettingsSnapshot?.Dispose();
            moduleSettingsSnapshot = null;
        }

        [TestCleanup]
        public async Task CleanupSpawnedWindows()
        {
            await CaptureFailureArtifactsBeforeCleanupAsync(TimeSpan.FromSeconds(2));
            WindowControl.TryKillProcessTreeByNameAndWait("PowerToys.Hosts", 10_000);
            WindowControl.TryCloseByApp("notepad");
        }

        /// <summary>
        /// Test Warning Dialog at startup
        /// <list type="bullet">
        /// <item>
        /// <description>Validating Warning-Dialog will be shown if 'Show a warning at startup' toggle is On.</description>
        /// </item>
        /// <item>
        /// <description>Validating Warning-Dialog will NOT be shown if 'Show a warning at startup' toggle is Off.</description>
        /// </item>
        /// <item>
        /// <description>Validating click 'Quit' button in Warning-Dialog, the Hosts File Editor window would be closed.</description>
        /// </item>
        /// <item>
        /// <description>Validating click 'Accept' button in Warning-Dialog, the Hosts File Editor window would NOT be closed.</description>
        /// </item>
        /// </list>
        /// </summary>
        [TestMethod("Hosts.Next.Settings.ShowWarningDialogIfRunAsAdmin")]
        [TestCategory("Hosts File Editor #1")]
        [TestCategory("Hosts File Editor #9")]
        public void TestWarningDialog()
        {
            var hostsSession = LaunchFromSetting(showWarning: true);
            var settingsSession = Session.FromProcess("PowerToys.Settings", PowerToysModule.PowerToysSettings);

            // Validating Warning-Dialog will be shown if 'Show a warning at startup' toggle is on.
            Assert.IsTrue(
                hostsSession.WaitForExactCount<Button>("Accept", 1, 10_000),
                "Should show warning dialog");

            // Quit Hosts File Editor.
            hostsSession.FindExact<Button>("Quit").Invoke(msPostAction: 0);

            // Validating click 'Quit' button in Warning-Dialog, the Hosts File Editor window would be closed.
            Assert.IsTrue(
                settingsSession.WaitFor(IsHostsFileEditorClosed, 15_000, pollIntervalMS: 250),
                "Hosts File Editor should be closed after click Quit button in Warning Dialog");

            // Re-launch Hosts File Editor from Settings.
            settingsSession.FindExact<Button>("Open Hosts File Editor").Click();
            hostsSession = AttachHostsEditor();

            // Should show warning dialog.
            Assert.IsTrue(
                hostsSession.WaitForExactCount<Button>("Accept", 1, 10_000),
                "Should show warning dialog");

            // Accept the warning this time.
            hostsSession.FindExact<Button>("Accept").Invoke(msPostAction: 0);
            Assert.IsTrue(
                hostsSession.WaitForExactCount<Button>("Accept", 0, 10_000),
                "The startup warning did not close after invoking Accept.");

            // Validating click 'Accept' button in Warning-Dialog, the Hosts File Editor window would NOT be closed.
            Assert.IsFalse(IsHostsFileEditorClosed(), "Hosts File Editor should NOT be closed after click Accept button in Warning Dialog");

            // Close Hosts File Editor window.
            WindowControl.TryCloseByApp("PowerToys.Hosts");

            hostsSession = LaunchFromSetting(showWarning: false);

            // Should NOT show warning dialog.
            Assert.IsFalse(
                hostsSession.WaitForExactCount<Button>("Accept", 1, 3_000),
                "Should NOT show warning dialog");

            // Host Editor Window should not be closed.
            Assert.IsFalse(IsHostsFileEditorClosed(), "Hosts File Editor should NOT be closed");

            // Close Hosts File Editor window.
            WindowControl.TryCloseByApp("PowerToys.Hosts");
        }

        /// <summary>
        /// Verifies the Open as administrator setting and, on an elevated test agent, confirms that
        /// the launched editor can save without the non-admin error.
        /// </summary>
        [TestMethod("Hosts.Next.Settings.OpenAsAdministratorSignalsAdminEvent")]
        [TestCategory("Hosts File Editor #8")]
        public void TestOpenAsAdministrator()
        {
            if (!ElevationHelper.IsCurrentProcessElevated())
            {
                // Keep a Hosts process alive: the admin event only launches with the "runas" verb
                // when no process exists, which would raise a UAC prompt the test cannot dismiss.
                var baselineHostsSession = LaunchFromSetting(showWarning: false, openAsAdmin: false);
                Assert.AreEqual(false, baselineHostsSession.IsElevated, "The standard-user baseline Hosts process should not be elevated.");

                var settingsSession = NavigateToHostsSettings();
                var openAsAdminToggle = settingsSession.FindExact<ToggleSwitch>("Open as administrator");
                openAsAdminToggle.Toggle(true);
                try
                {
                    Assert.IsTrue(
                        openAsAdminToggle.WaitForProperty("ToggleState", "On", 5_000),
                        "Open as administrator did not turn on.");

                    string moduleLogPath = GetLatestModuleInterfaceLogPath();
                    long originalLogLength = new FileInfo(moduleLogPath).Length;
                    Assert.IsTrue(
                        WindowsFinder.ListByApp("PowerToys.Hosts").Count > 0,
                        "Baseline Hosts process must still be running; otherwise Open as administrator raises a UAC prompt that blocks CI.");
                    settingsSession.FindExact<Button>("Open Hosts File Editor").Click();
                    Assert.IsTrue(
                        WaitForLogTextAfter(
                            moduleLogPath,
                            originalLogLength,
                            "Hosts-ShowHostsAdminEvent",
                            5_000),
                        "Opening Hosts with the administrator setting enabled did not signal the admin show event.");
                }
                finally
                {
                    var currentToggle = settingsSession.FindExact<ToggleSwitch>("Open as administrator");
                    currentToggle.Toggle(false);
                    Assert.IsTrue(
                        currentToggle.WaitForProperty("ToggleState", "Off", 5_000),
                        "Open as administrator did not return to its safe local-test state.");
                }

                return;
            }

            const string host = "hosts-admin-launch.uitest";
            var elevatedSettingsSession = NavigateToHostsSettings();
            var elevatedOpenAsAdminToggle = elevatedSettingsSession.FindExact<ToggleSwitch>("Open as administrator");
            Assert.IsFalse(
                elevatedOpenAsAdminToggle.IsEnabled,
                "Open as administrator should be disabled when PowerToys is already elevated.");

            var hostsSession = LaunchFromSetting(showWarning: false, openAsAdmin: false);
            Assert.AreEqual(true, hostsSession.IsElevated, "Hosts launched from elevated PowerToys should remain elevated.");

            CloseWarningDialogOn(hostsSession);
            RemoveAllEntriesOn(hostsSession);
            AddEntryOn(hostsSession, "192.168.123.124", host);

            Assert.IsTrue(
                WaitForHostsFileLine(host, 5_000),
                "The elevated Hosts process did not save the new entry to the hosts file.");
            Assert.AreEqual(
                0,
                hostsSession.CountExact<TextBlock>(HostsTestHelper.SaveErrorMessage),
                "The elevated Hosts process should not show the non-admin save error.");
        }

        private static string GetLatestModuleInterfaceLogPath()
        {
            string logsRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Microsoft",
                "PowerToys",
                "Hosts",
                "ModuleInterface",
                "Logs");
            string? logPath = Directory.Exists(logsRoot)
                ? Directory.EnumerateFiles(logsRoot, "*.log", SearchOption.AllDirectories)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault()
                : null;

            Assert.IsFalse(string.IsNullOrEmpty(logPath), $"No Hosts module-interface log was found under '{logsRoot}'.");
            return logPath!;
        }

        private static bool WaitForLogTextAfter(string logPath, long startOffset, string expectedText, int timeoutMS)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    using var stream = new FileStream(logPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                    if (stream.Length > startOffset)
                    {
                        stream.Position = startOffset;
                        using var reader = new StreamReader(stream);
                        if (reader.ReadToEnd().Contains(expectedText, StringComparison.Ordinal))
                        {
                            return true;
                        }
                    }
                }
                catch (IOException)
                {
                }

                Thread.Sleep(200);
            }

            return false;
        }

        /// <summary>
        /// Covers Release-checklist item #10 - Additional lines position.
        /// <list type="bullet">
        /// <item>
        /// <description>Validating the 'Placement of additional content' setting can be changed between Top and Bottom.</description>
        /// </item>
        /// <item>
        /// <description>Validating the additional content is actually written at the configured position in the hosts file.</description>
        /// </item>
        /// </list>
        /// Writing the hosts file requires the module to run elevated; the file-position assertions only run
        /// when the current session is elevated (mirrors <see cref="TestOpenAsAdministrator"/>).
        /// </summary>
        [TestMethod("Hosts.Next.Settings.AdditionalLinesPositionShouldBeApplied")]
        [TestCategory("Hosts File Editor #10")]
        public void TestAdditionalLinesPosition()
        {
            const string marker = "# powertoys-uitest-additional-content-marker";
            const string address = "10.10.10.10";
            const string host = "additional.lines.position.test";

            Session hostsSession = LaunchFromSetting(showWarning: false);
            string initialPosition = GetAdditionalLinesPosition();
            try
            {
                CloseWarningDialogOn(hostsSession);
                RemoveAllEntriesOn(hostsSession);
                AddEntryOn(hostsSession, address, host);

                // 1. Position = Bottom: the additional content should end up AFTER the entry line.
                SetAdditionalLinesPosition("Bottom");

                if (hostsSession.IsElevated == true)
                {
                    ApplyAdditionalContentAndAssertOrder(hostsSession, host, marker, markerShouldBeFirst: false);
                }
                else
                {
                    SetAdditionalContent(hostsSession, marker);
                    Assert.IsTrue(
                        hostsSession.WaitForExactCount<TextBlock>(HostsTestHelper.SaveErrorMessage, 1),
                        "A non-elevated Hosts process should report that additional content could not be saved.");
                }

                // 2. Position = Top: the additional content should end up BEFORE the entry line.
                SetAdditionalLinesPosition("Top");

                if (hostsSession.IsElevated == true)
                {
                    ApplyAdditionalContentAndAssertOrder(hostsSession, host, marker, markerShouldBeFirst: true);
                }
                else
                {
                    SetAdditionalContent(hostsSession, marker);
                }
            }
            finally
            {
                try
                {
                    SetAdditionalContent(hostsSession, string.Empty);
                    SetAdditionalLinesPosition(initialPosition);
                }
                catch (Exception ex)
                {
                    // Best-effort restoration must not replace the test's primary failure.
                    TestContext.WriteLine($"Failed to restore additional-content settings: {ex.Message}");
                }

                WindowControl.TryCloseByApp("PowerToys.Hosts");
            }
        }

        /// <summary>Change the 'Placement of additional content' combo box on the Hosts Settings page.</summary>
        private void SetAdditionalLinesPosition(string position)
        {
            // The dropdown items live in a popup surfaced by the Settings process as a separate
            // window, so search it with a process-scoped session (see ComboBox remarks).
            var settingsProcess = Session.FromProcess("PowerToys.Settings", PowerToysModule.PowerToysSettings);
            settingsProcess.FindExact<ComboBox>("Placement of additional content").Select(position);
            Assert.IsTrue(
                settingsProcess.WaitFor(
                    () => string.Equals(GetAdditionalLinesPosition(), position, StringComparison.Ordinal),
                    5_000,
                    pollIntervalMS: 250),
                $"Additional content position did not change to '{position}'.");
        }

        private static string GetAdditionalLinesPosition()
        {
            var settingsProcess = Session.FromProcess("PowerToys.Settings", PowerToysModule.PowerToysSettings);
            return settingsProcess.FindExact<ComboBox>("Placement of additional content").SelectedText;
        }

        private static void SetAdditionalContent(Session hostsSession, string content)
        {
            hostsSession.FindExact<Button>("Additional content").Click();
            hostsSession.Find<TextBox>(By.AccessibilityId("AdditionalLines")).SetText(content);
            hostsSession.FindExact<Button>("Save").Click();
            Assert.IsTrue(
                hostsSession.WaitForExactCount<Button>("Save", 0),
                "The Additional content dialog did not close after saving.");
        }

        private static void AddEntryOn(Session hostsSession, string ip, string host)
        {
            hostsSession.FindExact<Button>("New entry").Click();
            hostsSession.FindExact<TextBox>("Address").SetText(ip);
            hostsSession.FindExact<TextBox>("Hosts").SetText(host);
            HostsTestHelper.FindEntryDialogActiveToggle(hostsSession).Toggle(true);
            Assert.IsTrue(
                hostsSession.WaitFor(() => hostsSession.FindExact<Button>("Add", 500).IsEnabled, 5_000, pollIntervalMS: 250),
                "Add button did not become enabled after entering a valid host.");
            hostsSession.FindExact<Button>("Add").Click();
            Assert.IsTrue(
                hostsSession.WaitFor(() => hostsSession.CountExact<Button>("Delete") > 0, 5_000, pollIntervalMS: 250),
                "The new entry did not appear after clicking Add.");
        }

        private static void CloseWarningDialogOn(Session hostsSession)
        {
            if (hostsSession.CountExact<Button>("Accept") == 1)
            {
                hostsSession.FindExact<Button>("Accept").Invoke(msPostAction: 0);
                Assert.IsTrue(
                    hostsSession.WaitForExactCount<Button>("Accept", 0, 10_000),
                    "The startup warning did not close after invoking Accept.");
            }
        }

        private static void RemoveAllEntriesOn(Session hostsSession)
        {
            while (true)
            {
                int countBefore = hostsSession.CountExact<Button>("Delete");
                if (countBefore == 0)
                {
                    return;
                }

                var deleteButton = hostsSession.FindAll<Button>(By.Name("Delete"), 1_000)
                    .First(button => string.Equals(button.Name, "Delete", StringComparison.Ordinal));
                deleteButton.Focus();

                // Focusing selects the row and rebuilds its automation subtree, so invoke a freshly
                // resolved button rather than the now-stale element used for Focus().
                hostsSession.FindAll<Button>(By.Name("Delete"), 1_000)
                    .First(button => string.Equals(button.Name, "Delete", StringComparison.Ordinal))
                    .Invoke(msPostAction: 0);

                hostsSession.FindExact<Button>("Yes").Invoke(msPostAction: 0);
                Assert.IsTrue(
                    hostsSession.WaitFor(() => hostsSession.CountExact<Button>("Delete") < countBefore, 5_000, pollIntervalMS: 250),
                    "The entry count did not decrease after confirming deletion.");
            }
        }

        private static void ApplyAdditionalContentAndAssertOrder(
            Session hostsSession,
            string host,
            string marker,
            bool markerShouldBeFirst)
        {
            for (int attempt = 1; attempt <= 5; attempt++)
            {
                // A unique suffix guarantees each retry changes the bound text and triggers a write.
                SetAdditionalContent(hostsSession, $"{marker}-{attempt}");
                var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(2);
                while (DateTime.UtcNow < deadline)
                {
                    var lines = File.ReadAllLines(HostsFilePath).ToList();
                    int entryIndex = lines.FindIndex(line => line.Contains(host, StringComparison.Ordinal));
                    int markerIndex = lines.FindIndex(line => line.Contains(marker, StringComparison.Ordinal));
                    bool correctOrder = entryIndex >= 0 &&
                                        markerIndex >= 0 &&
                                        (markerShouldBeFirst ? markerIndex < entryIndex : markerIndex > entryIndex);
                    if (correctOrder)
                    {
                        return;
                    }

                    Thread.Sleep(200);
                }
            }

            Assert.Fail(
                markerShouldBeFirst
                    ? "With 'Top' selected, additional content should appear before the entries."
                    : "With 'Bottom' selected, additional content should appear after the entries.");
        }

        private bool IsHostsFileEditorClosed()
        {
            return WindowsFinder.ListByApp("PowerToys.Hosts").Count == 0;
        }

        private static bool WaitForHostsFileLine(string needle, int timeoutMS)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMS);
            while (DateTime.UtcNow < deadline)
            {
                if (File.ReadAllLines(HostsFilePath).Any(line => line.Contains(needle, StringComparison.Ordinal)))
                {
                    return true;
                }

                Thread.Sleep(200);
            }

            return false;
        }

        private Session NavigateToHostsSettings()
        {
            var settingsSession = Session.FromProcess("PowerToys.Settings", PowerToysModule.PowerToysSettings);
            if (!settingsSession.Has(By.AccessibilityId("HostsNavItem"), 500))
            {
                settingsSession.Find<NavigationViewItem>(By.AccessibilityId("AdvancedNavItem")).Click();
            }

            settingsSession.Find<NavigationViewItem>(By.AccessibilityId("HostsNavItem")).Click();
            return settingsSession;
        }

        /// <summary>
        /// Configures and launches the Hosts File Editor from the PowerToys Settings page, mirroring
        /// the legacy <c>LaunchFromSetting</c> helper. Returns a <see cref="Session"/> bound to the
        /// newly-launched (or already-running) Hosts window.
        /// </summary>
        private Session LaunchFromSetting(bool showWarning = false, bool openAsAdmin = false)
        {
            var settingsSession = NavigateToHostsSettings();

            settingsSession.FindExact<ToggleSwitch>("Hosts File Editor").Toggle(true);
            settingsSession.FindExact<ToggleSwitch>("Open as administrator").Toggle(openAsAdmin);
            settingsSession.FindExact<ToggleSwitch>("Show a warning at startup").Toggle(showWarning);

            // Launch Hosts File Editor.
            settingsSession.FindExact<Button>("Open Hosts File Editor").Click();

            return AttachHostsEditor();
        }

        private static Session AttachHostsEditor()
        {
            var hostsSession = Session.Attach(PowerToysModule.Hosts);
            WindowHelper.MaximizeWindow(new IntPtr(hostsSession.WindowHandle));
            Thread.Sleep(200);
            return hostsSession;
        }
    }
}
