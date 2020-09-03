using Microsoft.VisualStudio.TestTools.UnitTesting;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;
using System;

namespace PowerToysTests
{
    public class FancyZonesEditor : PowerToysSession
    {
        protected static WindowsElement editorWindow;
        protected static AppiumWebElement creatorWindow;

        protected static void ResetSettings()
        {
            ResetDefaultFancyZonesSettings(false);
            ResetDefaultZoneSettings(true);
        }

        protected static bool OpenEditor()
        {
            try
            {
                new Actions(session).KeyDown(OpenQA.Selenium.Keys.Command).SendKeys("`").KeyUp(OpenQA.Selenium.Keys.Command).Perform();
                
                //editorWindow = WaitElementByXPath("//Window[@Name=\"FancyZones Editor\"]");
                editorWindow = session.FindElementByName("FancyZones Editor");
                Assert.IsNotNull(editorWindow, "Couldn't find editor window");
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            return false;
        }

        protected static void CloseEditor()
        {
            try
            {
                if (editorWindow != null)
                {
                    editorWindow.SendKeys(OpenQA.Selenium.Keys.Alt + OpenQA.Selenium.Keys.F4);
                }
            }
            catch (OpenQA.Selenium.WebDriverException)
            {
                //editor has been already closed
            }
        }

        protected static void OpenCustomLayouts()
        {
            try
            {
                WindowsElement customsTab = session.FindElementByName("Custom");
                customsTab.Click();
                string isSelected = customsTab.GetAttribute("SelectionItem.IsSelected");
                Assert.AreEqual("True", isSelected, "Custom tab cannot be opened");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        protected static void OpenTemplates()
        {
            try
            {
                WindowsElement templatesTab = session.FindElementByName("Templates");
                templatesTab.Click();
                string isSelected = templatesTab.GetAttribute("SelectionItem.IsSelected");
                Assert.AreEqual("True", isSelected, "Templates tab cannot be opened");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        protected static void OpenCreatorWindow(string tabName, string buttonId = "EditCustomButton")
        {
            try
            {
                editorWindow.FindElementByName(tabName).Click();
                editorWindow.FindElementByAccessibilityId(buttonId).Click();
                creatorWindow = editorWindow.FindElementByXPath("//Window");
                Assert.IsNotNull(creatorWindow, "Creator window didn't open");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }
        }

        protected void ZoneCountTest(int canvasZoneCount, int gridZoneCount)
        {
            Assert.AreEqual(canvasZoneCount, session.FindElementsByClassName("CanvasZone").Count);
            Assert.AreEqual(gridZoneCount, session.FindElementsByClassName("GridZone").Count);
        }
    }
}