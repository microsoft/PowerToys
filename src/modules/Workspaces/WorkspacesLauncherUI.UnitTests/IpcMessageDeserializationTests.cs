// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.Data;

namespace WorkspacesLauncherUI.UnitTests
{
    /// <summary>
    /// Tests for JSON deserialization of IPC messages received from the C++ launcher engine.
    /// These messages drive the entire Launcher UI state. Ensuring correct deserialization
    /// is critical for migration parity.
    /// </summary>
    [TestClass]
    public class IpcMessageDeserializationTests
    {
        private const string FullIpcMessage = @"{
            ""processId"": 12345,
            ""apps"": {
                ""appLaunchInfos"": [
                    {
                        ""application"": {
                            ""application"": ""Visual Studio Code"",
                            ""application-path"": ""C:\\Users\\test\\AppData\\Local\\Programs\\Microsoft VS Code\\Code.exe"",
                            ""title"": ""MyProject - Visual Studio Code"",
                            ""package-full-name"": """",
                            ""app-user-model-id"": """",
                            ""pwa-app-id"": """",
                            ""command-line-arguments"": ""--reuse-window"",
                            ""is-elevated"": false,
                            ""can-launch-elevated"": true,
                            ""minimized"": false,
                            ""maximized"": true,
                            ""position"": { ""X"": 0, ""Y"": 0, ""width"": 1920, ""height"": 1080 },
                            ""monitor"": 0
                        },
                        ""state"": 2
                    },
                    {
                        ""application"": {
                            ""application"": ""Windows Terminal"",
                            ""application-path"": ""C:\\Program Files\\WindowsApps\\Microsoft.WindowsTerminal_1.0.0.0_x64__8wekyb3d8bbwe\\wt.exe"",
                            ""title"": ""PowerShell"",
                            ""package-full-name"": ""Microsoft.WindowsTerminal_1.0.0.0_x64__8wekyb3d8bbwe"",
                            ""app-user-model-id"": ""Microsoft.WindowsTerminal_8wekyb3d8bbwe!App"",
                            ""pwa-app-id"": """",
                            ""command-line-arguments"": """",
                            ""is-elevated"": false,
                            ""can-launch-elevated"": false,
                            ""minimized"": false,
                            ""maximized"": false,
                            ""position"": { ""X"": 960, ""Y"": 0, ""width"": 960, ""height"": 540 },
                            ""monitor"": 0
                        },
                        ""state"": 0
                    },
                    {
                        ""application"": {
                            ""application"": ""Notepad"",
                            ""application-path"": ""C:\\Windows\\System32\\notepad.exe"",
                            ""title"": ""Untitled - Notepad"",
                            ""package-full-name"": """",
                            ""app-user-model-id"": """",
                            ""pwa-app-id"": """",
                            ""command-line-arguments"": """",
                            ""is-elevated"": false,
                            ""can-launch-elevated"": true,
                            ""minimized"": true,
                            ""maximized"": false,
                            ""position"": { ""X"": 100, ""Y"": 100, ""width"": 800, ""height"": 600 },
                            ""monitor"": 1
                        },
                        ""state"": 3
                    }
                ]
            }
        }";

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_WithMultipleApps_ExtractsLauncherProcessId()
        {
            var parser = new AppLaunchData();
            var result = parser.Deserialize(FullIpcMessage);
            Assert.AreEqual(12345, result.LauncherProcessID);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_WithThreeApps_DeserializesAllAppEntries()
        {
            var parser = new AppLaunchData();
            var result = parser.Deserialize(FullIpcMessage);
            Assert.AreEqual(3, result.AppLaunchInfos.AppLaunchInfoList.Count);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_Win32Application_DeserializesAllApplicationFields()
        {
            var parser = new AppLaunchData();
            var result = parser.Deserialize(FullIpcMessage);
            var vscode = result.AppLaunchInfos.AppLaunchInfoList[0];

            Assert.AreEqual("Visual Studio Code", vscode.Application.Application);
            Assert.AreEqual(@"C:\Users\test\AppData\Local\Programs\Microsoft VS Code\Code.exe", vscode.Application.ApplicationPath);
            Assert.AreEqual("MyProject - Visual Studio Code", vscode.Application.Title);
            Assert.AreEqual(string.Empty, vscode.Application.PackageFullName);
            Assert.AreEqual(string.Empty, vscode.Application.AppUserModelId);
            Assert.AreEqual(string.Empty, vscode.Application.PwaAppId);
            Assert.AreEqual("--reuse-window", vscode.Application.CommandLineArguments);
            Assert.IsFalse(vscode.Application.IsElevated);
            Assert.IsTrue(vscode.Application.CanLaunchElevated);
            Assert.IsFalse(vscode.Application.Minimized);
            Assert.IsTrue(vscode.Application.Maximized);
            Assert.AreEqual(0, vscode.Application.Monitor);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_Win32Application_DeserializesWindowPosition()
        {
            var parser = new AppLaunchData();
            var result = parser.Deserialize(FullIpcMessage);
            var pos = result.AppLaunchInfos.AppLaunchInfoList[0].Application.Position;

            Assert.AreEqual(0, pos.X);
            Assert.AreEqual(0, pos.Y);
            Assert.AreEqual(1920, pos.Width);
            Assert.AreEqual(1080, pos.Height);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_PackagedUwpApp_DeserializesPackageIdentifiers()
        {
            var parser = new AppLaunchData();
            var result = parser.Deserialize(FullIpcMessage);
            var terminal = result.AppLaunchInfos.AppLaunchInfoList[1];

            Assert.AreEqual("Windows Terminal", terminal.Application.Application);
            Assert.AreEqual("Microsoft.WindowsTerminal_1.0.0.0_x64__8wekyb3d8bbwe", terminal.Application.PackageFullName);
            Assert.AreEqual("Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", terminal.Application.AppUserModelId);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_StateValueTwo_MapsToLaunchedAndMovedEnum()
        {
            var parser = new AppLaunchData();
            var result = parser.Deserialize(FullIpcMessage);
            Assert.AreEqual(LaunchingState.LaunchedAndMoved, result.AppLaunchInfos.AppLaunchInfoList[0].State);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_StateValueZero_MapsToWaitingEnum()
        {
            var parser = new AppLaunchData();
            var result = parser.Deserialize(FullIpcMessage);
            Assert.AreEqual(LaunchingState.Waiting, result.AppLaunchInfos.AppLaunchInfoList[1].State);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_StateValueThree_MapsToFailedEnum()
        {
            var parser = new AppLaunchData();
            var result = parser.Deserialize(FullIpcMessage);
            Assert.AreEqual(LaunchingState.Failed, result.AppLaunchInfos.AppLaunchInfoList[2].State);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_MinimizedWindow_DeserializesWindowStateFlags()
        {
            var parser = new AppLaunchData();
            var result = parser.Deserialize(FullIpcMessage);
            var notepad = result.AppLaunchInfos.AppLaunchInfoList[2];

            Assert.IsTrue(notepad.Application.Minimized);
            Assert.IsFalse(notepad.Application.Maximized);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_SecondaryMonitor_DeserializesMonitorIndex()
        {
            var parser = new AppLaunchData();
            var result = parser.Deserialize(FullIpcMessage);
            Assert.AreEqual(1, result.AppLaunchInfos.AppLaunchInfoList[2].Application.Monitor);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_ProgressiveWebApp_DeserializesPwaIdentifier()
        {
            string pwaMessage = @"{
                ""processId"": 100,
                ""apps"": {
                    ""appLaunchInfos"": [
                        {
                            ""application"": {
                                ""application"": ""Gmail"",
                                ""application-path"": ""C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe"",
                                ""title"": ""Gmail"",
                                ""package-full-name"": """",
                                ""app-user-model-id"": """",
                                ""pwa-app-id"": ""fmgjjmmmlfnkbppncijlocphclkkleod"",
                                ""command-line-arguments"": """",
                                ""is-elevated"": false,
                                ""can-launch-elevated"": false,
                                ""minimized"": false,
                                ""maximized"": false,
                                ""position"": { ""X"": 0, ""Y"": 0, ""width"": 800, ""height"": 600 },
                                ""monitor"": 0
                            },
                            ""state"": 1
                        }
                    ]
                }
            }";

