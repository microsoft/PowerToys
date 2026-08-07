// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using System.Text;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.Data;
using WorkspacesLauncherUI.Models;
using WorkspacesLauncherUI.ViewModels;

namespace WorkspacesLauncherUI.UnitTests
{
    /// <summary>
    /// Tests for MainViewModel IPC message handling and state management.
    /// MainViewModel is the core of the Launcher UI — it receives IPC messages
    /// from the C++ launcher engine and populates the AppsListed collection
    /// that the UI binds to.
    /// </summary>
    [TestClass]
    public class LauncherViewModelStateManagementTests
    {
        [TestMethod]
        [TestCategory("ViewModel")]
        public void ReceiveIpcMessage_ValidPayload_PopulatesAppsListedCollection()
        {
            using var vm = new MainViewModel();
            string message = CreateIpcMessage(("App1", @"C:\app1.exe", LaunchingState.Waiting), ("App2", @"C:\app2.exe", LaunchingState.Launched));
            SimulateIpcMessage(message);

            Assert.AreEqual(2, vm.AppsListed.Count);
        }

        [TestMethod]
        [TestCategory("ViewModel")]
        public void ReceiveIpcMessage_ValidPayload_MapsAppNamesFromJson()
        {
            using var vm = new MainViewModel();
            string message = CreateIpcMessage(("Visual Studio Code", @"C:\Code.exe", LaunchingState.Waiting), ("Windows Terminal", @"C:\wt.exe", LaunchingState.Launched));
            SimulateIpcMessage(message);

            Assert.AreEqual("Visual Studio Code", vm.AppsListed[0].Name);
            Assert.AreEqual("Windows Terminal", vm.AppsListed[1].Name);
        }

        [TestMethod]
        [TestCategory("ViewModel")]
        public void ReceiveIpcMessage_MixedStates_MapsEachAppToCorrectState()
        {
            using var vm = new MainViewModel();
            string message = CreateIpcMessage(
                ("App1", @"C:\app1.exe", LaunchingState.Waiting),
                ("App2", @"C:\app2.exe", LaunchingState.Launched),
                ("App3", @"C:\app3.exe", LaunchingState.LaunchedAndMoved),
                ("App4", @"C:\app4.exe", LaunchingState.Failed));
            SimulateIpcMessage(message);

            Assert.AreEqual(LaunchingState.Waiting, vm.AppsListed[0].LaunchState);
            Assert.AreEqual(LaunchingState.Launched, vm.AppsListed[1].LaunchState);
            Assert.AreEqual(LaunchingState.LaunchedAndMoved, vm.AppsListed[2].LaunchState);
            Assert.AreEqual(LaunchingState.Failed, vm.AppsListed[3].LaunchState);
        }

        [TestMethod]
        [TestCategory("ViewModel")]
        public void ReceiveIpcMessage_ValidPayload_PreservesExecutablePaths()
        {
            using var vm = new MainViewModel();
            string message = CreateIpcMessage(("Notepad", @"C:\Windows\System32\notepad.exe", LaunchingState.Waiting));
            SimulateIpcMessage(message);

            Assert.AreEqual(@"C:\Windows\System32\notepad.exe", vm.AppsListed[0].AppPath);
        }

