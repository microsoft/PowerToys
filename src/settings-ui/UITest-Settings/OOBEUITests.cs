// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using Microsoft.PowerToys.UITest;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.Settings.UITests
{
    [TestClass]
    public class OOBEUITests : UITestBase
    {
        // Constants for file paths and identifiers
        private const string LocalAppDataFolderPath = "%localappdata%\\Microsoft\\PowerToys";
        private const string LastVersionFilePath = "%localappdata%\\Microsoft\\PowerToys\\last_version.txt";

        public OOBEUITests()
            : base(PowerToysModule.PowerToysSettings)
        {
        }

        [TestMethod("OOBE.Basic.FirstStartTest")]
        [TestCategory("OOBE test #1")]
        public void TestOOBEFirstStart()
        {
            // Clean up previous PowerToys data to simulate first start
            // CleanPowerToysData();

            // Start PowerToys and verify OOBE opens
            // StartPowerToysAndVerifyOOBEOpens();

            // Navigate through all OOBE sections
            NavigateThroughOOBESections();

            // Close OOBE
            CloseOOBE();

            // Verify OOBE can be opened from Settings
            // OpenOOBEFromSettings();
        }

        /*

        [TestMethod("OOBE.WhatsNew.Test")]
        [TestCategory("OOBE test #2")]
        public void TestOOBEWhatsNew()
        {
            // Modify version file to trigger What's New
            ModifyLastVersionFile();

            // Start PowerToys and verify OOBE opens in What's New page
            StartPowerToysAndVerifyWhatsNewOpens();

            // Close OOBE
            CloseOOBE();
        }
        */

        private void CleanPowerToysData()
        {
            this.ExitScopeExe();

            // Exit PowerToys if it's running
            try
            {
                foreach (Process process in Process.GetProcessesByName("PowerToys"))
                {
                    process.Kill();
                    process.WaitForExit();
                }

                // Delete PowerToys folder in LocalAppData
                string powerToysFolder = Environment.ExpandEnvironmentVariables(LocalAppDataFolderPath);
                if (Directory.Exists(powerToysFolder))
                {
                    Directory.Delete(powerToysFolder, true);
                }

                // Wait to ensure deletion is complete
                Task.Delay(1000).Wait();
            }
            catch (Exception ex)
            {
                Assert.Inconclusive($"Could not clean PowerToys data: {ex.Message}");
            }
        }

        private void StartPowerToysAndVerifyOOBEOpens()
        {
            try
            {
                // Start PowerToys
                this.RestartScopeExe();

                // Wait for OOBE window to appear
                Task.Delay(5000).Wait();

                // Verify OOBE window opened
                Assert.IsTrue(this.Session.HasOne("Welcome to PowerToys"), "OOBE window should open with 'Welcome to PowerToys' title");

                // Verify we're on the Overview page
                Assert.IsTrue(this.Has("Overview"), "OOBE should start on Overview page");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Failed to start PowerToys and verify OOBE: {ex.Message}");
            }
        }

        private void NavigateThroughOOBESections()
        {
            // List of modules to test
            string[] modules = new string[]
            {
                "What's new",
                "Advanced Paste",
            };

            this.Find<NavigationViewItem>("Welcome to PowerToys").Click();

            foreach (string module in modules)
            {
                TestModule(module);
            }
        }

        private void TestModule(string moduleName)
        {
            var oobeWindow = this.Find<Window>("Welcome to PowerToys");
            Assert.IsNotNull(oobeWindow);

            /*
                - [] open the Settings for that module
                - [] verify the Settings work as expected (toggle some controls on/off etc.)
                - [] close the Settings
                - [] if it's available, test the `Launch module name` button
                */
            oobeWindow.Find<Button>(By.Name("Open Settings")).Click();

            // Find<NavigationViewItem>("What's new").Click();
            Task.Delay(1000).Wait();
        }

        private void CloseOOBE()
        {
            try
            {
                // Find the close button and click it
                this.Session.CloseMainWindow();
                Task.Delay(1000).Wait();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Failed to close OOBE: {ex.Message}");
            }
        }

        private void OpenOOBEFromSettings()
        {
            try
            {
                // Open PowerToys Settings
                this.Session.Attach(PowerToysModule.PowerToysSettings);

                // Navigate to General page
                this.Find<NavigationViewItem>("General").Click();
                Task.Delay(1000).Wait();

                // Click on "Welcome to PowerToys" link
                this.Find<HyperlinkButton>("Welcome to PowerToys").Click();
                Task.Delay(2000).Wait();

                // Verify OOBE opened
                Assert.IsTrue(this.Session.HasOne("Welcome to PowerToys"), "OOBE should open when clicking the link in Settings");

                // Close OOBE
                this.Session.CloseMainWindow();
            }
            catch (Exception ex)
            {
                Assert.Fail($"Failed to open OOBE from Settings: {ex.Message}");
            }
        }

        private void ModifyLastVersionFile()
        {
            try
            {
                // Create PowerToys folder if it doesn't exist
                string powerToysFolder = Environment.ExpandEnvironmentVariables(LocalAppDataFolderPath);
                if (!Directory.Exists(powerToysFolder))
                {
                    Directory.CreateDirectory(powerToysFolder);
                }

                // Write a different version to trigger What's New
                string versionFilePath = Environment.ExpandEnvironmentVariables(LastVersionFilePath);
                File.WriteAllText(versionFilePath, "0.0.1");

                // Wait to ensure file is written
                Task.Delay(1000).Wait();
            }
            catch (Exception ex)
            {
                Assert.Inconclusive($"Could not modify version file: {ex.Message}");
            }
        }

        private void StartPowerToysAndVerifyWhatsNewOpens()
        {
            try
            {
                // Start PowerToys
                this.RestartScopeExe();

                // Wait for OOBE window to appear
                Task.Delay(5000).Wait();

                // Verify OOBE window opened
                Assert.IsTrue(this.Session.HasOne("Welcome to PowerToys"), "OOBE window should open");

                // Verify we're on the What's New page
                Assert.IsTrue(this.Has("What's new"), "OOBE should open on What's New page after version change");
            }
            catch (Exception ex)
            {
                Assert.Fail($"Failed to verify What's New page: {ex.Message}");
            }
        }
    }
}
