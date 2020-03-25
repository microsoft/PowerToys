using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using System.Collections.ObjectModel;
using System.Windows.Forms;

namespace PowerToysTests
{ 
    [TestClass]
    public class FancyZonesEditorGridZoneResizeTests : FancyZonesEditor
    {
        private const int defaultZoneSpace = 16;

        private void MoveLeft(AppiumWebElement thumb, int leftBorder)
        {
            Actions action = new Actions(session);
            action.ClickAndHold(thumb);

            const int step = 5;
            int moves = (thumb.Rect.X - leftBorder) / step;
            for (int j = 0; j < moves; j++)
            {
                action.MoveByOffset(-step, 0);
            }

            //additional steps to move closer to border
            for (int j = 0; j < step; j++)
            {
                action.MoveByOffset(-1, 0);
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

            for (int i = 0; i < thumbs.Count; i++)
            {
                AppiumWebElement thumb = thumbs[i];
                int border = i == 0 ? 0 : thumbs[i - 1].Rect.X;
                MoveLeft(thumb, border);
                Assert.IsTrue(thumb.Rect.Left <= border);
                Assert.IsTrue(thumb.Rect.X >= border);
                Assert.IsTrue(thumb.Rect.Right > border);
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