using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Remote;
using OpenQA.Selenium;

namespace PowerToysTests
{
    public class PowerToysSession
    {
        protected const string WindowsApplicationDriverUrl = "http://127.0.0.1:4723";
        protected static WindowsDriver<WindowsElement> session;

        public static void Setup(TestContext context)
        {
            // TODO: Launch PowerToys application if it is not yet launched
            if (session == null)
            {
                // Create a new Desktop session to use PowerToys.
                AppiumOptions appiumOptions = new AppiumOptions();
                appiumOptions.PlatformName = "Windows";
                appiumOptions.AddAdditionalCapability("app", "Root");
                session = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), appiumOptions);
                Assert.IsNotNull(session);
            }

        }

        public static void TearDown()
        {
            if (session!=null)
            {
                session.Quit();
                session = null;
            }
        }

    }
}
