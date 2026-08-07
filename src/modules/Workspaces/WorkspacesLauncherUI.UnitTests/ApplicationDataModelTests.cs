// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.VisualStudio.TestTools.UnitTesting;
using WorkspacesLauncherUI.Data;

namespace WorkspacesLauncherUI.UnitTests
{
    /// <summary>
    /// Tests for ApplicationWrapper struct field mapping.
    /// All fields must be accessible and hold correct values after deserialization.
    /// </summary>
    [TestClass]
    public class ApplicationDataModelTests
    {
        [TestMethod]
        [TestCategory("DataModel")]
        public void AppField_ApplicationName_StoresDisplayName()
        {
            var app = new ApplicationWrapper { Application = "Visual Studio Code" };
            Assert.AreEqual("Visual Studio Code", app.Application);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppField_ExecutablePath_StoresFullPathWithSpaces()
        {
            var app = new ApplicationWrapper { ApplicationPath = @"C:\Users\test\AppData\Local\Programs\Microsoft VS Code\Code.exe" };
            Assert.AreEqual(@"C:\Users\test\AppData\Local\Programs\Microsoft VS Code\Code.exe", app.ApplicationPath);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppField_WindowTitle_StoresActiveWindowTitle()
        {
            var app = new ApplicationWrapper { Title = "MyProject - Visual Studio Code" };
            Assert.AreEqual("MyProject - Visual Studio Code", app.Title);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppField_PackageFullName_StoresUwpPackageIdentifier()
        {
            var app = new ApplicationWrapper { PackageFullName = "Microsoft.WindowsTerminal_1.21.0.0_x64__8wekyb3d8bbwe" };
            Assert.AreEqual("Microsoft.WindowsTerminal_1.21.0.0_x64__8wekyb3d8bbwe", app.PackageFullName);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppField_AppUserModelId_StoresAumidForPackagedApps()
        {
            var app = new ApplicationWrapper { AppUserModelId = "Microsoft.WindowsTerminal_8wekyb3d8bbwe!App" };
            Assert.AreEqual("Microsoft.WindowsTerminal_8wekyb3d8bbwe!App", app.AppUserModelId);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppField_PwaAppId_StoresChromeOrEdgePwaIdentifier()
        {
            var app = new ApplicationWrapper { PwaAppId = "fmgjjmmmlfnkbppncijlocphclkkleod" };
            Assert.AreEqual("fmgjjmmmlfnkbppncijlocphclkkleod", app.PwaAppId);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppField_CliArguments_StoresLaunchArgumentsExactly()
        {
            var app = new ApplicationWrapper { CommandLineArguments = "--reuse-window --goto file.ts:42" };
            Assert.AreEqual("--reuse-window --goto file.ts:42", app.CommandLineArguments);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppField_IsElevated_StoresAdminRunningState()
        {
            var app = new ApplicationWrapper { IsElevated = true };
            Assert.IsTrue(app.IsElevated);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppField_CanLaunchElevated_StoresElevationCapability()
        {
            var app = new ApplicationWrapper { CanLaunchElevated = true };
            Assert.IsTrue(app.CanLaunchElevated);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppField_Minimized_StoresMinimizedWindowState()
        {
            var app = new ApplicationWrapper { Minimized = true };
            Assert.IsTrue(app.Minimized);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppField_Maximized_StoresMaximizedWindowState()
        {
            var app = new ApplicationWrapper { Maximized = true };
            Assert.IsTrue(app.Maximized);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppField_MonitorIndex_StoresTargetDisplayNumber()
        {
            var app = new ApplicationWrapper { Monitor = 2 };
            Assert.AreEqual(2, app.Monitor);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppField_WindowPosition_StoresRectangleCoordinates()
        {
            var pos = new PositionWrapper { X = 100, Y = 200, Width = 800, Height = 600 };
            var app = new ApplicationWrapper { Position = pos };

            Assert.AreEqual(100, app.Position.X);
            Assert.AreEqual(200, app.Position.Y);
            Assert.AreEqual(800, app.Position.Width);
            Assert.AreEqual(600, app.Position.Height);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppDefaults_StringFields_AreNullBeforeDeserialization()
        {
            ApplicationWrapper app = default;

            Assert.IsNull(app.Application);
            Assert.IsNull(app.ApplicationPath);
            Assert.IsNull(app.Title);
            Assert.IsNull(app.PackageFullName);
            Assert.IsNull(app.AppUserModelId);
            Assert.IsNull(app.PwaAppId);
            Assert.IsNull(app.CommandLineArguments);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppDefaults_BooleanFields_AreFalseBeforeDeserialization()
        {
            ApplicationWrapper app = default;

            Assert.IsFalse(app.IsElevated);
            Assert.IsFalse(app.CanLaunchElevated);
            Assert.IsFalse(app.Minimized);
            Assert.IsFalse(app.Maximized);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppDefaults_MonitorIndex_IsZeroPrimaryMonitor()
        {
            ApplicationWrapper app = default;
            Assert.AreEqual(0, app.Monitor);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppConfig_AdminAppOnSecondMonitor_AllFieldsPopulated()
        {
            var app = new ApplicationWrapper
            {
                Application = "Registry Editor",
                ApplicationPath = @"C:\Windows\regedit.exe",
                Title = "Registry Editor",
                PackageFullName = string.Empty,
                AppUserModelId = string.Empty,
                PwaAppId = string.Empty,
                CommandLineArguments = string.Empty,
                IsElevated = true,
                CanLaunchElevated = true,
                Minimized = false,
                Maximized = false,
                Position = new PositionWrapper { X = 1920, Y = 0, Width = 1024, Height = 768 },
                Monitor = 1,
            };

            Assert.IsTrue(app.IsElevated);
            Assert.AreEqual(1, app.Monitor);
            Assert.AreEqual(1920, app.Position.X);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppConfig_MinimizedOnThirdMonitor_StateAndMonitorCorrect()
        {
            var app = new ApplicationWrapper
            {
                Application = "Notepad",
                ApplicationPath = @"C:\Windows\System32\notepad.exe",
                Minimized = true,
                Maximized = false,
                Position = new PositionWrapper { X = 3840, Y = 0, Width = 800, Height = 600 },
                Monitor = 2,
            };

            Assert.IsTrue(app.Minimized);
            Assert.IsFalse(app.Maximized);
            Assert.AreEqual(2, app.Monitor);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppConfig_PathWithParenthesesAndSpaces_PreservedExactly()
        {
            string complexPath = @"C:\Program Files (x86)\Microsoft Office\root\Office16\WINWORD.EXE";
            var app = new ApplicationWrapper { ApplicationPath = complexPath };
            Assert.AreEqual(complexPath, app.ApplicationPath);
        }

        [TestMethod]
        [TestCategory("DataModel")]
        public void AppConfig_ExplicitEmptyStrings_AreEmptyNotNull()
        {
            var app = new ApplicationWrapper
            {
                Application = string.Empty,
                ApplicationPath = string.Empty,
                Title = string.Empty,
                PackageFullName = string.Empty,
                AppUserModelId = string.Empty,
                PwaAppId = string.Empty,
                CommandLineArguments = string.Empty,
            };

            Assert.AreEqual(string.Empty, app.Application);
            Assert.AreEqual(string.Empty, app.ApplicationPath);
            Assert.AreEqual(string.Empty, app.PackageFullName);
        }
    }
}