            var parser = new AppLaunchData();
            var result = parser.Deserialize(pwaMessage);
            var gmail = result.AppLaunchInfos.AppLaunchInfoList[0];

            Assert.AreEqual("fmgjjmmmlfnkbppncijlocphclkkleod", gmail.Application.PwaAppId);
            Assert.AreEqual(LaunchingState.Launched, gmail.State);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_ElevatedProcess_DeserializesAdminFlags()
        {
            string elevatedMessage = @"{
                ""processId"": 200,
                ""apps"": {
                    ""appLaunchInfos"": [
                        {
                            ""application"": {
                                ""application"": ""Registry Editor"",
                                ""application-path"": ""C:\\Windows\\regedit.exe"",
                                ""title"": ""Registry Editor"",
                                ""package-full-name"": """",
                                ""app-user-model-id"": """",
                                ""pwa-app-id"": """",
                                ""command-line-arguments"": """",
                                ""is-elevated"": true,
                                ""can-launch-elevated"": true,
                                ""minimized"": false,
                                ""maximized"": false,
                                ""position"": { ""X"": 100, ""Y"": 100, ""width"": 1024, ""height"": 768 },
                                ""monitor"": 0
                            },
                            ""state"": 2
                        }
                    ]
                }
            }";

            var parser = new AppLaunchData();
            var result = parser.Deserialize(elevatedMessage);
            var regedit = result.AppLaunchInfos.AppLaunchInfoList[0];

