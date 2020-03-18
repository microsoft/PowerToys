using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorOpeningTests : PowerToysSession
    {
        WindowsElement editorWindow;

        void RemoveSettingsFile()
        {
            if (Directory.Exists(_zoneSettingsPath))
            {
                Directory.Delete(_zoneSettingsPath);
            }
        }

        void CreateEmptySettingsFile()
        {
            string zoneSettings = "";
            File.WriteAllText(_zoneSettingsPath, zoneSettings);
        }

        void CreateDefaultSettingsFile()
        {
            string zoneSettings = "{\"app-zone-history\":[],\"devices\":[],\"custom-zone-sets\":[]}";
            File.WriteAllText(_zoneSettingsPath, zoneSettings);
        }

        void CreateValidSettingsFile()
        {
            string zoneSettings = "{\"app-zone-history\":[{\"app-path\":\"C:\\Program Files (x86)\\Microsoft Visual Studio\\2019\\Community\\Common7\\IDE\\Extensions\\TestPlatform\\testhost.exe\",\"zone-index\":3,\"device-id\":\"DELA026#5&10a58c63&0&UID16777488_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\",\"zoneset-uuid\":\"{D13ABB6D-7721-4176-9647-C8C0836D99CC}\"}],\"devices\":[{\"device-id\":\"DELA026#5&10a58c63&0&UID16777488_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\",\"active-zoneset\":{\"uuid\":\"{D13ABB6D-7721-4176-9647-C8C0836D99CC}\",\"type\":\"columns\"},\"editor-show-spacing\":true,\"editor-spacing\":16,\"editor-zone-count\":3}],\"custom-zone-sets\":[]}";
            File.WriteAllText(_zoneSettingsPath, zoneSettings);
        }

        void CreateValidSettingsFileWithUtf8()
        {
            string zoneSettings = "{\"app-zone-history\":[{\"app-path\":\"C:\\Program Files (x86)\\йцукен\\testhost.exe\",\"zone-index\":3,\"device-id\":\"DELA026#5&10a58c63&0&UID16777488_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\",\"zoneset-uuid\":\"{D13ABB6D-7721-4176-9647-C8C0836D99CC}\"}],\"devices\":[{\"device-id\":\"DELA026#5&10a58c63&0&UID16777488_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\",\"active-zoneset\":{\"uuid\":\"{D13ABB6D-7721-4176-9647-C8C0836D99CC}\",\"type\":\"columns\"},\"editor-show-spacing\":true,\"editor-spacing\":16,\"editor-zone-count\":3}],\"custom-zone-sets\":[]}";
            File.WriteAllText(_zoneSettingsPath, zoneSettings);
        }

        void CreateInvalidSettingsFile()
        {
            string zoneSettings = "{\"app-zone-history\":[{\"app-path\":\"C:\\Program Files (x86)\\Microsoft Visual Studio\\testhost.exe\",\"zone-index\":3,\"device-id\":\"wrong-device-id\",\"zoneset-uuid\":\"{D13ABB6D-invalid-uuid-C8C0836D99CC}\"}],\"devices\":[{\"device-id\":\"DELA026#5&10a58c63&0&UID16777488_1920_1200_{39B25DD2-130D-4B5D-8851-4791D66B1539}\",\"active-zoneset\":{\"uuid\":\"{D13ABB6D-7721-4176-9647-C8C0836D99CC}\",\"type\":\"columns\"},\"editor-show-spacing\":true,\"editor-spacing\":16,\"editor-zone-count\":3}],\"custom-zone-sets\":[]}";
            File.WriteAllText(_zoneSettingsPath, zoneSettings);
        }

        [TestMethod]
        public void OpenEditorBySettingsButtonNoSettings()
        {
            RemoveSettingsFile();
            OpenFancyZonesSettings();

            WindowsElement editorButton = session.FindElementByXPath("//Button[@Name=\"Edit zones\"]");
            Assert.IsNotNull(editorButton);

            editorButton.Click();
            ShortWait();

            editorWindow = session.FindElementByXPath("//Window[@Name=\"FancyZones Editor\"]");
            Assert.IsNotNull(editorWindow);
        }

        [TestMethod]
        public void OpenEditorBySettingsButtonEmptySettings()
        {
            CreateEmptySettingsFile();
            OpenFancyZonesSettings();

            WindowsElement editorButton = session.FindElementByXPath("//Button[@Name=\"Edit zones\"]");
            Assert.IsNotNull(editorButton);

            editorButton.Click();
            ShortWait();

            editorWindow = session.FindElementByXPath("//Window[@Name=\"FancyZones Editor\"]");
            Assert.IsNotNull(editorWindow);
        }

        [TestMethod]
        public void OpenEditorBySettingsButtonDefaultSettings()
        {
            CreateDefaultSettingsFile();
            OpenFancyZonesSettings();

            WindowsElement editorButton = session.FindElementByXPath("//Button[@Name=\"Edit zones\"]");
            Assert.IsNotNull(editorButton);

            editorButton.Click();
            ShortWait();

            editorWindow = session.FindElementByXPath("//Window[@Name=\"FancyZones Editor\"]");
            Assert.IsNotNull(editorWindow);
        }

        [TestMethod]
        public void OpenEditorBySettingsButtonValidSettings()
        {
            CreateValidSettingsFile();
            OpenFancyZonesSettings();

            WindowsElement editorButton = session.FindElementByXPath("//Button[@Name=\"Edit zones\"]");
            Assert.IsNotNull(editorButton);

            editorButton.Click();
            ShortWait();

            editorWindow = session.FindElementByXPath("//Window[@Name=\"FancyZones Editor\"]");
            Assert.IsNotNull(editorWindow);
        }

        [TestMethod]
        public void OpenEditorBySettingsButtonValidUtf8Settings()
        {
            CreateValidSettingsFileWithUtf8();
            OpenFancyZonesSettings();

            WindowsElement editorButton = session.FindElementByXPath("//Button[@Name=\"Edit zones\"]");
            Assert.IsNotNull(editorButton);

            editorButton.Click();
            ShortWait();

            editorWindow = session.FindElementByXPath("//Window[@Name=\"FancyZones Editor\"]");
            Assert.IsNotNull(editorWindow);
        }

        [TestMethod]
        public void OpenEditorBySettingsButtonInvalidSettings()
        {
            CreateInvalidSettingsFile();
            OpenFancyZonesSettings();

            WindowsElement editorButton = session.FindElementByXPath("//Button[@Name=\"Edit zones\"]");
            Assert.IsNotNull(editorButton);

            editorButton.Click();
            ShortWait();

            editorWindow = session.FindElementByXPath("//Window[@Name=\"FancyZones Editor\"]");
            Assert.IsNotNull(editorWindow);
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context);
            OpenSettings();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            CloseSettings();
            TearDown();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            
        }

        [TestCleanup]
        public void TestCleanup()
        {
            //Close editor
            if (editorWindow != null)
            {
                editorWindow.SendKeys(OpenQA.Selenium.Keys.Alt + OpenQA.Selenium.Keys.F4);
            }
        }
    }
}