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

        private void Move(AppiumWebElement thumb, int border, bool moveAscending, bool moveHorizontally)
        {
            Actions action = new Actions(session);
            action.ClickAndHold(thumb);

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
        public void MoveVerticalDelimiter()
        {
            OpenCreatorWindow("Columns", "Custom table layout creator", "EditTemplateButton");
            WindowsElement gridEditor = session.FindElementByClassName("GridEditor");
            Assert.IsNotNull(gridEditor);

            Assert.AreEqual(3, session.FindElementsByClassName("GridZone").Count);
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

                Move(thumb, Screen.PrimaryScreen.WorkingArea.Right, false, false);
                int actual = thumb.Rect.X;

                Assert.AreEqual(expected, actual);
            }
        }

        [TestMethod]
        public void MoveHorizontalDelimiter()
        {
            OpenCreatorWindow("Rows", "Custom table layout creator", "EditTemplateButton");
            WindowsElement gridEditor = session.FindElementByClassName("GridEditor");
            Assert.IsNotNull(gridEditor);

            Assert.AreEqual(3, session.FindElementsByClassName("GridZone").Count);
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

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context, false);
            ResetSettings();

            if (!isPowerToysLaunched)
            {
                LaunchPowerToys();
            }
            OpenEditor();
            OpenTemplates();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            CloseEditor();
            TearDown();
        }

        [TestInitialize]
        public void TestInitialize()
        {

        }

        [TestCleanup]
        public void TestCleanup()
        {
            new Actions(session).MoveToElement(session.FindElementByXPath("//Button[@Name=\"Cancel\"]")).Click().Perform();
        }
    }
}