            Assert.IsTrue(regedit.Application.IsElevated);
            Assert.IsTrue(regedit.Application.CanLaunchElevated);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_SingleAppWorkspace_DeserializesSuccessfully()
        {
            string singleAppMessage = @"{
                ""processId"": 1,
                ""apps"": {
                    ""appLaunchInfos"": [
                        {
                            ""application"": {
                                ""application"": ""Notepad"",
                                ""application-path"": ""C:\\Windows\\System32\\notepad.exe"",
                                ""title"": """",
                                ""package-full-name"": """",
                                ""app-user-model-id"": """",
                                ""pwa-app-id"": """",
                                ""command-line-arguments"": """",
                                ""is-elevated"": false,
                                ""can-launch-elevated"": false,
                                ""minimized"": false,
                                ""maximized"": false,
                                ""position"": { ""X"": 0, ""Y"": 0, ""width"": 400, ""height"": 300 },
                                ""monitor"": 0
                            },
                            ""state"": 0
                        }
                    ]
                }
            }";

            var parser = new AppLaunchData();
            var result = parser.Deserialize(singleAppMessage);

            Assert.AreEqual(1, result.AppLaunchInfos.AppLaunchInfoList.Count);
            Assert.AreEqual("Notepad", result.AppLaunchInfos.AppLaunchInfoList[0].Application.Application);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_ZeroApps_ReturnsEmptyListWithValidProcessId()
        {
            string emptyAppsMessage = @"{
                ""processId"": 42,
                ""apps"": {
                    ""appLaunchInfos"": []
                }
            }";

            var parser = new AppLaunchData();
            var result = parser.Deserialize(emptyAppsMessage);

            Assert.AreEqual(42, result.LauncherProcessID);
            Assert.AreEqual(0, result.AppLaunchInfos.AppLaunchInfoList.Count);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        [ExpectedException(typeof(JsonException))]
        public void IpcMessage_MalformedJson_ThrowsJsonException()
        {
            var parser = new AppLaunchData();
            parser.Deserialize("not valid json {{{");
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        [ExpectedException(typeof(JsonException))]
        public void IpcMessage_EmptyPayload_ThrowsJsonException()
        {
            var parser = new AppLaunchData();
            parser.Deserialize(string.Empty);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_LeftOfPrimaryMonitor_DeserializesNegativeCoordinates()
        {
            string negativePositionMessage = @"{
                ""processId"": 1,
                ""apps"": {
                    ""appLaunchInfos"": [
                        {
                            ""application"": {
                                ""application"": ""Notepad"",
                                ""application-path"": ""C:\\Windows\\System32\\notepad.exe"",
                                ""title"": """",
                                ""package-full-name"": """",
                                ""app-user-model-id"": """",
                                ""pwa-app-id"": """",
                                ""command-line-arguments"": """",
                                ""is-elevated"": false,
                                ""can-launch-elevated"": false,
                                ""minimized"": false,
                                ""maximized"": false,
                                ""position"": { ""X"": -1920, ""Y"": -200, ""width"": 800, ""height"": 600 },
                                ""monitor"": 1
                            },
                            ""state"": 0
                        }
                    ]
                }
            }";

