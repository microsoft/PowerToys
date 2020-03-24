using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    public class FancyZonesEditor : PowerToysSession
    {
        protected static WindowsElement editorWindow;

        protected static void ResetSettings()
        {
            ResetDefaultFancyZonesSettings(false);
            ResetDefautZoneSettings(true);
        }

        protected static void OpenEditor()
        {
            new Actions(session).KeyDown(OpenQA.Selenium.Keys.Command).SendKeys("`").KeyUp(OpenQA.Selenium.Keys.Command).Perform();
            ShortWait();
            editorWindow = session.FindElementByXPath("//Window[@Name=\"FancyZones Editor\"]");
        }

        protected static void CloseEditor()
        {
            try
            {
                if (editorWindow != null)
                {
                    editorWindow.SendKeys(OpenQA.Selenium.Keys.Alt + OpenQA.Selenium.Keys.F4);
                    ShortWait();
                }
            }
            catch (OpenQA.Selenium.WebDriverException)
            {
                //editor has been already closed
            }
        }

        protected static void OpenCustomLayouts()
        {
            WindowsElement customsTab = session.FindElementByName("Custom");
            customsTab.Click();
            string isSelected = customsTab.GetAttribute("SelectionItem.IsSelected");
            Assert.AreEqual("True", isSelected, "Custom tab cannot be opened");
        }

        protected static void OpenTemplates()
        {
            WindowsElement templatesTab = session.FindElementByName("Templates");
            templatesTab.Click();
            string isSelected = templatesTab.GetAttribute("SelectionItem.IsSelected");
            Assert.AreEqual("True", isSelected, "Templates tab cannot be opened");
        }

        protected void OpenCreatorWindow(string tabName, string creatorWindowName, string buttonId = "EditCustomButton")
        {
            string elementXPath = "//Text[@Name=\"" + tabName + "\"]";
            session.FindElementByXPath(elementXPath).Click();
            session.FindElementByAccessibilityId(buttonId).Click();

            WindowsElement creatorWindow = session.FindElementByName(creatorWindowName);
            Assert.IsNotNull(creatorWindow, "Creator window didn't open");
        }

        protected void ZoneCountTest(int canvasZoneCount, int gridZoneCount)
        {
            Assert.AreEqual(canvasZoneCount, session.FindElementsByClassName("CanvasZone").Count);
            Assert.AreEqual(gridZoneCount, session.FindElementsByClassName("GridZone").Count);
        }
    }
}