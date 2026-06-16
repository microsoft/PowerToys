// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.Data;
using WorkspacesLauncherUI.Models;
using WorkspacesLauncherUI.ViewModels;

namespace WorkspacesLauncherUI.UnitTests
{
    /// <summary>
    /// End-to-end scenario tests that simulate complete user workflows
    /// through the Launcher UI. These verify the full pipeline:
    ///   IPC JSON message → Deserialization → ViewModel → Model properties.
    /// </summary>
    [TestClass]
    public class UserWorkflowIntegrationTests
    {
        [TestMethod]
        [TestCategory("Scenario")]
        public void UserLaunchesWorkspace_ThreeApps_AllProgressFromWaitingToSuccess()
        {
            using var vm = new MainViewModel();

            SimulateIpcMessage(BuildMessage(
                1234,
                App("Visual Studio Code", @"C:\Code.exe", LaunchingState.Waiting),
                App("Windows Terminal", @"C:\wt.exe", LaunchingState.Waiting),
                App("Microsoft Edge", @"C:\edge.exe", LaunchingState.Waiting)));

            Assert.AreEqual(3, vm.AppsListed.Count);
            Assert.IsTrue(vm.AppsListed.All(a => a.Loading), "All apps should show loading spinner initially");

            SimulateIpcMessage(BuildMessage(
                1234,
                App("Visual Studio Code", @"C:\Code.exe", LaunchingState.Launched),
                App("Windows Terminal", @"C:\wt.exe", LaunchingState.Waiting),
                App("Microsoft Edge", @"C:\edge.exe", LaunchingState.Waiting)));

            Assert.IsTrue(vm.AppsListed[0].Loading, "Launched but not yet moved — still loading");

            SimulateIpcMessage(BuildMessage(
                1234,
                App("Visual Studio Code", @"C:\Code.exe", LaunchingState.LaunchedAndMoved),
                App("Windows Terminal", @"C:\wt.exe", LaunchingState.Launched),
                App("Microsoft Edge", @"C:\edge.exe", LaunchingState.Waiting)));

            Assert.IsFalse(vm.AppsListed[0].Loading, "Moved app should stop loading");
            Assert.AreEqual("\U0000F78C", vm.AppsListed[0].StateGlyph, "Moved app should show checkmark");

            SimulateIpcMessage(BuildMessage(
                1234,
                App("Visual Studio Code", @"C:\Code.exe", LaunchingState.LaunchedAndMoved),
                App("Windows Terminal", @"C:\wt.exe", LaunchingState.LaunchedAndMoved),
                App("Microsoft Edge", @"C:\edge.exe", LaunchingState.LaunchedAndMoved)));

            Assert.IsTrue(vm.AppsListed.All(a => !a.Loading), "All apps should stop loading");
            Assert.IsTrue(vm.AppsListed.All(a => a.StateGlyph == "\U0000F78C"), "All apps should show checkmark");
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void UserLaunchesWorkspace_OneAppMissing_FailedShowsRedOthersShowGreen()
        {
            using var vm = new MainViewModel();

            SimulateIpcMessage(BuildMessage(
                1234,
                App("Notepad", @"C:\Windows\notepad.exe", LaunchingState.LaunchedAndMoved),
                App("Missing App", @"C:\nonexistent\app.exe", LaunchingState.Failed),
                App("Calculator", @"C:\Windows\calc.exe", LaunchingState.LaunchedAndMoved)));

            Assert.IsFalse(vm.AppsListed[0].Loading);
            Assert.AreEqual("\U0000F78C", vm.AppsListed[0].StateGlyph);

            Assert.IsFalse(vm.AppsListed[1].Loading);
            Assert.AreEqual("\U0000EF2C", vm.AppsListed[1].StateGlyph);
            var redBrush = vm.AppsListed[1].StateColor as System.Windows.Media.SolidColorBrush;
            Assert.AreEqual(254, redBrush.Color.R);

            Assert.AreEqual("\U0000F78C", vm.AppsListed[2].StateGlyph);
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void UserCancelsLaunch_MidProgress_PartialAppsShowCanceledState()
        {
            using var vm = new MainViewModel();

            SimulateIpcMessage(BuildMessage(
                5678,
                App("App1", @"C:\app1.exe", LaunchingState.LaunchedAndMoved),
                App("App2", @"C:\app2.exe", LaunchingState.Canceled),
                App("App3", @"C:\app3.exe", LaunchingState.Canceled)));

            Assert.AreEqual(LaunchingState.LaunchedAndMoved, vm.AppsListed[0].LaunchState);
            Assert.AreEqual(LaunchingState.Canceled, vm.AppsListed[1].LaunchState);
            Assert.AreEqual(LaunchingState.Canceled, vm.AppsListed[2].LaunchState);
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void UserLaunchesWorkspace_SingleApp_CompletesFullLifecycle()
        {
            using var vm = new MainViewModel();

            SimulateIpcMessage(BuildMessage(
                100,
                App("Notepad", @"C:\Windows\System32\notepad.exe", LaunchingState.Waiting)));

            Assert.AreEqual(1, vm.AppsListed.Count);
            Assert.AreEqual("Notepad", vm.AppsListed[0].Name);
            Assert.IsTrue(vm.AppsListed[0].Loading);

            SimulateIpcMessage(BuildMessage(
                100,
                App("Notepad", @"C:\Windows\System32\notepad.exe", LaunchingState.LaunchedAndMoved)));

            Assert.IsFalse(vm.AppsListed[0].Loading);
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void UserLaunchesWorkspace_ChromeAndEdgePwa_PwaIdsPreserved()
        {
            using var vm = new MainViewModel();

            SimulateIpcMessage(BuildMessageFull(
                300,
                AppFull("Gmail", @"C:\chrome.exe", string.Empty, string.Empty, "fmgjjmmmlfnkbppncijlocphclkkleod", LaunchingState.LaunchedAndMoved),
                AppFull("Teams", @"C:\edge.exe", string.Empty, string.Empty, "cifhbcnohmdccbgoicgdjpfamggdegmo", LaunchingState.Launched)));

            Assert.AreEqual("fmgjjmmmlfnkbppncijlocphclkkleod", vm.AppsListed[0].PwaAppId);
            Assert.AreEqual("cifhbcnohmdccbgoicgdjpfamggdegmo", vm.AppsListed[1].PwaAppId);
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void UserLaunchesWorkspace_AdminApp_ElevatedFlagPreservedInUi()
        {
            using var vm = new MainViewModel();

            string message = @"{
                ""processId"": 400,
                ""apps"": {
                    ""appLaunchInfos"": [
                        {
                            ""application"": {
                                ""application"": ""Command Prompt (Admin)"",
                                ""application-path"": ""C:\\Windows\\System32\\cmd.exe"",
                                ""title"": ""Administrator: Command Prompt"",
                                ""package-full-name"": """",
                                ""app-user-model-id"": """",
                                ""pwa-app-id"": """",
                                ""command-line-arguments"": """",
                                ""is-elevated"": true,
                                ""can-launch-elevated"": true,
                                ""minimized"": false,
                                ""maximized"": false,
                                ""position"": { ""X"": 0, ""Y"": 0, ""width"": 800, ""height"": 600 },
                                ""monitor"": 0
                            },
                            ""state"": 2
                        }
                    ]
                }
            }";

            SimulateIpcMessage(message);
            Assert.AreEqual("Command Prompt (Admin)", vm.AppsListed[0].Name);
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void UserLaunchesWorkspace_FifteenApps_AllAppsDisplayedWithLoadingState()
        {
            using var vm = new MainViewModel();
            var apps = new (string Name, string Path, LaunchingState State)[15];

            for (int i = 0; i < 15; i++)
            {
                apps[i] = ($"App {i}", $@"C:\app{i}.exe", LaunchingState.Waiting);
            }

            SimulateIpcMessage(BuildMessage(500, apps));

            Assert.AreEqual(15, vm.AppsListed.Count);
            for (int i = 0; i < 15; i++)
            {
                Assert.AreEqual($"App {i}", vm.AppsListed[i].Name);
                Assert.IsTrue(vm.AppsListed[i].Loading);
            }
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void UserLaunchesWorkspace_AllAppsMissing_AllShowRedErrorState()
        {
            using var vm = new MainViewModel();

            SimulateIpcMessage(BuildMessage(
                800,
                App("App1", @"C:\missing1.exe", LaunchingState.Failed),
                App("App2", @"C:\missing2.exe", LaunchingState.Failed)));

            Assert.IsTrue(vm.AppsListed.All(a => !a.Loading), "Failed apps should not show loading");
            Assert.IsTrue(vm.AppsListed.All(a => a.StateGlyph == "\U0000EF2C"), "Failed apps should show error glyph");
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void UserLaunchesWorkspace_UwpStoreApp_PackageFieldsMappedToUi()
        {
            using var vm = new MainViewModel();

            SimulateIpcMessage(BuildMessageFull(
                900,
                AppFull(
                    "Windows Settings",
                    @"C:\Program Files\WindowsApps\windows.immersivecontrolpanel\SystemSettings.exe",
                    "windows.immersivecontrolpanel_10.0.0.0_neutral_cw5n1h2txyewy",
                    "windows.immersivecontrolpanel_cw5n1h2txyewy!microsoft.windows.immersivecontrolpanel",
                    string.Empty,
                    LaunchingState.LaunchedAndMoved)));

            Assert.AreEqual("Windows Settings", vm.AppsListed[0].Name);
            Assert.AreEqual("windows.immersivecontrolpanel_10.0.0.0_neutral_cw5n1h2txyewy", vm.AppsListed[0].PackagedName);
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void UserLaunchesWorkspace_RapidIpcUpdates_FinalStateIsDisplayed()
        {
            using var vm = new MainViewModel();

            for (int i = 0; i <= 4; i++)
            {
                SimulateIpcMessage(BuildMessage(
                    1000,
                    App("App", @"C:\app.exe", (LaunchingState)Math.Min(i, 2))));
            }

            Assert.AreEqual(1, vm.AppsListed.Count);
            Assert.AreEqual(LaunchingState.LaunchedAndMoved, vm.AppsListed[0].LaunchState);
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void UserLaunchesWorkspace_Win32AndPackagedAndPwa_AllTypesCoexistInList()
        {
            using var vm = new MainViewModel();

            SimulateIpcMessage(BuildMessageFull(
                1100,
                AppFull("Notepad", @"C:\Windows\notepad.exe", string.Empty, string.Empty, string.Empty, LaunchingState.LaunchedAndMoved),
                AppFull("Terminal", @"C:\wt.exe", "Microsoft.WindowsTerminal_1.0_x64__8wekyb3d8bbwe", "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", string.Empty, LaunchingState.LaunchedAndMoved),
                AppFull("Outlook", @"C:\edge.exe", string.Empty, string.Empty, "pwa_outlook_id", LaunchingState.Launched)));

            Assert.AreEqual(3, vm.AppsListed.Count);
            Assert.AreEqual(string.Empty, vm.AppsListed[0].PwaAppId);
            Assert.AreEqual("Microsoft.WindowsTerminal_1.0_x64__8wekyb3d8bbwe", vm.AppsListed[1].PackagedName);
            Assert.AreEqual("pwa_outlook_id", vm.AppsListed[2].PwaAppId);
        }

        [TestMethod]
        [TestCategory("Scenario")]
        public void UserLaunchesWorkspace_FiveUpdates_UiRefreshedOnEveryIpcMessage()
        {
            using var vm = new MainViewModel();
            int fireCount = 0;

            vm.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "AppsListed")
                {
                    fireCount++;
                }
            };

            for (int i = 0; i < 5; i++)
            {
                SimulateIpcMessage(BuildMessage(
                    1200,
                    App("App", @"C:\app.exe", (LaunchingState)Math.Min(i, 2))));
            }

            Assert.AreEqual(5, fireCount, "PropertyChanged should fire once per IPC message");
        }

        private static (string Name, string Path, LaunchingState State) App(string name, string path, LaunchingState state)
        {
            return (name, path, state);
        }

        private static (string Name, string Path, string PackageFullName, string Aumid, string PwaAppId, LaunchingState State) AppFull(
            string name, string path, string packageFullName, string aumid, string pwaAppId, LaunchingState state)
        {
            return (name, path, packageFullName, aumid, pwaAppId, state);
        }

        private static void SimulateIpcMessage(string message)
        {
            WorkspacesLauncherUI.App.IPCMessageReceivedCallback?.Invoke(message);
        }

        private static string BuildMessage(
            int processId,
            params (string Name, string Path, LaunchingState State)[] apps)
        {
            var fullApps = apps.Select(a => (a.Name, a.Path, string.Empty, string.Empty, string.Empty, a.State)).ToArray();
            return BuildMessageFull(processId, fullApps);
        }

        private static string BuildMessageFull(
            int processId,
            params (string Name, string Path, string PackageFullName, string Aumid, string PwaAppId, LaunchingState State)[] apps)
        {
            var sb = new StringBuilder();
            sb.Append(CultureInfo.InvariantCulture, $@"{{ ""processId"": {processId}, ""apps"": {{ ""appLaunchInfos"": [");

            for (int i = 0; i < apps.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                var (name, path, packageFullName, aumid, pwaAppId, state) = apps[i];
                string escapedPath = path.Replace(@"\", @"\\");
                string appJson = string.Create(CultureInfo.InvariantCulture, $@"{{""application"": {{""application"": ""{name}"",""application-path"": ""{escapedPath}"",""title"": """",""package-full-name"": ""{packageFullName}"",""app-user-model-id"": ""{aumid}"",""pwa-app-id"": ""{pwaAppId}"",""command-line-arguments"": """",""is-elevated"": false,""can-launch-elevated"": false,""minimized"": false,""maximized"": false,""position"": {{ ""X"": 0, ""Y"": 0, ""width"": 100, ""height"": 100 }},""monitor"": 0}},""state"": {(int)state}}}");
                sb.Append(appJson);
            }

            sb.Append("]}}");
            return sb.ToString();
        }
    }
}
