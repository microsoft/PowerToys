using System;
using System.IO;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    [TestClass]
    public class FancyZonesTests : PowerToysSession
    {
        private string _settingsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft/PowerToys/FancyZones/settings.json");
        private string _initialSettings;
        private JObject _initialSettingsJson;
        
        private JObject getProperties()
        {
            JObject settings = JObject.Parse(File.ReadAllText(_settingsPath));
            return settings["properties"].ToObject<JObject>();
        }

        private T getPropertyValue<T>(string propertyName)
        {
            JObject properties = getProperties();
            return properties[propertyName].ToObject<JObject>()["value"].Value<T>();
        }

        private T getPropertyValue<T>(JObject properties, string propertyName)
        {
            return properties[propertyName].ToObject<JObject>()["value"].Value<T>();
        }

        private void OpenFancyZonesSettings()
        {
            WindowsElement fzNavigationButton = session.FindElementByXPath("//Button[@Name=\"FancyZones\"]");
            Assert.IsNotNull(fzNavigationButton);

            fzNavigationButton.Click();
            fzNavigationButton.Click();

            ShortWait();
        }
        
        private void SaveAndCheckOpacitySettings(WindowsElement saveButton, WindowsElement editor, int expected)
        {
            Assert.AreEqual(expected.ToString() + "\r\n", editor.Text);

            saveButton.Click();
            ShortWait();

            int value = getPropertyValue<int>("fancyzones_highlight_opacity");
            Assert.AreEqual(expected, value);
        }

        private void SetOpacity(WindowsElement editor, string key)
        {
            editor.Click(); //activate
            editor.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.Backspace); //clear previous value
            editor.SendKeys(key);
            editor.SendKeys(OpenQA.Selenium.Keys.Enter); //confirm changes
        }

        [TestMethod]
        public void FancyZonesSettingsOpen()
        {
            OpenFancyZonesSettings();

            WindowsElement fzTitle = session.FindElementByName("FancyZones Settings");
            Assert.IsNotNull(fzTitle);
        }

        [TestMethod]
        public void EditorOpen()
        {
            OpenFancyZonesSettings();

            session.FindElementByXPath("//Button[@Name=\"Edit zones\"]").Click();
            ShortWait();

            WindowsElement editorWindow = session.FindElementByName("FancyZones Editor");
            Assert.IsNotNull(editorWindow);
        }

        /*
         * click each toggle,
         * save changes,
         * check if settings are changed after clicking save button
         */
        [TestMethod]
        public void TogglesSingleClickSaveButtonTest()
        {
            OpenFancyZonesSettings();

            WindowsElement saveButton = session.FindElementByName("Save");
            Assert.IsNotNull(saveButton);
            string IsEnabled = saveButton.GetAttribute("IsEnabled");
            Assert.AreEqual("False", IsEnabled);
                        
            for (int i = 37; i < 45; i++)
            {
                string toggleId = "Toggle" + i.ToString();
                WindowsElement toggle = session.FindElementByAccessibilityId(toggleId);
                Assert.IsNotNull(toggle);
                toggle.Click();

                IsEnabled = saveButton.GetAttribute("IsEnabled");
                Assert.AreEqual("True", IsEnabled);

                saveButton.Click();
                IsEnabled = saveButton.GetAttribute("IsEnabled");
                Assert.AreEqual("False", IsEnabled);
            }
            
            //check saved settings
            JObject savedProps = getProperties();
            JObject initialProps = _initialSettingsJson["properties"].ToObject<JObject>();

            Assert.AreNotEqual(getPropertyValue<bool>(initialProps, "fancyzones_shiftDrag"), getPropertyValue<bool>(savedProps, "fancyzones_shiftDrag"));
            Assert.AreNotEqual(getPropertyValue<bool>(initialProps, "fancyzones_overrideSnapHotkeys"), getPropertyValue<bool>(savedProps, "fancyzones_overrideSnapHotkeys"));
            Assert.AreNotEqual(getPropertyValue<bool>(initialProps, "fancyzones_zoneSetChange_flashZones"), getPropertyValue<bool>(savedProps, "fancyzones_zoneSetChange_flashZones"));
            Assert.AreNotEqual(getPropertyValue<bool>(initialProps, "fancyzones_displayChange_moveWindows"), getPropertyValue<bool>(savedProps, "fancyzones_displayChange_moveWindows"));
            Assert.AreNotEqual(getPropertyValue<bool>(initialProps, "fancyzones_zoneSetChange_moveWindows"), getPropertyValue<bool>(savedProps, "fancyzones_zoneSetChange_moveWindows"));
            Assert.AreNotEqual(getPropertyValue<bool>(initialProps, "fancyzones_virtualDesktopChange_moveWindows"), getPropertyValue<bool>(savedProps, "fancyzones_virtualDesktopChange_moveWindows"));
            Assert.AreNotEqual(getPropertyValue<bool>(initialProps, "fancyzones_appLastZone_moveWindows"), getPropertyValue<bool>(savedProps, "fancyzones_appLastZone_moveWindows"));
            Assert.AreNotEqual(getPropertyValue<bool>(initialProps, "use_cursorpos_editor_startupscreen"), getPropertyValue<bool>(savedProps, "use_cursorpos_editor_startupscreen"));
        }

        /*
         * click each toggle twice,
         * save changes,
         * check if settings are unchanged after clicking save button
         */
        [TestMethod]
        public void TogglesDoubleClickSave()
        {
            OpenFancyZonesSettings();

            WindowsElement saveButton = session.FindElementByName("Save");
            Assert.IsNotNull(saveButton);
            string isEnabled = saveButton.GetAttribute("IsEnabled");
            Assert.AreEqual("False", isEnabled);

            for (int i = 37; i < 45; i++)
            {
                string toggleId = "Toggle" + i.ToString();
                WindowsElement toggle = session.FindElementByAccessibilityId(toggleId);
                Assert.IsNotNull(toggle);
                toggle.Click();
                toggle.Click();

                isEnabled = saveButton.GetAttribute("IsEnabled");
                Assert.AreEqual("True", isEnabled);

                saveButton.Click();
                isEnabled = saveButton.GetAttribute("IsEnabled");
                Assert.AreEqual("False", isEnabled);
            }

            string savedSettings = File.ReadAllText(_settingsPath);
            Assert.AreEqual(_initialSettings, savedSettings);
        }

        [TestMethod]
        public void HighlightOpacitySetValue()
        {
            OpenFancyZonesSettings();
            WindowsElement saveButton = session.FindElementByName("Save");
            Assert.IsNotNull(saveButton);

            WindowsElement editor = session.FindElementByName("Zone Highlight Opacity (%)");
            Assert.IsNotNull(editor);

            SetOpacity(editor, "50");
            SaveAndCheckOpacitySettings(saveButton, editor, 50);

            SetOpacity(editor, "-50");
            SaveAndCheckOpacitySettings(saveButton, editor, 0);

            SetOpacity(editor, "200");
            SaveAndCheckOpacitySettings(saveButton, editor, 100);

            //for invalid input values previously saved value expected
            SetOpacity(editor, "asdf"); 
            SaveAndCheckOpacitySettings(saveButton, editor, 100); 
            
            SetOpacity(editor, "*");
            SaveAndCheckOpacitySettings(saveButton, editor, 100); 
            
            SetOpacity(editor, OpenQA.Selenium.Keys.Return);
            SaveAndCheckOpacitySettings(saveButton, editor, 100);

            Clipboard.SetText("Hello, clipboard");
            SetOpacity(editor, OpenQA.Selenium.Keys.Control + "v");
            SaveAndCheckOpacitySettings(saveButton, editor, 100);
        }

        [TestMethod]
        public void HighlightOpacityIncreaseValue()
        {
            OpenFancyZonesSettings();
            WindowsElement saveButton = session.FindElementByName("Save");
            Assert.IsNotNull(saveButton);

            WindowsElement editor = session.FindElementByName("Zone Highlight Opacity (%)");
            Assert.IsNotNull(editor);

            SetOpacity(editor, "99");
            SaveAndCheckOpacitySettings(saveButton, editor, 99);

            System.Drawing.Rectangle editorRect = editor.Rect;
            
            Actions action = new Actions(session);
            action.MoveToElement(editor).MoveByOffset(editorRect.Width / 2 + 10, -editorRect.Height / 4).Perform();
            ShortWait();

            action.Click().Perform();
            Assert.AreEqual("100\r\n", editor.Text);
            SaveAndCheckOpacitySettings(saveButton, editor, 100);

            action.Click().Perform();
            Assert.AreEqual("100\r\n", editor.Text);
            SaveAndCheckOpacitySettings(saveButton, editor, 100);
        }

        [TestMethod]
        public void HighlightOpacityDecreaseValue()
        {
            OpenFancyZonesSettings();
            WindowsElement saveButton = session.FindElementByName("Save");
            Assert.IsNotNull(saveButton);

            WindowsElement editor = session.FindElementByName("Zone Highlight Opacity (%)");
            Assert.IsNotNull(editor);

            SetOpacity(editor, "1");
            SaveAndCheckOpacitySettings(saveButton, editor, 1);

            System.Drawing.Rectangle editorRect = editor.Rect;

            Actions action = new Actions(session);
            action.MoveToElement(editor).MoveByOffset(editorRect.Width / 2 + 10, editorRect.Height / 4).Perform();
            ShortWait();

            action.Click().Perform();
            Assert.AreEqual("0\r\n", editor.Text);
            SaveAndCheckOpacitySettings(saveButton, editor, 0);

            action.Click().Perform();
            Assert.AreEqual("0\r\n", editor.Text);
            SaveAndCheckOpacitySettings(saveButton, editor, 0);
        }

        [TestMethod]
        public void HighlightOpacityClearValueButton()
        {
            OpenFancyZonesSettings();
            WindowsElement editor = session.FindElementByName("Zone Highlight Opacity (%)");
            Assert.IsNotNull(editor);

            editor.Click(); //activate
            OpenQA.Selenium.Appium.AppiumWebElement clearButton = editor.FindElementByName("Clear value");
            Assert.IsNotNull(clearButton);
            
            /*element is not pointer- or keyboard interactable.*/
            Actions action = new Actions(session);
            action.MoveToElement(clearButton).Click().Perform();

            Assert.AreEqual("\r\n", editor.Text);
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
            try
            {
                _initialSettings = File.ReadAllText(_settingsPath);
                _initialSettingsJson = JObject.Parse(_initialSettings);
            }
            catch (System.IO.FileNotFoundException)
            {
                _initialSettings = "";
            }
            

            OpenSettings();
            ShortWait();
        }

        [TestCleanup]
        public void TestCleanup()
        {
            try
            {
                WindowsElement editorWindow = session.FindElementByName("FancyZones Editor");
                if (editorWindow != null)
                {
                    editorWindow.SendKeys(OpenQA.Selenium.Keys.Alt + OpenQA.Selenium.Keys.F4);
                }
            } 
            catch (OpenQA.Selenium.WebDriverException)
            {
                //editor window not found
            }

            CloseSettings();
            if (_initialSettings.Length > 0)
            {
                File.WriteAllText(_settingsPath, _initialSettings);
            }            
        }
    }
}
