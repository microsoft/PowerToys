using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    public class PowerToysSession
    {
        protected const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";
        protected const string AppPath = "C:\\Program Files\\PowerToys\\PowerToys.exe";
        
        protected static WindowsDriver<WindowsElement> session;
        protected static bool isPowerToysLaunched = false;
        protected static WindowsElement trayButton;

        protected static string _settingsFolderPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft\\PowerToys\\FancyZones");
        protected static string _settingsPath = _settingsFolderPath + "\\settings.json"; 
        protected static string _zoneSettingsPath = _settingsFolderPath + "\\zones-settings.json";

        protected static string _initialSettings = "";
        protected static string _initialZoneSettings = "";

        protected const string _defaultSettings = "{\"version\":\"1.0\",\"name\":\"FancyZones\",\"properties\":{\"fancyzones_shiftDrag\":{\"value\":true},\"fancyzones_overrideSnapHotkeys\":{\"value\":false},\"fancyzones_zoneSetChange_flashZones\":{\"value\":false},\"fancyzones_displayChange_moveWindows\":{\"value\":false},\"fancyzones_zoneSetChange_moveWindows\":{\"value\":false},\"fancyzones_virtualDesktopChange_moveWindows\":{\"value\":false},\"fancyzones_appLastZone_moveWindows\":{\"value\":false},\"use_cursorpos_editor_startupscreen\":{\"value\":true},\"fancyzones_zoneHighlightColor\":{\"value\":\"#0078D7\"},\"fancyzones_highlight_opacity\":{\"value\":90},\"fancyzones_editor_hotkey\":{\"value\":{\"win\":true,\"ctrl\":false,\"alt\":false,\"shift\":false,\"code\":192,\"key\":\"`\"}},\"fancyzones_excluded_apps\":{\"value\":\"\"}}}";
        protected const string _defaultZoneSettings = "{\"app-zone-history\":[],\"devices\":[],\"custom-zone-sets\":[]}";
            

        public static void Setup(TestContext context, bool isLaunchRequired = true)
        {
            ReadUserSettings(); //read settings before running tests to restore them after

            if (session == null)
            {
                // Create a new Desktop session to use PowerToys.
                AppiumOptions appiumOptions = new AppiumOptions();
                appiumOptions.PlatformName = "Windows";
                appiumOptions.AddAdditionalCapability("app", "Root");
                session = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), appiumOptions);
                Assert.IsNotNull(session);

                trayButton = session.FindElementByAccessibilityId("1502");

                isPowerToysLaunched = CheckPowerToysLaunched();
                if (!isPowerToysLaunched && isLaunchRequired)
                {
                    LaunchPowerToys();
                }
            }
        }

        public static void TearDown()
        {
            RestoreUserSettings(); //restore initial settings files

            if (session != null)
            {
                session.Quit();
                session = null;
            }
        }

        public static void WaitSeconds(double seconds)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
        }

        //Trying to find element by XPath
        protected static WindowsElement WaitElementByName(string name, double maxTime = 10)
        {
            WindowsElement result = null;
            Stopwatch timer = new Stopwatch();
            timer.Start();
            while (timer.Elapsed < TimeSpan.FromSeconds(maxTime))
            {
                try
                {
                    result = session.FindElementByName(name);
                }
                catch { }
                return result;
            }
            return null;
        }

        //Trying to find element by XPath
        protected static WindowsElement WaitElementByXPath(string xPath, double maxTime = 10)
        {
            WindowsElement result = null;
            Stopwatch timer = new Stopwatch();
            timer.Start();
            while (timer.Elapsed < TimeSpan.FromSeconds(maxTime))
            {
                try
                {
                    result = session.FindElementByXPath(xPath);
                }
                catch { }
                return result;
            }
            return null;
        }

        //Trying to find element by AccessibilityId
        protected static WindowsElement WaitElementByAccessibilityId(string accessibilityId, double maxTime = 10)
        {
            WindowsElement result = null;
            Stopwatch timer = new Stopwatch();
            timer.Start();
            while (timer.Elapsed < TimeSpan.FromSeconds(maxTime))
            {
                try
                {
                    result = session.FindElementByAccessibilityId(accessibilityId);
                }
                catch { }
                return result;
            }
            return null;
        }

        public static void OpenSettings()
        {
            trayButton.Click();
            session.FindElementByXPath("//Button[@Name=\"PowerToys\"]").Click();
            trayButton.Click();
        }

        public static void OpenFancyZonesSettings()
        {
            WindowsElement fzNavigationButton = WaitElementByXPath("//Button[@Name=\"FancyZones\"]");
            Assert.IsNotNull(fzNavigationButton);

            fzNavigationButton.Click();
            fzNavigationButton.Click();
        }

        public static void CloseSettings()
        {
            try
            {
                WindowsElement settings = session.FindElementByName("PowerToys Settings");
                if (settings != null)
                {
                    settings.SendKeys(Keys.Alt + Keys.F4);
                }
            }
            catch(Exception)
            {

            }
        }

        private static bool CheckPowerToysLaunched()        
        {
            trayButton.Click();
            bool isLaunched = false;

            try
            {
                WindowsElement pt = session.FindElementByXPath("//Button[@Name=\"PowerToys\"]");
                isLaunched = (pt != null);
            }
            catch(OpenQA.Selenium.WebDriverException)
            {
                //PowerToys not found
            }

            trayButton.Click(); //close
            return isLaunched;
        }

        public static void LaunchPowerToys()
        {
            try
            {
                AppiumOptions opts = new AppiumOptions();
                opts.PlatformName = "Windows";
                opts.AddAdditionalCapability("app", AppPath);

                WindowsDriver<WindowsElement> driver = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), opts);
                Assert.IsNotNull(driver);
                driver.LaunchApp();
            }
            catch (OpenQA.Selenium.WebDriverException ex)
            {
                Console.WriteLine("Exception on PowerToys launch:" + ex.Message);
                //exception could be thrown even if app launched successfully
            }

            isPowerToysLaunched = true;
        }

        public static void ExitPowerToys()
        {
            trayButton.Click();

            WindowsElement pt = WaitElementByXPath("//Button[@Name=\"PowerToys\"]");
            Assert.IsNotNull(pt, "Couldn't find \'PowerToys\' button");
            new Actions(session).MoveToElement(pt).ContextClick().Perform();
            
            WaitElementByXPath("//MenuItem[@Name=\"Exit\"]").Click();
            trayButton.Click(); //close tray
            isPowerToysLaunched = false;
        }

        public static void ResetDefaultFancyZonesSettings(bool relaunch)
        {
            ResetSettings(_settingsFolderPath, _settingsPath, _defaultSettings, relaunch);
        }

        public static void ResetDefautZoneSettings(bool relaunch)
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

            if (isPowerToysLaunched)
            {
                ExitPowerToys();
            }

            if (relaunch)
            {
                LaunchPowerToys();
            }
        }

        private static void ReadUserSettings()
        {
            try
            {
                _initialSettings = File.ReadAllText(_settingsPath);
            }
            catch (Exception)
            {
                //failed to read settings
            }

            try
            {
                _initialZoneSettings = File.ReadAllText(_zoneSettingsPath);
            }
            catch (Exception)
            {
                //failed to read settings
            }
        }

        private static void RestoreUserSettings()
        {
            if (_initialSettings.Length > 0)
            {
                File.WriteAllText(_settingsPath, _initialSettings);
            }
            else
            {
                File.Delete(_settingsPath);
            }

            if (_initialZoneSettings.Length > 0)
            {
                File.WriteAllText(_zoneSettingsPath, _initialZoneSettings);
            }
            else
            {
                File.Delete(_zoneSettingsPath);
            }
        }
    }
}
