using System;
using System.IO.Abstractions;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    public class PowerToysSession
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;
        private static readonly IDirectory Directory = FileSystem.Directory;

        protected const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";
        protected const string AppPath = "C:\\Program Files\\PowerToys\\PowerToys.exe";

        protected static WindowsDriver<WindowsElement> session;
        protected static bool isPowerToysLaunched = false;

        protected static WindowsElement trayButton;
        protected static WindowsElement settingsWindow;

        protected static string _commonSettingsFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\PowerToys");
        protected static string _settingsFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\PowerToys\\FancyZones");
        protected static string _fancyZonesSettingsPath = _settingsFolderPath + "\\settings.json";
        protected static string _zoneSettingsPath = _settingsFolderPath + "\\zones-settings.json";
        protected static string _appHistoryPath = _settingsFolderPath + "\\app-zone-history.json";
        protected static string _commonSettingsPath = _commonSettingsFolderPath + "\\settings.json";

        protected static string _initialFancyZonesSettings = "";
        protected static string _initialZoneSettings = "";
        protected static string _initialAppHistorySettings = "";
        protected static string _initialCommonSettings = "";

        protected const string _defaultFancyZonesSettings = "{\"version\":\"1.0\",\"name\":\"FancyZones\",\"properties\":{\"fancyzones_shiftDrag\":{\"value\":true},\"fancyzones_mouseSwitch\":{\"value\":false},\"fancyzones_overrideSnapHotkeys\":{\"value\":false},\"fancyzones_moveWindowAcrossMonitors\":{\"value\":false},\"fancyzones_zoneSetChange_flashZones\":{\"value\":false},\"fancyzones_displayChange_moveWindows\":{\"value\":false},\"fancyzones_zoneSetChange_moveWindows\":{\"value\":false},\"fancyzones_appLastZone_moveWindows\":{\"value\":false},\"use_cursorpos_editor_startupscreen\":{\"value\":true},\"fancyzones_zoneHighlightColor\":{\"value\":\"#0078D7\"},\"fancyzones_highlight_opacity\":{\"value\":90},\"fancyzones_editor_hotkey\":{\"value\":{\"win\":true,\"ctrl\":false,\"alt\":false,\"shift\":false,\"code\":192,\"key\":\"`\"}},\"fancyzones_excluded_apps\":{\"value\":\"\"}}}";
        protected const string _defaultZoneSettings = "{\"devices\":[],\"custom-zone-sets\":[]}";


        public static void Setup(TestContext context)
        {
            if (session == null)
            {
                ReadUserSettings(); //read settings before running tests to restore them after

                // Create a new Desktop session to use PowerToys.
                AppiumOptions appiumOptions = new AppiumOptions();
                appiumOptions.PlatformName = "Windows";
                appiumOptions.AddAdditionalCapability("app", "Root");
                try
                {
                    session = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), appiumOptions);
                    session.Manage().Timeouts().ImplicitWait = TimeSpan.FromSeconds(1);

                    trayButton = session.FindElementByAccessibilityId("1502");

                    isPowerToysLaunched = CheckPowerToysLaunched();
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        public static void TearDown()
        {
            RestoreUserSettings(); //restore initial settings files

            if (session != null)
            {
                trayButton = null;
                settingsWindow = null;

                session.Quit();
                session = null;
            }
        }

        public static void WaitSeconds(double seconds)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
        }

        public static void OpenSettings()
        {
            trayButton.Click();

            try
            {
                PowerToysTrayButton().Click();
                settingsWindow = session.FindElementByName("PowerToys Settings");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            trayButton.Click(); //close
            Assert.IsNotNull(settingsWindow);
        }

        public static void OpenFancyZonesSettings()
        {
            try
            {
                AppiumWebElement fzNavigationButton = settingsWindow.FindElementByName("FancyZones");
                Assert.IsNotNull(fzNavigationButton);

                fzNavigationButton.Click();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        public static void CloseSettings()
        {
            try
            {
                WindowsElement settings = session.FindElementByName("PowerToys Settings");
                if (settings != null)
                {
                    settings.Click();
                    settings.FindElementByName("Close").Click();
                    //settings.SendKeys(Keys.Alt + Keys.F4);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        protected static AppiumWebElement PowerToysTrayButton()
        {
            WindowsElement notificationOverflow = session.FindElementByName("Notification Overflow");
            AppiumWebElement overflowArea = notificationOverflow.FindElementByName("Overflow Notification Area");
            AppiumWebElement powerToys = overflowArea.FindElementByXPath("//Button[contains(@Name, \"PowerToys\")]");
            return powerToys;
        }

        private static bool CheckPowerToysLaunched()
        {
            bool isLaunched = false;
            trayButton.Click();

            try
            {
                AppiumWebElement pt = PowerToysTrayButton();
                isLaunched = (pt != null);
            }
            catch (OpenQA.Selenium.WebDriverException)
            {
                //PowerToys not found
            }

            trayButton.Click(); //close
            return isLaunched;
        }

        public static void LaunchPowerToys()
        {
            AppiumOptions opts = new AppiumOptions();
            opts.AddAdditionalCapability("app", AppPath);

            try
            {
                WindowsDriver<WindowsElement> driver = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), opts);
                Assert.IsNotNull(driver);
                driver.LaunchApp();
            }
            catch (WebDriverException)
            {
                //exception is expected since WinApDriver tries to find main app window 
            }

            isPowerToysLaunched = true;
        }

        public static void ExitPowerToys()
        {
            trayButton.Click();

            try
            {
                AppiumWebElement pt = PowerToysTrayButton();
                Assert.IsNotNull(pt, "Could not exit PowerToys");

                new Actions(session).MoveToElement(pt).ContextClick().Perform();
                session.FindElementByAccessibilityId("40001").Click();
                //WaitElementByXPath("//MenuItem[@Name=\"Exit\"]").Click();

                isPowerToysLaunched = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            trayButton.Click(); //close tray
        }

        public static void EnableModules(bool colorPicker, bool fancyZones, bool fileExplorer, bool imageResizer, bool keyboardManager, bool powerRename, bool powerRun, bool shortcutGuide, bool relaunch = false)
        {
            JObject json = JObject.Parse(_initialCommonSettings);
            JObject enabled = new JObject();
            enabled["ColorPicker"] = colorPicker;
            enabled["FancyZones"] = fancyZones;
            enabled["File Explorer"] = fileExplorer;
            enabled["Image Resizer"] = imageResizer;
            enabled["Keyboard Manager"] = keyboardManager;
            enabled["PowerRename"] = powerRename;
            enabled["PowerToys Run"] = powerRun;
            enabled["Shortcut Guide"] = shortcutGuide;

            json["enabled"] = enabled;

            ResetSettings(_commonSettingsFolderPath, _commonSettingsPath, json.ToString(), relaunch);
        }

        public static void ResetDefaultFancyZonesSettings(bool relaunch)
        {
            ResetSettings(_settingsFolderPath, _fancyZonesSettingsPath, _defaultFancyZonesSettings, relaunch);
        }

        public static void ResetDefaultZoneSettings(bool relaunch)
        {
            ResetSettings(_settingsFolderPath, _zoneSettingsPath, _defaultZoneSettings, relaunch);
        }

        private static void ResetSettings(string folder, string filePath, string data, bool relaunch)
        {
            if (!Directory.Exists(folder))
            {
                Directory.CreateDirectory(folder);
            }
            File.WriteAllText(filePath, data);

            ExitPowerToys();
            if (relaunch)
            {
                LaunchPowerToys();
            }
        }

        private static void ReadUserSettings()
        {
            try
            {
                if (_initialCommonSettings.Length == 0)
                {
                    _initialCommonSettings = File.ReadAllText(_commonSettingsPath);
                }
            }
            catch (Exception)
            { }

            try
            {
                if (_initialFancyZonesSettings.Length == 0)
                {
                    _initialFancyZonesSettings = File.ReadAllText(_fancyZonesSettingsPath);
                }
            }
            catch (Exception)
            { }

            try
            {
                if (_initialZoneSettings.Length == 0)
                {
                    _initialZoneSettings = File.ReadAllText(_zoneSettingsPath);
                }
            }
            catch (Exception)
            { }

            try
            {
                if (_initialAppHistorySettings.Length == 0)
                {
                    _initialAppHistorySettings = File.ReadAllText(_appHistoryPath);
                }
            }
            catch (Exception)
            { }
        }

        private static void RestoreUserSettings()
        {
            if (_initialCommonSettings.Length > 0)
            {
                File.WriteAllText(_commonSettingsPath, _initialCommonSettings);
            }
            else
            {
                File.Delete(_commonSettingsPath);
            }

            if (_initialFancyZonesSettings.Length > 0)
            {
                File.WriteAllText(_fancyZonesSettingsPath, _initialFancyZonesSettings);
            }
            else
            {
                File.Delete(_fancyZonesSettingsPath);
            }

            if (_initialZoneSettings.Length > 0)
            {
                File.WriteAllText(_zoneSettingsPath, _initialZoneSettings);
            }
            else
            {
                File.Delete(_zoneSettingsPath);
            }

            if (_initialAppHistorySettings.Length > 0)
            {
                File.WriteAllText(_appHistoryPath, _initialAppHistorySettings);
            }
            else
            {
                File.Delete(_appHistoryPath);
            }
        }
    }
}
