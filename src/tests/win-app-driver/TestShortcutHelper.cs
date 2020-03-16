using System;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    [TestClass]
    public class TestShortcutHelper : PowerToysSession
    {
        // Try to manage Press/Release of Windows Key here,
        // since Keyboard.PressKey seems to release the key if pressed
        // and Keyboard.ReleaseKey seems to press the key if not pressed.
        private bool isWinKeyPressed;

        private void PressWinKey()
        {
            if(!isWinKeyPressed)
            {
                new Actions(session).KeyDown(OpenQA.Selenium.Keys.Command).Perform();
                isWinKeyPressed = true;
            }
        }

        private void ReleaseWinKey()
        {
            if(isWinKeyPressed)
            {
                new Actions(session).KeyUp(OpenQA.Selenium.Keys.Command).Perform();
                isWinKeyPressed = false;
            }
        }

        [TestMethod]
        public void AppearsOnWinKeyPress()
        {
            PressWinKey();
            Thread.Sleep(TimeSpan.FromSeconds(2));
            WindowsElement shortcutHelperWindow = session.FindElementByXPath("/Pane[@ClassName=\"#32769\"]/Pane[@ClassName=\"PToyD2DPopup\"]");
            Assert.IsNotNull(shortcutHelperWindow);
            ReleaseWinKey();
            Thread.Sleep(TimeSpan.FromSeconds(2));
        }
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException),
            "The Shortcut Guide UI was still found after releasing the key.")]
        public void DisappearsOnWinKeyRelease()
        {
            PressWinKey();
            Thread.Sleep(TimeSpan.FromSeconds(2));
            WindowsElement shortcutHelperWindow;
            try
            {
                shortcutHelperWindow = session.FindElementByXPath("/Pane[@ClassName=\"#32769\"]/Pane[@ClassName=\"PToyD2DPopup\"]");
                Assert.IsNotNull(shortcutHelperWindow);
            }
            catch (InvalidOperationException)
            {
                // Not the exception we wanted to catch here.
                Assert.Fail("Shortcut Guide not found");
            }
            ReleaseWinKey();
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
            shortcutHelperWindow = session.FindElementByXPath("/Pane[@ClassName=\"#32769\"]/Pane[@ClassName=\"PToyD2DPopup\"]");
        }
        [TestMethod]
        public void DoesNotBlockStartMenuOnShortPress()
        {
            PressWinKey();
            Thread.Sleep(TimeSpan.FromSeconds(0.4));
            // FindElementByClassName will be faster than using with XPath.
            WindowsElement shortcutHelperWindow = session.FindElementByClassName("PToyD2DPopup");
            Assert.IsNotNull(shortcutHelperWindow);
            ReleaseWinKey();
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
            WindowsElement startMenuWindow = session.FindElementByXPath("/Pane[@ClassName=\"#32769\"]/Window[@Name=\"Start\"]");
        }
        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException),
            "The Start Menu was found after releasing the key on a long press.")]
        public void DoesNotSpawnStartMenuOnLongPress()
        {
            PressWinKey();
            Thread.Sleep(TimeSpan.FromSeconds(2));
            try
            {
                // FindElementByClassName will be faster than using with XPath.
                WindowsElement shortcutHelperWindow = session.FindElementByClassName("PToyD2DPopup");
                Assert.IsNotNull(shortcutHelperWindow);
            } catch (InvalidOperationException)
            {
                // Not the exception we wanted to catch here.
                Assert.Fail("Shortcut Guide not found");
            }
            ReleaseWinKey();
            Thread.Sleep(TimeSpan.FromSeconds(0.5));
            WindowsElement startMenuWindow = session.FindElementByXPath("/Pane[@ClassName=\"#32769\"]/Window[@Name=\"Start\"]");
        }
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context);
        }
        [ClassCleanup]
        public static void ClassCleanup()
        {
            TearDown();
        }
        [TestInitialize]
        public void TestInitialize()
        {
            isWinKeyPressed = false;

            // If the start menu is open, close it.
            WindowsElement startMenuWindow = null;
            try
            {
                startMenuWindow = session.FindElementByXPath("/Pane[@ClassName=\"#32769\"]/Window[@Name=\"Start\"]");
            } catch (Exception)
            {
                //Start menu not found, as expected.
            }
            if (startMenuWindow!=null)
            {
                string startMenuWindowHandle = (int.Parse(startMenuWindow.GetAttribute("NativeWindowHandle"))).ToString("x");
                // Create session for controlling the Start Menu.
                AppiumOptions appiumOptions = new AppiumOptions();
                appiumOptions.PlatformName = "Windows";
                appiumOptions.AddAdditionalCapability("appTopLevelWindow", startMenuWindowHandle);
                WindowsDriver<WindowsElement> startMenuSession = new WindowsDriver<WindowsElement>(new Uri(WindowsApplicationDriverUrl), appiumOptions);
                if (startMenuSession != null)
                {
                    new Actions(session).SendKeys(OpenQA.Selenium.Keys.Escape + OpenQA.Selenium.Keys.Escape).Perform();
                    startMenuSession.Quit();
                }
            }
        }
        [TestCleanup]
        public void TestCleanup()
        {
            // Release Windows Key in case it's being pressed by some of the tests
            ReleaseWinKey();
        }
    }
}