        [TestMethod]
        [TestCategory("ViewModel")]
        public void ReceiveIpcMessage_PackagedApp_MapsPackageNameAndAumid()
        {
            using var vm = new MainViewModel();
            string message = @"{
                ""processId"": 1,
                ""apps"": {
                    ""appLaunchInfos"": [
                        {
                            ""application"": {
                                ""application"": ""Terminal"",
                                ""application-path"": ""C:\\wt.exe"",
                                ""title"": """",
                                ""package-full-name"": ""Microsoft.WindowsTerminal_1.0_x64__8wekyb3d8bbwe"",
                                ""app-user-model-id"": ""Microsoft.WindowsTerminal_8wekyb3d8bbwe!App"",
                                ""pwa-app-id"": """",
                                ""command-line-arguments"": """",
                                ""is-elevated"": false,
                                ""can-launch-elevated"": false,
                                ""minimized"": false,
                                ""maximized"": false,
                                ""position"": { ""X"": 0, ""Y"": 0, ""width"": 100, ""height"": 100 },
                                ""monitor"": 0
                            },
                            ""state"": 0
                        }
                    ]
                }
            }";
            SimulateIpcMessage(message);

            Assert.AreEqual("Microsoft.WindowsTerminal_1.0_x64__8wekyb3d8bbwe", vm.AppsListed[0].PackagedName);
            Assert.AreEqual("Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", vm.AppsListed[0].Aumid);
        }

        [TestMethod]
        [TestCategory("ViewModel")]
        public void ReceiveIpcMessage_PwaApp_MapsPwaAppIdentifier()
        {
            using var vm = new MainViewModel();
            string message = @"{
                ""processId"": 1,
                ""apps"": {
                    ""appLaunchInfos"": [
                        {
                            ""application"": {
                                ""application"": ""Gmail"",
                                ""application-path"": ""C:\\chrome.exe"",
                                ""title"": """",
                                ""package-full-name"": """",
                                ""app-user-model-id"": """",
                                ""pwa-app-id"": ""abc123"",
                                ""command-line-arguments"": """",
                                ""is-elevated"": false,
                                ""can-launch-elevated"": false,
                                ""minimized"": false,
                                ""maximized"": false,
                                ""position"": { ""X"": 0, ""Y"": 0, ""width"": 100, ""height"": 100 },
                                ""monitor"": 0
                            },
                            ""state"": 0
                        }
                    ]
                }
            }";
            SimulateIpcMessage(message);

            Assert.AreEqual("abc123", vm.AppsListed[0].PwaAppId);
        }

        [TestMethod]
        [TestCategory("ViewModel")]
        public void ReceiveIpcMessage_AnyUpdate_RaisesPropertyChangedForDataBinding()
        {
            using var vm = new MainViewModel();
            bool propertyChangedFired = false;
            string changedPropertyName = null;

            vm.PropertyChanged += (sender, args) =>
            {
                propertyChangedFired = true;
                changedPropertyName = args.PropertyName;
            };

            string message = CreateIpcMessage(("App", @"C:\app.exe", LaunchingState.Waiting));
            SimulateIpcMessage(message);

            Assert.IsTrue(propertyChangedFired, "PropertyChanged should fire when AppsListed is updated");
            Assert.AreEqual("AppsListed", changedPropertyName);
        }

        [TestMethod]
        [TestCategory("ViewModel")]
        public void ReceiveIpcMessage_ProgressUpdates_ReplacesEntireCollectionEachTime()
        {
            using var vm = new MainViewModel();

            string msg1 = CreateIpcMessage(("App1", @"C:\app1.exe", LaunchingState.Waiting), ("App2", @"C:\app2.exe", LaunchingState.Waiting));
            SimulateIpcMessage(msg1);
            Assert.AreEqual(2, vm.AppsListed.Count);
            Assert.AreEqual(LaunchingState.Waiting, vm.AppsListed[0].LaunchState);

            string msg2 = CreateIpcMessage(("App1", @"C:\app1.exe", LaunchingState.Launched), ("App2", @"C:\app2.exe", LaunchingState.Waiting));
            SimulateIpcMessage(msg2);
            Assert.AreEqual(2, vm.AppsListed.Count);
            Assert.AreEqual(LaunchingState.Launched, vm.AppsListed[0].LaunchState);

            string msg3 = CreateIpcMessage(("App1", @"C:\app1.exe", LaunchingState.LaunchedAndMoved), ("App2", @"C:\app2.exe", LaunchingState.LaunchedAndMoved));
            SimulateIpcMessage(msg3);
            Assert.AreEqual(LaunchingState.LaunchedAndMoved, vm.AppsListed[0].LaunchState);
            Assert.AreEqual(LaunchingState.LaunchedAndMoved, vm.AppsListed[1].LaunchState);
        }

        [TestMethod]
        [TestCategory("ViewModel")]
        public void ReceiveIpcMessage_SomeAppsFail_AllowsMixedSuccessAndFailure()
        {
            using var vm = new MainViewModel();
            string message = CreateIpcMessage(
                ("App1", @"C:\app1.exe", LaunchingState.LaunchedAndMoved),
                ("App2", @"C:\app2.exe", LaunchingState.Failed),
                ("App3", @"C:\app3.exe", LaunchingState.LaunchedAndMoved));
            SimulateIpcMessage(message);

            Assert.AreEqual(LaunchingState.LaunchedAndMoved, vm.AppsListed[0].LaunchState);
            Assert.AreEqual(LaunchingState.Failed, vm.AppsListed[1].LaunchState);
            Assert.AreEqual(LaunchingState.LaunchedAndMoved, vm.AppsListed[2].LaunchState);
        }

        [TestMethod]
        [TestCategory("ViewModel")]
        public void ReceiveIpcMessage_CanceledState_ReflectedInCollection()
        {
            using var vm = new MainViewModel();
            string message = CreateIpcMessage(("App1", @"C:\app1.exe", LaunchingState.LaunchedAndMoved), ("App2", @"C:\app2.exe", LaunchingState.Canceled));
            SimulateIpcMessage(message);

            Assert.AreEqual(LaunchingState.Canceled, vm.AppsListed[1].LaunchState);
        }

        [TestMethod]
        [TestCategory("ViewModel")]
        public void ReceiveIpcMessage_EmptyAppList_SetsCollectionToEmpty()
        {
            using var vm = new MainViewModel();
            string message = @"{ ""processId"": 1, ""apps"": { ""appLaunchInfos"": [] } }";
            SimulateIpcMessage(message);

            Assert.AreEqual(0, vm.AppsListed.Count);
        }

        [TestMethod]
        [TestCategory("ViewModel")]
        public void ReceiveIpcMessage_CorruptedPayload_GracefullyIgnoredWithoutCrash()
        {
            using var vm = new MainViewModel();
            SimulateIpcMessage("this is not json");

            Assert.AreEqual(0, vm.AppsListed.Count);
        }

        [TestMethod]
        [TestCategory("ViewModel")]
        public void ReceiveIpcMessage_EmptyString_GracefullyIgnoredWithoutCrash()
        {
            using var vm = new MainViewModel();
            SimulateIpcMessage(string.Empty);
            Assert.AreEqual(0, vm.AppsListed.Count);
        }

        [TestMethod]
        [TestCategory("ViewModel")]
        public void DisposeViewModel_SingleCall_CompletesWithoutException()
        {
            var vm = new MainViewModel();
            vm.Dispose();
        }

        [TestMethod]
        [TestCategory("ViewModel")]
        public void DisposeViewModel_MultipleCalls_RemainsIdempotent()
        {
            var vm = new MainViewModel();
            vm.Dispose();
            vm.Dispose();
        }

        private static void SimulateIpcMessage(string message)
        {
            App.IPCMessageReceivedCallback?.Invoke(message);
        }

        private static string CreateIpcMessage(params (string Name, string Path, LaunchingState State)[] apps)
        {
            var sb = new StringBuilder();
            sb.Append(@"{ ""processId"": 1, ""apps"": { ""appLaunchInfos"": [");

            for (int i = 0; i < apps.Length; i++)
            {
                if (i > 0)
                {
                    sb.Append(',');
                }

                var (name, path, state) = apps[i];
                string escapedPath = path.Replace(@"\", @"\\");
                string appJson = string.Create(CultureInfo.InvariantCulture, $@"{{""application"": {{""application"": ""{name}"",""application-path"": ""{escapedPath}"",""title"": """",""package-full-name"": """",""app-user-model-id"": """",""pwa-app-id"": """",""command-line-arguments"": """",""is-elevated"": false,""can-launch-elevated"": false,""minimized"": false,""maximized"": false,""position"": {{ ""X"": 0, ""Y"": 0, ""width"": 100, ""height"": 100 }},""monitor"": 0}},""state"": {(int)state}}}");
                sb.Append(appJson);
            }

            sb.Append("]}}");
            return sb.ToString();
        }
    }
}
