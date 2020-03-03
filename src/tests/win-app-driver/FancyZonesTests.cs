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
        private string _settingsPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Microsoft/PowerToys/FancyZones/settings.json");
        private string _initialSettings;
        private JObject _initialSettingsJson;

        private void OpenFancyZonesSettings()
        {
            WindowsElement fzMenuButton = session.FindElementByXPath("//Button[@Name=\"FancyZones\"]");
            Assert.IsNotNull(fzMenuButton);
            fzMenuButton.Click();
            fzMenuButton.Click();

            ShortWait();
        }
        
        private void SaveAndCheckOpacitySettings(WindowsElement saveButton, WindowsElement editor, int expected)
        {
            Assert.AreEqual(expected.ToString() + "\r\n", editor.Text);

            saveButton.Click();
            ShortWait();

            JObject savedSettings = JObject.Parse(File.ReadAllText(_settingsPath));
            JObject savedProps = savedSettings["properties"].ToObject<JObject>();
            int value = savedProps["fancyzones_highlight_opacity"].ToObject<JObject>()["value"].Value<int>();
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
            WindowsElement fzMenuButton = session.FindElementByXPath("//Button[@Name=\"FancyZones\"]");
            Assert.IsNotNull(fzMenuButton);
            fzMenuButton.Click();
            fzMenuButton.Click();
            ShortWait();

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
            JObject savedSettings = JObject.Parse(File.ReadAllText(_settingsPath));
            JObject savedProps = savedSettings["properties"].ToObject<JObject>();
            JObject initialProps = _initialSettingsJson["properties"].ToObject<JObject>();

            Assert.AreNotEqual(initialProps["fancyzones_shiftDrag"].ToObject<JObject>()["value"].Value<bool>(), savedProps["fancyzones_shiftDrag"].ToObject<JObject>()["value"].Value<bool>());
            Assert.AreNotEqual(initialProps["fancyzones_overrideSnapHotkeys"].ToObject<JObject>()["value"].Value<bool>(), savedProps["fancyzones_overrideSnapHotkeys"].ToObject<JObject>()["value"].Value<bool>());
            Assert.AreNotEqual(initialProps["fancyzones_zoneSetChange_flashZones"].ToObject<JObject>()["value"].Value<bool>(), savedProps["fancyzones_zoneSetChange_flashZones"].ToObject<JObject>()["value"].Value<bool>());
            Assert.AreNotEqual(initialProps["fancyzones_displayChange_moveWindows"].ToObject<JObject>()["value"].Value<bool>(), savedProps["fancyzones_displayChange_moveWindows"].ToObject<JObject>()["value"].Value<bool>());
            Assert.AreNotEqual(initialProps["fancyzones_zoneSetChange_moveWindows"].ToObject<JObject>()["value"].Value<bool>(), savedProps["fancyzones_zoneSetChange_moveWindows"].ToObject<JObject>()["value"].Value<bool>());
            Assert.AreNotEqual(initialProps["fancyzones_virtualDesktopChange_moveWindows"].ToObject<JObject>()["value"].Value<bool>(), savedProps["fancyzones_virtualDesktopChange_moveWindows"].ToObject<JObject>()["value"].Value<bool>());
            Assert.AreNotEqual(initialProps["fancyzones_appLastZone_moveWindows"].ToObject<JObject>()["value"].Value<bool>(), savedProps["fancyzones_appLastZone_moveWindows"].ToObject<JObject>()["value"].Value<bool>());
            Assert.AreNotEqual(initialProps["use_cursorpos_editor_startupscreen"].ToObject<JObject>()["value"].Value<bool>(), savedProps["use_cursorpos_editor_startupscreen"].ToObject<JObject>()["value"].Value<bool>());
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
