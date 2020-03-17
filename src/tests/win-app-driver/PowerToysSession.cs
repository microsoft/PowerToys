using System;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium;

namespace PowerToysTests
{
    public class PowerToysSession
    {
        protected const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";
        protected static WindowsDriver<WindowsElement> session;
        protected static bool isPowerToysLaunched = false;
        protected static WindowsElement trayButton;

        protected static string _settingsPath = "";
        protected static string _zoneSettingsPath = "";
        protected static string _initialSettings = "";
        protected static string _initialZoneSettings = "";

        public static void Setup(TestContext context)
        {
            //read settings before running tests to restore them after
            _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft/PowerToys/FancyZones/settings.json");
            if (Directory.Exists(_settingsPath))
            {
                _initialSettings = File.ReadAllText(_settingsPath);
            }

            _zoneSettingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft/PowerToys/FancyZones/zones-settings.json");
            if (Directory.Exists(_zoneSettingsPath))
            {
                _initialZoneSettings = File.ReadAllText(_zoneSettingsPath);
            }

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
                if (!isPowerToysLaunched)
                {
                    LaunchPowerToys();
                }
            }

        }

        public static void TearDown()
        {
            if (session!=null)
            {
                session.Quit();
                session = null;
            }

            //restore initial settings files
            if (_initialSettings.Length > 0)
            {
                File.WriteAllText(_settingsPath, _initialSettings);
            }

            if (_initialZoneSettings.Length > 0)
            {
                File.WriteAllText(_zoneSettingsPath, _initialZoneSettings);
            }
        }

        public static void WaitSeconds(int seconds)
        {
            Thread.Sleep(TimeSpan.FromSeconds(seconds));
        }

        public static void ShortWait()
        {
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
        }

        public static void OpenSettings()
        {
            trayButton.Click();
            session.FindElementByName("PowerToys").Click();
            trayButton.Click();
        }

        public static void OpenFancyZonesSettings()
        {
            WindowsElement fzNavigationButton = session.FindElementByXPath("//Button[@Name=\"FancyZones\"]");
            Assert.IsNotNull(fzNavigationButton);

            fzNavigationButton.Click();
            fzNavigationButton.Click();

            ShortWait();
        }

        public static void CloseSettings()
        {
            WindowsElement settings = session.FindElementByName("PowerToys Settings");
            settings.SendKeys(Keys.Alt + Keys.F4);
        }

        private static bool CheckPowerToysLaunched()        
        {
            trayButton.Click();
            bool isLaunched = false;

            try
            {
                WindowsElement pt = session.FindElementByName("PowerToys");
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
                opts.AddAdditionalCapability("app", "Microsoft.PowerToys_8wekyb3d8bbwe!PowerToys");
                
                WindowsDriver<WindowsElement> driver = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), opts);
                Assert.IsNotNull(driver);
                driver.LaunchApp();
            }
            catch (OpenQA.Selenium.WebDriverException)
            {
                //exception could be thrown even if app launched successfully
            }
        }
    }
}