            var parser = new AppLaunchData();
            var result = parser.Deserialize(negativePositionMessage);
            var pos = result.AppLaunchInfos.AppLaunchInfoList[0].Application.Position;

            Assert.AreEqual(-1920, pos.X);
            Assert.AreEqual(-200, pos.Y);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_FourthMonitor_DeserializesHighMonitorIndex()
        {
            string multiMonitorMessage = @"{
                ""processId"": 1,
                ""apps"": {
                    ""appLaunchInfos"": [
                        {
                            ""application"": {
                                ""application"": ""App"",
                                ""application-path"": ""C:\\app.exe"",
                                ""title"": """",
                                ""package-full-name"": """",
                                ""app-user-model-id"": """",
                                ""pwa-app-id"": """",
                                ""command-line-arguments"": """",
                                ""is-elevated"": false,
                                ""can-launch-elevated"": false,
                                ""minimized"": false,
                                ""maximized"": false,
                                ""position"": { ""X"": 3840, ""Y"": 0, ""width"": 1920, ""height"": 1080 },
                                ""monitor"": 3
                            },
                            ""state"": 0
                        }
                    ]
                }
            }";

            var parser = new AppLaunchData();
            var result = parser.Deserialize(multiMonitorMessage);
            Assert.AreEqual(3, result.AppLaunchInfos.AppLaunchInfoList[0].Application.Monitor);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_AllFiveStateValues_MapToCorrectEnumMembers()
        {
            for (int stateValue = 0; stateValue <= 4; stateValue++)
            {
                string template = @"{""processId"": 1,""apps"": {""appLaunchInfos"": [{""application"": {""application"": ""App"",""application-path"": ""C:\\app.exe"",""title"": """",""package-full-name"": """",""app-user-model-id"": """",""pwa-app-id"": """",""command-line-arguments"": """",""is-elevated"": false,""can-launch-elevated"": false,""minimized"": false,""maximized"": false,""position"": { ""X"": 0, ""Y"": 0, ""width"": 100, ""height"": 100 },""monitor"": 0},""state"": STATE_PLACEHOLDER}]}}";
                string message = template.Replace("STATE_PLACEHOLDER", stateValue.ToString(CultureInfo.InvariantCulture));

                var parser = new AppLaunchData();
                var result = parser.Deserialize(message);
                Assert.AreEqual((LaunchingState)stateValue, result.AppLaunchInfos.AppLaunchInfoList[0].State);
            }
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_CommandLineWithSpecialChars_PreservesArgumentsExactly()
        {
            string cliMessage = @"{
                ""processId"": 1,
                ""apps"": {
                    ""appLaunchInfos"": [
                        {
                            ""application"": {
                                ""application"": ""VS Code"",
                                ""application-path"": ""C:\\Code.exe"",
                                ""title"": """",
                                ""package-full-name"": """",
                                ""app-user-model-id"": """",
                                ""pwa-app-id"": """",
                                ""command-line-arguments"": ""--new-window --goto C:\\project\\file.ts:42"",
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

            var parser = new AppLaunchData();
            var result = parser.Deserialize(cliMessage);
            Assert.AreEqual(@"--new-window --goto C:\project\file.ts:42", result.AppLaunchInfos.AppLaunchInfoList[0].Application.CommandLineArguments);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_JapaneseAppName_DeserializesUnicodeCorrectly()
        {
            string unicodeMessage = @"{
                ""processId"": 1,
                ""apps"": {
                    ""appLaunchInfos"": [
                        {
                            ""application"": {
                                ""application"": ""\u30E1\u30E2\u5E33"",
                                ""application-path"": ""C:\\Windows\\System32\\notepad.exe"",
                                ""title"": ""\u7121\u984C - \u30E1\u30E2\u5E33"",
                                ""package-full-name"": """",
                                ""app-user-model-id"": """",
                                ""pwa-app-id"": """",
                                ""command-line-arguments"": """",
                                ""is-elevated"": false,
                                ""can-launch-elevated"": false,
                                ""minimized"": false,
                                ""maximized"": false,
                                ""position"": { ""X"": 0, ""Y"": 0, ""width"": 400, ""height"": 300 },
                                ""monitor"": 0
                            },
                            ""state"": 0
                        }
                    ]
                }
            }";

            var parser = new AppLaunchData();
            var result = parser.Deserialize(unicodeMessage);

            Assert.AreEqual("\u30E1\u30E2\u5E33", result.AppLaunchInfos.AppLaunchInfoList[0].Application.Application);
            Assert.AreEqual("\u7121\u984C - \u30E1\u30E2\u5E33", result.AppLaunchInfos.AppLaunchInfoList[0].Application.Title);
        }

        [TestMethod]
        [TestCategory("Deserialization")]
        public void IpcMessage_TenAppWorkspace_DeserializesAllWithCorrectPositionsAndStates()
        {
            var appEntries = new StringBuilder();
            for (int i = 0; i < 10; i++)
            {
                if (i > 0)
                {
                    appEntries.Append(',');
                }

                string entry = string.Create(CultureInfo.InvariantCulture, $@"{{""application"": {{""application"": ""App{i}"",""application-path"": ""C:\\app{i}.exe"",""title"": ""Window {i}"",""package-full-name"": """",""app-user-model-id"": """",""pwa-app-id"": """",""command-line-arguments"": """",""is-elevated"": false,""can-launch-elevated"": false,""minimized"": false,""maximized"": false,""position"": {{ ""X"": {i * 100}, ""Y"": 0, ""width"": 400, ""height"": 300 }},""monitor"": {i % 3}}},""state"": {i % 5}}}");
                appEntries.Append(entry);
            }

            string manyAppsMessage = string.Create(CultureInfo.InvariantCulture, $@"{{""processId"": 9999,""apps"": {{""appLaunchInfos"": [{appEntries}]}}}}");

            var parser = new AppLaunchData();
            var result = parser.Deserialize(manyAppsMessage);

            Assert.AreEqual(10, result.AppLaunchInfos.AppLaunchInfoList.Count);
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(string.Create(CultureInfo.InvariantCulture, $"App{i}"), result.AppLaunchInfos.AppLaunchInfoList[i].Application.Application);
                Assert.AreEqual(i * 100, result.AppLaunchInfos.AppLaunchInfoList[i].Application.Position.X);
                Assert.AreEqual(i % 3, result.AppLaunchInfos.AppLaunchInfoList[i].Application.Monitor);
                Assert.AreEqual((LaunchingState)(i % 5), result.AppLaunchInfos.AppLaunchInfoList[i].State);
            }
        }
    }
}
