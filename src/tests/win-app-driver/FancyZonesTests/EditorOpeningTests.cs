using System.IO.Abstractions;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorOpeningTests : FancyZonesEditor
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IFile File = FileSystem.File;
        private static readonly IDirectory Directory = FileSystem.Directory;
        void RemoveSettingsFile()
        {
            File.Delete(_zoneSettingsPath);
            File.Delete(_appHistoryPath);
        }

        void RemoveSettingsFolder()
        {
            Directory.Delete(_settingsFolderPath, true);
        }

        void CreateEmptySettingsFile()
        {
            string zoneSettings = "";
            File.WriteAllText(_zoneSettingsPath, zoneSettings);

            string appHistory = "";
            File.WriteAllText(_appHistoryPath, appHistory);
        }

        void CreateDefaultSettingsFile()
        {
            string zoneSettings = "{\"devices\":[],\"custom-zone-sets\":[]}";
            File.WriteAllText(_zoneSettingsPath, zoneSettings);

            string appHistory = "{\"app-zone-history\":[]}";
            File.WriteAllText(_appHistoryPath, appHistory);
        }

        void CreateValidSettingsFile()
        {
            string zoneSettings = "{\"devices\":[{\"device-id\":\"DELA026#5&10a58c63&0&UID16777488_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\",\"active-zoneset\":{\"uuid\":\"{D13ABB6D-7721-4176-9647-C8C0836D99CC}\",\"type\":\"columns\"},\"editor-show-spacing\":true,\"editor-spacing\":16,\"editor-zone-count\":3}],\"custom-zone-sets\":[]}";
            File.WriteAllText(_zoneSettingsPath, zoneSettings);

            string appHistory = "{\"app-zone-history\":[{\"app-path\":\"C:\\Program Files\\Microsoft Visual Studio\\2022\\Community\\Common7\\IDE\\Extensions\\TestPlatform\\testhost.exe\",\"zone-index\":3,\"device-id\":\"DELA026#5&10a58c63&0&UID16777488_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\",\"zoneset-uuid\":\"{D13ABB6D-7721-4176-9647-C8C0836D99CC}\"}]}";
            File.WriteAllText(_appHistoryPath, appHistory);
        }

        void CreateValidSettingsFileWithUtf8()
        {
            string zoneSettings = "{\"devices\":[{\"device-id\":\"DELA026#5&10a58c63&0&UID16777488_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\",\"active-zoneset\":{\"uuid\":\"{D13ABB6D-7721-4176-9647-C8C0836D99CC}\",\"type\":\"columns\"},\"editor-show-spacing\":true,\"editor-spacing\":16,\"editor-zone-count\":3}],\"custom-zone-sets\":[]}";
            File.WriteAllText(_zoneSettingsPath, zoneSettings);

            string appHistory = "{\"app-zone-history\":[{\"app-path\":\"C:\\Program Files (x86)\\йцукен\\testhost.exe\",\"zone-index\":3,\"device-id\":\"DELA026#5&10a58c63&0&UID16777488_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\",\"zoneset-uuid\":\"{D13ABB6D-7721-4176-9647-C8C0836D99CC}\"}]}";
            File.WriteAllText(_appHistoryPath, appHistory);
        }

        void CreateInvalidSettingsFile()
        {
            string zoneSettings = "{\"app-zone-history\":[{\"app-path\":\"C:\\Program Files (x86)\\Microsoft Visual Studio\\testhost.exe\",\"zone-index\":3,\"device-id\":\"wrong-device-id\",\"zoneset-uuid\":\"{D13ABB6D-invalid-uuid-C8C0836D99CC}\"}],\"devices\":[{\"device-id\":\"DELA026#5&10a58c63&0&UID16777488_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\",\"active-zoneset\":{\"uuid\":\"{D13ABB6D-7721-4176-9647-C8C0836D99CC}\",\"type\":\"columns\"},\"editor-show-spacing\":true,\"editor-spacing\":16,\"editor-zone-count\":3}],\"custom-zone-sets\":[]}";
            File.WriteAllText(_zoneSettingsPath, zoneSettings);

            string appHistory = "";
            File.WriteAllText(_appHistoryPath, appHistory);
        }

        void CreateCroppedSettingsFile()
        {
            string zoneSettings = "{\"devices\":[],\"custom-zone-sets\":[{\"uuid\":\"{8BEC7183-C90E-4D41-AD1C-1AC2BC4760BA}\",\"name\":\"";
            File.WriteAllText(_zoneSettingsPath, zoneSettings);

            string appHistory = "{\"app-zone-history\":[]}";
            File.WriteAllText(_appHistoryPath, appHistory);
        }

        void TestEditorOpened(bool errorExpected = false)
        {
            WindowsElement errorMessage = null;
            try
            {
                errorMessage = session.FindElementByName("FancyZones Editor Exception Handler");
                if (errorMessage != null)
                {
                    errorMessage.FindElementByName("OK").Click();
                }
            }
            catch (OpenQA.Selenium.WebDriverException)
            {
                //no error message, it's ok
            }

            editorWindow = session.FindElementByName("FancyZones Editor");
            Assert.IsNotNull(editorWindow);

            if (!errorExpected)
            {
                Assert.IsNull(errorMessage);
            }
            else
            {
                Assert.IsNotNull(errorMessage);
            }
        }

        void OpenEditorBySettingsButton()
        {
            OpenSettings();
            OpenFancyZonesSettings();
            settingsWindow.FindElementByName("Launch zones editor").Click();
        }

        void OpenEditorByHotkey()
        {
            new Actions(session).KeyDown(OpenQA.Selenium.Keys.Command).SendKeys("`").KeyUp(OpenQA.Selenium.Keys.Command).Perform();
        }

        [TestMethod]
        public void OpenEditorBySettingsButtonNoSettings()
        {
            RemoveSettingsFile();
            OpenEditorBySettingsButton();
            TestEditorOpened(true);
        }

        [TestMethod]
        public void OpenEditorBySettingsButtonNoSettingsFolder()
        {
            /*
            if (isPowerToysLaunched)
            {
                ExitPowerToys();
            }
            RemoveSettingsFolder();
            LaunchPowerToys();
            */

            RemoveSettingsFolder();
            OpenEditorBySettingsButton();
            TestEditorOpened(true);
        }

        [TestMethod]
        public void OpenEditorBySettingsButtonEmptySettings()
        {
            CreateEmptySettingsFile();
            OpenEditorBySettingsButton();
            TestEditorOpened(true);
        }

        [TestMethod]
        public void OpenEditorBySettingsButtonDefaultSettings()
        {
            CreateDefaultSettingsFile();
            OpenEditorBySettingsButton();
            TestEditorOpened();
        }

        [TestMethod]
        public void OpenEditorBySettingsButtonValidSettings()
        {
            CreateValidSettingsFile();
            OpenEditorBySettingsButton();
            TestEditorOpened();
        }

        [TestMethod]
        public void OpenEditorBySettingsButtonValidUtf8Settings()
        {
            CreateValidSettingsFileWithUtf8();
            OpenEditorBySettingsButton();
            TestEditorOpened();
        }

        [TestMethod]
        public void OpenEditorBySettingsButtonInvalidSettings()
        {
            CreateInvalidSettingsFile();
            OpenEditorBySettingsButton();
            TestEditorOpened(true);
        }

        [TestMethod]
        public void OpenEditorBySettingsButtonCroppedSettings()
        {
            CreateCroppedSettingsFile();
            OpenEditorBySettingsButton();
            TestEditorOpened(true);
        }

        [TestMethod]
        public void OpenEditorByHotkeyNoSettings()
        {
            RemoveSettingsFile();
            OpenEditorByHotkey();
            TestEditorOpened(true);
        }

        [TestMethod]
        public void OpenEditorByHotkeyNoSettingsFolder()
        {
            /*
            if (isPowerToysLaunched)
            {
                ExitPowerToys();
            }
            RemoveSettingsFolder();
            LaunchPowerToys();
            */
            RemoveSettingsFolder();
            OpenEditorByHotkey();
            TestEditorOpened(true);
        }

        [TestMethod]
        public void OpenEditorByHotkeyEmptySettings()
        {
            CreateEmptySettingsFile();
            OpenEditorByHotkey();
            TestEditorOpened(true);
        }

        [TestMethod]
        public void OpenEditorByHotkeyDefaultSettings()
        {
            CreateDefaultSettingsFile();
            OpenEditorByHotkey();
            TestEditorOpened();
        }

        [TestMethod]
        public void OpenEditorByHotkeyValidSettings()
        {
            CreateValidSettingsFile();
            OpenEditorByHotkey();
            TestEditorOpened();
        }

        [TestMethod]
        public void OpenEditorByHotkeyValidUtf8Settings()
        {
            CreateValidSettingsFileWithUtf8();
            OpenEditorByHotkey();
            TestEditorOpened();
        }

        [TestMethod]
        public void OpenEditorByHotkeyInvalidSettings()
        {
            CreateInvalidSettingsFile();
            OpenEditorByHotkey();
            TestEditorOpened(true);
        }

        [TestMethod]
        public void OpenEditorByHotkeyCroppedSettings()
        {
            CreateCroppedSettingsFile();
            OpenEditorByHotkey();
            TestEditorOpened(true);
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context);
            Assert.IsNotNull(session);
            EnableModules(false, true, false, false, false, false, false, false);

            ResetDefaultFancyZonesSettings(true);
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            ExitPowerToys();
            TearDown();
        }

        [TestInitialize]
        public void TestInitialize()
        {

        }

        [TestCleanup]
        public void TestCleanup()
        {
            CloseEditor();

            if (!Directory.Exists(_settingsFolderPath))
            {
                Directory.CreateDirectory(_settingsFolderPath);
            }
        }
    }
}