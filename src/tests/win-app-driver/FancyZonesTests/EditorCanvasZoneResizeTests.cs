using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using System.Windows.Forms;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorCanvasZoneResizeTests : FancyZonesEditor
    {
        private void MoveCorner(WindowsElement corner, bool shiftLeft, bool shiftUp, int xOffset, int yOffset)
        {
            int shiftX = shiftLeft ? -(corner.Rect.Width / 2) + 1 : (corner.Rect.Width / 2) - 1;
            int shiftY = shiftUp ? -(corner.Rect.Height / 2) + 1 : (corner.Rect.Height / 2) - 1;

            new Actions(session).MoveToElement(corner)
                .MoveByOffset(shiftX, shiftY)
                .ClickAndHold().MoveByOffset(xOffset, yOffset).Release().Perform();
        }

        [TestMethod]
        public void MoveTopBorder()
        {
            WindowsElement topBorder = session.FindElementByAccessibilityId("NResize");
            WindowsElement bottomBorder = session.FindElementByAccessibilityId("SResize");
            Assert.IsNotNull(topBorder);
            Assert.IsNotNull(bottomBorder);

            int height = bottomBorder.Rect.Y - topBorder.Rect.Y;

            //up
            new Actions(session).MoveToElement(topBorder).ClickAndHold().MoveByOffset(0, -5000).Release().Perform();
            Assert.IsTrue(topBorder.Rect.Y >= 0);
            Assert.IsTrue(height < bottomBorder.Rect.Y - topBorder.Rect.Y);
            height = bottomBorder.Rect.Y - topBorder.Rect.Y;

            //down
            new Actions(session).MoveToElement(topBorder).ClickAndHold().MoveByOffset(0, 5000).Release().Perform();
            Assert.IsTrue(topBorder.Rect.Y <= bottomBorder.Rect.Y);
            Assert.IsTrue(height > bottomBorder.Rect.Y - topBorder.Rect.Y);
        }

        [TestMethod]
        public void MoveBottomBorder()
        {
            WindowsElement topBorder = session.FindElementByAccessibilityId("NResize");
            WindowsElement bottomBorder = session.FindElementByAccessibilityId("SResize");
            Assert.IsNotNull(topBorder);
            Assert.IsNotNull(bottomBorder);

            int height = bottomBorder.Rect.Y - topBorder.Rect.Y;

            //up
            new Actions(session).MoveToElement(bottomBorder).ClickAndHold().MoveByOffset(0, -5000).Release().Perform();
            Assert.IsTrue(topBorder.Rect.Y <= bottomBorder.Rect.Y);
            Assert.IsTrue(height > bottomBorder.Rect.Y - topBorder.Rect.Y);
            height = bottomBorder.Rect.Y - topBorder.Rect.Y;

            //down
            new Actions(session).MoveToElement(bottomBorder).ClickAndHold().MoveByOffset(0, 5000).Release().Perform();
            Assert.IsTrue(bottomBorder.Rect.Y <= Screen.PrimaryScreen.WorkingArea.Bottom);
            Assert.IsTrue(height < bottomBorder.Rect.Y - topBorder.Rect.Y);
        }

        [TestMethod]
        public void MoveLeftBorder()
        {
            WindowsElement leftBorder = session.FindElementByAccessibilityId("WResize");
            WindowsElement rightBorder = session.FindElementByAccessibilityId("EResize");
            Assert.IsNotNull(leftBorder);
            Assert.IsNotNull(rightBorder);

            int width = rightBorder.Rect.X - leftBorder.Rect.X;

            //to the left
            new Actions(session).MoveToElement(leftBorder).ClickAndHold().MoveByOffset(-5000, 0).Release().Perform();
            Assert.IsTrue(leftBorder.Rect.Y <= Screen.PrimaryScreen.WorkingArea.Bottom);
            Assert.IsTrue(width < rightBorder.Rect.X - leftBorder.Rect.X);
            width = rightBorder.Rect.X - leftBorder.Rect.X;

            //to the right
            new Actions(session).MoveToElement(leftBorder).ClickAndHold().MoveByOffset(5000, 0).Release().Perform();
            Assert.IsTrue(leftBorder.Rect.X <= rightBorder.Rect.X);
            Assert.IsTrue(width > rightBorder.Rect.X - leftBorder.Rect.X);
        }

        [TestMethod]
        public void MoveRightBorder()
        {
            WindowsElement leftBorder = session.FindElementByAccessibilityId("WResize");
            WindowsElement rightBorder = session.FindElementByAccessibilityId("EResize");
            Assert.IsNotNull(leftBorder);
            Assert.IsNotNull(rightBorder);

            int width = rightBorder.Rect.X - leftBorder.Rect.X;

            //to the left
            new Actions(session).MoveToElement(rightBorder).ClickAndHold().MoveByOffset(-5000, 0).Release().Perform();
            Assert.IsTrue(leftBorder.Rect.X <= rightBorder.Rect.X);
            Assert.IsTrue(width > rightBorder.Rect.X - leftBorder.Rect.X);
            width = rightBorder.Rect.X - leftBorder.Rect.X;

            //to the right
            new Actions(session).MoveToElement(rightBorder).ClickAndHold().MoveByOffset(5000, 0).Release().Perform();
            Assert.IsTrue(leftBorder.Rect.X <= Screen.PrimaryScreen.WorkingArea.Right);
            Assert.IsTrue(width < rightBorder.Rect.X - leftBorder.Rect.X);
        }

        [TestMethod]
        public void MoveTopLeftCorner()
        {
            WindowsElement topLeftCorner = session.FindElementByAccessibilityId("NWResize");
            WindowsElement bottomBorder = session.FindElementByAccessibilityId("SResize");
            WindowsElement rightBorder = session.FindElementByAccessibilityId("EResize");
            Assert.IsNotNull(topLeftCorner);
            Assert.IsNotNull(bottomBorder);
            Assert.IsNotNull(rightBorder);

            int expectedWidth = rightBorder.Rect.X - topLeftCorner.Rect.X;
            int expectedHeight = bottomBorder.Rect.Y - topLeftCorner.Rect.Y;
            int actualWidth, actualHeight;

            //up-left
            MoveCorner(topLeftCorner, true, true, -5000, -5000);
            actualHeight = bottomBorder.Rect.Y - topLeftCorner.Rect.Y;
            actualWidth = rightBorder.Rect.X - topLeftCorner.Rect.X;

            Assert.IsTrue(topLeftCorner.Rect.Y >= 0);
            Assert.IsTrue(topLeftCorner.Rect.X >= 0);
            Assert.IsTrue(actualHeight > expectedHeight);
            Assert.IsTrue(actualWidth > expectedWidth);

            expectedHeight = actualHeight;
            expectedWidth = actualWidth;

            //down-right
            MoveCorner(topLeftCorner, true, true, 5000, 5000);
            actualHeight = bottomBorder.Rect.Y - topLeftCorner.Rect.Y;
            actualWidth = rightBorder.Rect.X - topLeftCorner.Rect.X;

            Assert.IsTrue(topLeftCorner.Rect.Y <= bottomBorder.Rect.Y);
            Assert.IsTrue(topLeftCorner.Rect.X <= rightBorder.Rect.X);
            Assert.IsTrue(actualHeight < expectedHeight);
            Assert.IsTrue(actualWidth < expectedWidth);
        }

        [TestMethod]
        public void MoveTopRightCorner()
        {
            WindowsElement topRightCorner = session.FindElementByAccessibilityId("NEResize");
            WindowsElement bottomBorder = session.FindElementByAccessibilityId("SResize");
            WindowsElement leftBorder = session.FindElementByAccessibilityId("WResize");
            Assert.IsNotNull(topRightCorner);
            Assert.IsNotNull(bottomBorder);
            Assert.IsNotNull(leftBorder);

            int expectedWidth = topRightCorner.Rect.X - leftBorder.Rect.X;
            int expectedHeight = bottomBorder.Rect.Y - topRightCorner.Rect.Y;
            int actualWidth, actualHeight;

            //up-right
            MoveCorner(topRightCorner, false, true, 5000, -5000);
            actualHeight = bottomBorder.Rect.Y - topRightCorner.Rect.Y;
            actualWidth = topRightCorner.Rect.X - leftBorder.Rect.X;

            Assert.IsTrue(topRightCorner.Rect.Y >= 0);
            Assert.IsTrue(leftBorder.Rect.X <= Screen.PrimaryScreen.WorkingArea.Right);
            Assert.IsTrue(actualHeight > expectedHeight);
            Assert.IsTrue(actualWidth > expectedWidth);

            expectedHeight = actualHeight;
            expectedWidth = actualWidth;

            //down-left
            MoveCorner(topRightCorner, false, true, -5000, 5000);
            actualHeight = bottomBorder.Rect.Y - topRightCorner.Rect.Y;
            actualWidth = topRightCorner.Rect.X - leftBorder.Rect.X;

            Assert.IsTrue(topRightCorner.Rect.Y <= bottomBorder.Rect.Y);
            Assert.IsTrue(topRightCorner.Rect.X >= leftBorder.Rect.X);
            Assert.IsTrue(actualHeight < expectedHeight);
            Assert.IsTrue(actualWidth < expectedWidth);
        }

        [TestMethod]
        public void MoveBottomLeftCorner()
        {
            WindowsElement bottomLeftCorner = session.FindElementByAccessibilityId("SWResize");
            WindowsElement topBorder = session.FindElementByAccessibilityId("NResize");
            WindowsElement rightBorder = session.FindElementByAccessibilityId("EResize");
            Assert.IsNotNull(bottomLeftCorner);
            Assert.IsNotNull(topBorder);
            Assert.IsNotNull(rightBorder);

            int expectedWidth = rightBorder.Rect.X - bottomLeftCorner.Rect.X;
            int expectedHeight = bottomLeftCorner.Rect.Y - topBorder.Rect.Y;
            int actualWidth, actualHeight;

            //up-left
            MoveCorner(bottomLeftCorner, true, false, 5000, -5000);
            actualHeight = bottomLeftCorner.Rect.Y - topBorder.Rect.Y;
            actualWidth = rightBorder.Rect.X - bottomLeftCorner.Rect.X;

            Assert.IsTrue(bottomLeftCorner.Rect.Y >= topBorder.Rect.Y);
            Assert.IsTrue(bottomLeftCorner.Rect.X <= rightBorder.Rect.X);
            Assert.IsTrue(actualHeight < expectedHeight);
            Assert.IsTrue(actualWidth < expectedWidth);

            expectedHeight = actualHeight;
            expectedWidth = actualWidth;

            //down-right
            MoveCorner(bottomLeftCorner, true, false, -5000, 5000);
            actualHeight = bottomLeftCorner.Rect.Y - topBorder.Rect.Y;
            actualWidth = rightBorder.Rect.X - bottomLeftCorner.Rect.X;

            Assert.IsTrue(bottomLeftCorner.Rect.Y <= Screen.PrimaryScreen.WorkingArea.Bottom);
            Assert.IsTrue(bottomLeftCorner.Rect.X >= 0);
            Assert.IsTrue(actualHeight > expectedHeight);
            Assert.IsTrue(actualWidth > expectedWidth);
        }

        [TestMethod]
        public void MoveBottomRightCorner()
        {
            WindowsElement zone = session.FindElementByAccessibilityId("Caption");
            Assert.IsNotNull(zone, "Unable to move zone");
            new Actions(session).MoveToElement(zone).ClickAndHold().MoveByOffset(creatorWindow.Rect.Width / 2, 0).Release().Perform();
            WindowsElement bottomRightCorner = session.FindElementByAccessibilityId("SEResize");
            WindowsElement topBorder = session.FindElementByAccessibilityId("NResize");
            WindowsElement leftBorder = session.FindElementByAccessibilityId("WResize");
            Assert.IsNotNull(bottomRightCorner);
            Assert.IsNotNull(topBorder);
            Assert.IsNotNull(leftBorder);

            int expectedWidth = bottomRightCorner.Rect.X - leftBorder.Rect.X;
            int expectedHeight = bottomRightCorner.Rect.Y - topBorder.Rect.Y;
            int actualWidth, actualHeight;

            //up-left
            MoveCorner(bottomRightCorner, false, false, -5000, -5000);
            actualHeight = bottomRightCorner.Rect.Y - topBorder.Rect.Y;
            actualWidth = bottomRightCorner.Rect.X - leftBorder.Rect.X;

            Assert.IsTrue(bottomRightCorner.Rect.Y >= topBorder.Rect.Y);
            Assert.IsTrue(bottomRightCorner.Rect.X >= leftBorder.Rect.X);
            Assert.IsTrue(actualHeight < expectedHeight);
            Assert.IsTrue(actualWidth < expectedWidth);

            expectedHeight = actualHeight;
            expectedWidth = actualWidth;

            //down-right
            MoveCorner(bottomRightCorner, false, false, 5000, 5000);
            actualHeight = bottomRightCorner.Rect.Y - topBorder.Rect.Y;
            actualWidth = bottomRightCorner.Rect.X - leftBorder.Rect.X;

            Assert.IsTrue(bottomRightCorner.Rect.Y <= Screen.PrimaryScreen.WorkingArea.Bottom);
            Assert.IsTrue(bottomRightCorner.Rect.X <= Screen.PrimaryScreen.WorkingArea.Right);
            Assert.IsTrue(actualHeight > expectedHeight);
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context);
            Assert.IsNotNull(session);

            EnableModules(false, true, false, false, false, false, false, false);
            ResetSettings();

            Assert.IsTrue(OpenEditor());
            OpenCustomLayouts();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            CloseEditor();
            ExitPowerToys();
            TearDown();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            //create canvas zone
            OpenCreatorWindow("Create new custom");
            creatorWindow.FindElementByAccessibilityId("newZoneButton").Click();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            AppiumWebElement cancelButton = creatorWindow.FindElementByName("Cancel");
            Assert.IsNotNull(cancelButton);
            new Actions(session).MoveToElement(cancelButton).Click().Perform();
        }
    }
}