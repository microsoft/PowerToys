using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace PowerToysTests
{
    [TestClass]
    public class ImageResizerOpeningTwiceTest : PowerToysSession
    {
        // Import the FindWindow API to find our window
        [DllImportAttribute("User32.dll")]
        private static extern int FindWindow(string className, string windowName);
        int hWndOpen = FindWindow(null, "Image Resizer - Open files");
        int hWndImageResizer = FindWindow(null, "Image Resizer");
        private bool isTrayOpened;
        [TestMethod]
        public void OpenImageResizer()
        {
            //open tray
            trayButton.Click();
            isTrayOpened = true;

            //open PowerToys context menu
            WindowsElement pt = session.FindElementByName("PowerToys");
            Assert.IsNotNull(pt);

            new Actions(session).MoveToElement(pt).ContextClick().Perform();
            ShortWait();

            //open Image Resizer window twice
            session.FindElementByXPath("//MenuItem[@Name=\"Image Resizer\"]").Click();
            ShortWait();
            trayButton.Click();
            new Actions(session).MoveToElement(pt).ContextClick().Perform();
            ShortWait();
            session.FindElementByXPath("//MenuItem[@Name=\"Image Resizer\"]").Click();
            ShortWait();

            //check Open File Dialog window opened
            Assert.AreNotEqual(hWndOpen, 0);
            ShortWait();

            //close Open File Dialog window
            CloseWindow("Image Resizer - Open files");
            ShortWait();

            //check Open File Dialog window closed
            Assert.AreEqual(hWndOpen, 0);

            //check Image Resizer window opened
            Assert.AreNotEqual(hWndImageResizer, 0);
            ShortWait();

            //open Image Resizer window one more time = twice
            new Actions(session).MoveToElement(pt).ContextClick().Perform();
            ShortWait();
            session.FindElementByXPath("//MenuItem[@Name=\"Image Resizer\"]").Click();
            ShortWait();

            //close Image Resizer window
            CloseWindow("Image Resizer");
            ShortWait();

            //check Image Resizer Closed
            Assert.AreEqual(hWndImageResizer, 0);

            //close Image Resizer include background processes
            CloseWindowByProcessName("ImageResizer");
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

        }

        [TestCleanup]
        public void TestCleanup()
        {
            if (isTrayOpened)
            {
                trayButton.Click();
            }
        }
    }
}

