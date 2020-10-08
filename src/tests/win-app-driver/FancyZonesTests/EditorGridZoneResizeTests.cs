using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using System;
using System.Collections.ObjectModel;
using System.Windows.Forms;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesEditorGridZoneResizeTests : FancyZonesEditor
    {
        private const int moveStep = 5;

        private void Move(AppiumWebElement thumb, int border, bool moveAscending, bool moveHorizontally, int clickShift = 0)
        {
            Actions action = new Actions(session);
            action.MoveToElement(thumb).MoveByOffset(0, clickShift).ClickAndHold();

            int thumbCenter = 0;
            if (moveHorizontally)
            {
                thumbCenter = thumb.Rect.X + thumb.Rect.Width / 2;
            }
            else
            {
                thumbCenter = thumb.Rect.Y + thumb.Rect.Height / 2;
            }

            int moves = Math.Abs(thumbCenter - border) / moveStep;
            for (int j = 0; j < moves; j++)
            {
                int step = moveAscending ? moveStep : -moveStep;
                if (moveHorizontally)
                {
                    action.MoveByOffset(step, 0);
                }
                else
                {
                    action.MoveByOffset(0, step);
                }
            }

            action.Release().Perform();
        }

        [TestMethod]
        public void MoveVerticalSplitter()
        {
            OpenCreatorWindow("Columns", "EditTemplateButton");
            WindowsElement gridEditor = session.FindElementByClassName("GridEditor");
            Assert.IsNotNull(gridEditor);

            Assert.AreEqual(3, gridEditor.FindElementsByClassName("GridZone").Count);
            ReadOnlyCollection<AppiumWebElement> thumbs = gridEditor.FindElementsByClassName("Thumb");
            Assert.AreEqual(2, thumbs.Count);

            //move left
            for (int i = 0; i < thumbs.Count; i++)
            {
                AppiumWebElement thumb = thumbs[i];
                int border = i == 0 ? 0 : thumbs[i - 1].Rect.Right;
                Move(thumb, border, false, true);

                Assert.IsTrue(thumb.Rect.Left - border <= moveStep);
                Assert.IsTrue(thumb.Rect.Right > border);
            }

            //move right
            for (int i = thumbs.Count - 1; i >= 0; i--)
            {
                AppiumWebElement thumb = thumbs[i];
                int border = i == thumbs.Count - 1 ? Screen.PrimaryScreen.WorkingArea.Right : thumbs[i + 1].Rect.Left;
                Move(thumb, border, true, true);

                Assert.IsTrue(border - thumb.Rect.Right <= moveStep);
                Assert.IsTrue(thumb.Rect.Left < border);
            }

            //move up
            foreach (AppiumWebElement thumb in thumbs)
            {
                int expected = thumb.Rect.X;

                Move(thumb, 0, false, false);
                int actual = thumb.Rect.X;

                Assert.AreEqual(expected, actual);
            }

            //move down
            foreach (AppiumWebElement thumb in thumbs)
            {
                int expected = thumb.Rect.X;

                Move(thumb, Screen.PrimaryScreen.WorkingArea.Right, true, false);
                int actual = thumb.Rect.X;

                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void MoveHorizontalSplitter()
        {
            OpenCreatorWindow("Rows", "EditTemplateButton");
            WindowsElement gridEditor = session.FindElementByClassName("GridEditor");
            Assert.IsNotNull(gridEditor);

            Assert.AreEqual(3, gridEditor.FindElementsByClassName("GridZone").Count);
            ReadOnlyCollection<AppiumWebElement> thumbs = gridEditor.FindElementsByClassName("Thumb");
            Assert.AreEqual(2, thumbs.Count);

            //move up
            for (int i = 0; i < thumbs.Count; i++)
            {
                AppiumWebElement thumb = thumbs[i];
                int border = i == 0 ? 0 : thumbs[i - 1].Rect.Bottom;
                Move(thumb, border, false, false);

                Assert.IsTrue(thumb.Rect.Top - border <= moveStep);
                Assert.IsTrue(thumb.Rect.Right > border);
            }

            //move down
            for (int i = thumbs.Count - 1; i >= 0; i--)
            {
                AppiumWebElement thumb = thumbs[i];
                int border = i == thumbs.Count - 1 ? Screen.PrimaryScreen.WorkingArea.Bottom : thumbs[i + 1].Rect.Top;
                Move(thumb, border, true, false);

                Assert.IsTrue(border - thumb.Rect.Bottom <= moveStep);
                Assert.IsTrue(thumb.Rect.Top < border);
            }

            //move left
            foreach (AppiumWebElement thumb in thumbs)
            {
                int expected = thumb.Rect.Y;

                Move(thumb, 0, false, true);
                int actual = thumb.Rect.Y;

                Assert.AreEqual(expected, actual);
            }

            //move right
            foreach (AppiumWebElement thumb in thumbs)
            {
                int expected = thumb.Rect.Y;

                Move(thumb, Screen.PrimaryScreen.WorkingArea.Right, true, true);
                int actual = thumb.Rect.Y;

                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void CreateSplitter()
        {
            OpenCreatorWindow("Columns", "EditTemplateButton");
            WindowsElement gridEditor = session.FindElementByClassName("GridEditor");
            Assert.IsNotNull(gridEditor);

            ReadOnlyCollection<AppiumWebElement> zones = gridEditor.FindElementsByClassName("GridZone");
            Assert.AreEqual(3, zones.Count, "Zones count invalid");

            const int defaultSpacing = 16;
            int splitPos = zones[0].Rect.Y + zones[0].Rect.Height / 2;

            new Actions(session).MoveToElement(zones[0]).Click().Perform();

            zones = gridEditor.FindElementsByClassName("GridZone");
            Assert.AreEqual(4, zones.Count);

            //check splitted zone 
            Assert.AreEqual(defaultSpacing, zones[0].Rect.Top);
            Assert.IsTrue(Math.Abs(zones[0].Rect.Bottom - splitPos + defaultSpacing / 2) <= 2);
            Assert.IsTrue(Math.Abs(zones[1].Rect.Top - splitPos - defaultSpacing / 2) <= 2);
            Assert.AreEqual(Screen.PrimaryScreen.Bounds.Bottom - defaultSpacing, zones[1].Rect.Bottom);
        }

        [TestMethod]
        public void TestSplitterShiftAfterCreation()
        {
            OpenCreatorWindow("Columns", "EditTemplateButton");
            WindowsElement gridEditor = session.FindElementByClassName("GridEditor");
            Assert.IsNotNull(gridEditor);

            ReadOnlyCollection<AppiumWebElement> zones = gridEditor.FindElementsByClassName("GridZone");
            Assert.AreEqual(3, zones.Count, "Zones count invalid");

            const int defaultSpacing = 16;

            //create first split
            int firstSplitPos = zones[0].Rect.Y + zones[0].Rect.Height / 4;
            new Actions(session).MoveToElement(zones[0]).MoveByOffset(0, -(zones[0].Rect.Height / 4)).Click().Perform();

            zones = gridEditor.FindElementsByClassName("GridZone");
            Assert.AreEqual(4, zones.Count);

            Assert.AreEqual(defaultSpacing, zones[0].Rect.Top);
            Assert.IsTrue(Math.Abs(zones[0].Rect.Bottom - firstSplitPos + defaultSpacing / 2) <= 2);
            Assert.IsTrue(Math.Abs(zones[1].Rect.Top - firstSplitPos - defaultSpacing / 2) <= 2);
            Assert.AreEqual(Screen.PrimaryScreen.Bounds.Bottom - defaultSpacing, zones[3].Rect.Bottom);

            //create second split
            int secondSplitPos = zones[3].Rect.Y + zones[3].Rect.Height / 2;
            int expectedTop = zones[3].Rect.Top;

            new Actions(session).MoveToElement(zones[3]).Click().Perform();

            zones = gridEditor.FindElementsByClassName("GridZone");
            Assert.AreEqual(5, zones.Count);

            //check first split on same position
            Assert.AreEqual(defaultSpacing, zones[0].Rect.Top);
            Assert.IsTrue(Math.Abs(zones[0].Rect.Bottom - firstSplitPos + defaultSpacing / 2) <= 2);

            //check second split
            Assert.AreEqual(expectedTop, zones[3].Rect.Top);
            Assert.IsTrue(Math.Abs(zones[3].Rect.Bottom - secondSplitPos + defaultSpacing / 2) <= 2);
            Assert.IsTrue(Math.Abs(zones[4].Rect.Top - secondSplitPos - defaultSpacing / 2) <= 2);
            Assert.AreEqual(Screen.PrimaryScreen.Bounds.Bottom - defaultSpacing, zones[4].Rect.Bottom);
        }

        [TestMethod]
        public void CreateSplitterWithShiftPressed()
        {
            OpenCreatorWindow("Columns", "EditTemplateButton");
            WindowsElement gridEditor = session.FindElementByClassName("GridEditor");
            Assert.IsNotNull(gridEditor);

            ReadOnlyCollection<AppiumWebElement> thumbs = gridEditor.FindElementsByClassName("Thumb");
            Assert.AreEqual(3, gridEditor.FindElementsByClassName("GridZone").Count);
            Assert.AreEqual(2, thumbs.Count);

            new Actions(session).MoveToElement(thumbs[0]).Click().MoveByOffset(-100, 0)
                .KeyDown(OpenQA.Selenium.Keys.Shift).Click().KeyUp(OpenQA.Selenium.Keys.Shift)
                .Perform();
            Assert.AreEqual(3, gridEditor.FindElementsByClassName("Thumb").Count);

            ReadOnlyCollection<AppiumWebElement> zones = gridEditor.FindElementsByClassName("GridZone");
            Assert.AreEqual(4, zones.Count);

            //check that zone was splitted vertically
            Assert.AreEqual(zones[0].Rect.Height, zones[1].Rect.Height);
            Assert.AreEqual(zones[1].Rect.Height, zones[2].Rect.Height);
            Assert.AreEqual(zones[2].Rect.Height, zones[3].Rect.Height);
        }

        [TestMethod]
        public void CreateSplitterWithShiftPressedFocusOnGridEditor()
        {
            OpenCreatorWindow("Columns", "EditTemplateButton");
            WindowsElement gridEditor = session.FindElementByClassName("GridEditor");
            Assert.IsNotNull(gridEditor);

            ReadOnlyCollection<AppiumWebElement> thumbs = gridEditor.FindElementsByClassName("Thumb");
            Assert.AreEqual(3, gridEditor.FindElementsByClassName("GridZone").Count);
            Assert.AreEqual(2, thumbs.Count);

            new Actions(session).MoveToElement(thumbs[0]).Click().MoveByOffset(-100, 0)
                .KeyDown(OpenQA.Selenium.Keys.Shift).Click().KeyUp(OpenQA.Selenium.Keys.Shift)
                .Perform();
            Assert.AreEqual(3, gridEditor.FindElementsByClassName("Thumb").Count);

            ReadOnlyCollection<AppiumWebElement> zones = gridEditor.FindElementsByClassName("GridZone");
            Assert.AreEqual(4, zones.Count);

            //check that zone was splitted vertically
            Assert.AreEqual(zones[0].Rect.Height, zones[1].Rect.Height);
            Assert.AreEqual(zones[1].Rect.Height, zones[2].Rect.Height);
            Assert.AreEqual(zones[2].Rect.Height, zones[3].Rect.Height);
        }

        [TestMethod]
        public void MoveHorizontallyWithLimiter()
        {
            OpenCreatorWindow("Columns", "EditTemplateButton");
            WindowsElement gridEditor = session.FindElementByClassName("GridEditor");
            Assert.IsNotNull(gridEditor);

            Assert.AreEqual(3, gridEditor.FindElementsByClassName("GridZone").Count);
            ReadOnlyCollection<AppiumWebElement> thumbs = gridEditor.FindElementsByClassName("Thumb");
            Assert.AreEqual(2, thumbs.Count);

            //create new zones
            new Actions(session).MoveToElement(thumbs[0]).Click().MoveByOffset(-30, 0)
                .KeyDown(OpenQA.Selenium.Keys.Shift).Click().KeyUp(OpenQA.Selenium.Keys.Shift)
                .Perform();
            thumbs = gridEditor.FindElementsByClassName("Thumb");
            Assert.AreEqual(4, gridEditor.FindElementsByClassName("GridZone").Count);
            Assert.AreEqual(3, thumbs.Count);

            //move thumbs
            AppiumWebElement limiter = gridEditor.FindElementsByClassName("Thumb")[0];
            AppiumWebElement movable = gridEditor.FindElementsByClassName("Thumb")[1];

            Move(movable, 0, false, true);
            Assert.IsTrue(movable.Rect.X > limiter.Rect.X);
            Assert.IsTrue(movable.Rect.X - limiter.Rect.X < movable.Rect.Width);

            Move(limiter, limiter.Rect.X - (limiter.Rect.X / 2), false, true);

            Move(movable, 0, false, true);
            Assert.IsTrue(movable.Rect.X > limiter.Rect.X);
            Assert.IsTrue(movable.Rect.X - limiter.Rect.X < movable.Rect.Width);
        }

        [TestMethod]
        public void MoveVerticallyWithLimiter()
        {
            OpenCreatorWindow("Rows", "EditTemplateButton");
            WindowsElement gridEditor = session.FindElementByClassName("GridEditor");
            Assert.IsNotNull(gridEditor);

            Assert.AreEqual(3, gridEditor.FindElementsByClassName("GridZone").Count);
            ReadOnlyCollection<AppiumWebElement> thumbs = gridEditor.FindElementsByClassName("Thumb");
            Assert.AreEqual(2, thumbs.Count);

            //create new zones
            new Actions(session).MoveToElement(thumbs[0]).Click().MoveByOffset(0, -(thumbs[0].Rect.Y / 2))
                .KeyDown(OpenQA.Selenium.Keys.Shift).Click().KeyUp(OpenQA.Selenium.Keys.Shift)
                .Perform();
            thumbs = gridEditor.FindElementsByClassName("Thumb");
            Assert.AreEqual(4, gridEditor.FindElementsByClassName("GridZone").Count);
            Assert.AreEqual(3, thumbs.Count);

            //move thumbs
            AppiumWebElement limiter = gridEditor.FindElementsByClassName("Thumb")[0];
            AppiumWebElement movable = gridEditor.FindElementsByClassName("Thumb")[1];

            Move(movable, 0, false, false);
            Assert.IsTrue(movable.Rect.Y > limiter.Rect.Y);
            Assert.IsTrue(movable.Rect.Y - limiter.Rect.Y < movable.Rect.Height);

            Move(limiter, limiter.Rect.Y - (limiter.Rect.Y / 2), false, false, -5);

            Move(movable, 0, false, false);
            Assert.IsTrue(movable.Rect.Y > limiter.Rect.Y);
            Assert.IsTrue(movable.Rect.Y - limiter.Rect.Y < movable.Rect.Height);
        }

        [TestMethod]
        public void MergeZones()
        {
            OpenCreatorWindow("Columns", "EditTemplateButton");
            WindowsElement gridEditor = session.FindElementByClassName("GridEditor");
            Assert.IsNotNull(gridEditor);

            ReadOnlyCollection<AppiumWebElement> zones = gridEditor.FindElementsByClassName("GridZone");
            ReadOnlyCollection<AppiumWebElement> thumbs = gridEditor.FindElementsByClassName("Thumb");
            Assert.AreEqual(3, zones.Count);
            Assert.AreEqual(2, thumbs.Count);

            Move(zones[0], thumbs[0].Rect.X + thumbs[0].Rect.Width + 10, true, true, -(zones[0].Rect.Height / 2) + 10);

            AppiumWebElement mergeButton = gridEditor.FindElementByName("Merge zones");
            Assert.IsNotNull(mergeButton, "Cannot merge: no merge button");
            new Actions(session).Click(mergeButton).Perform();

            Assert.AreEqual(2, gridEditor.FindElementsByClassName("GridZone").Count);
            Assert.AreEqual(1, gridEditor.FindElementsByClassName("Thumb").Count);
        }

        [TestMethod]
        public void MoveAfterMerge()
        {
            OpenCreatorWindow("Columns", "EditTemplateButton");
            WindowsElement gridEditor = session.FindElementByClassName("GridEditor");
            Assert.IsNotNull(gridEditor);

            ReadOnlyCollection<AppiumWebElement> thumbs = gridEditor.FindElementsByClassName("Thumb");

            //create new zones
            new Actions(session).MoveToElement(thumbs[0]).Click().MoveByOffset(-(thumbs[0].Rect.X / 2), 0)
                .KeyDown(OpenQA.Selenium.Keys.Shift).Click().KeyUp(OpenQA.Selenium.Keys.Shift)
                .Perform();
            thumbs = gridEditor.FindElementsByClassName("Thumb");

            //merge zones
            ReadOnlyCollection<AppiumWebElement> zones = gridEditor.FindElementsByClassName("GridZone");
            Move(zones[0], thumbs[0].Rect.X + thumbs[0].Rect.Width + 10, true, true, -(zones[0].Rect.Height / 2) + 10);
            AppiumWebElement mergeButton = gridEditor.FindElementByName("Merge zones");
            Assert.IsNotNull(mergeButton, "Cannot merge: no merge button");
            new Actions(session).Click(mergeButton).Perform();

            //move thumb
            thumbs = gridEditor.FindElementsByClassName("Thumb");
            AppiumWebElement thumb = thumbs[0]; 
            Move(thumb, 0, false, true);
            Assert.IsTrue(thumb.Rect.Left <= moveStep);
            Assert.IsTrue(thumb.Rect.Right > 0);
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context);
            Assert.IsNotNull(session);
            EnableModules(false, true, false, false, false, false, false, false);

            ResetSettings();

            if (!isPowerToysLaunched)
            {
                LaunchPowerToys();
            }
            Assert.IsTrue(OpenEditor());
            OpenTemplates();
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