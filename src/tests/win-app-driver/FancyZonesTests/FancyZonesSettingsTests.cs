using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO.Abstractions;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json.Linq;
using OpenQA.Selenium.Appium;
using OpenQA.Selenium.Appium.Windows;
using OpenQA.Selenium.Interactions;

namespace PowerToysTests
{
    [Ignore]
    [TestClass]
    public class FancyZonesSettingsTests : PowerToysSession
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IFile File = FileSystem.File;


        private JObject _initialSettingsJson;

        private static WindowsElement _saveButton;
        private static Actions _scrollUp;

        private const int _expectedTogglesCount = 9;

        private static void Init()
        {
            OpenSettings();
            OpenFancyZonesSettings();

            _saveButton = session.FindElementByName("Save");
            Assert.IsNotNull(_saveButton);

            _scrollUp = new Actions(session).MoveToElement(_saveButton).MoveByOffset(0, _saveButton.Rect.Height).ContextClick()
                .SendKeys(OpenQA.Selenium.Keys.Home);
            Assert.IsNotNull(_scrollUp);
        }

        private JObject GetProperties()
        {
            try
            {
                JObject settings = JObject.Parse(File.ReadAllText(_fancyZonesSettingsPath));
                return settings["properties"].ToObject<JObject>();
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                return new JObject();
            }
        }

        private T GetPropertyValue<T>(string propertyName)
        {
            JObject properties = GetProperties();
            return properties[propertyName].ToObject<JObject>()["value"].Value<T>();
        }

        private T GetPropertyValue<T>(JObject properties, string propertyName)
        {
            return properties[propertyName].ToObject<JObject>()["value"].Value<T>();
        }

        private void ScrollDown(int count)
        {
            Actions scroll = new Actions(session);
            scroll.MoveToElement(_saveButton).MoveByOffset(0, _saveButton.Rect.Height).ContextClick();
            for (int i = 0; i < count; i++)
            {
                scroll.SendKeys(OpenQA.Selenium.Keys.PageDown);
            }

            scroll.Perform();
        }

        private void ScrollUp()
        {
            _scrollUp.Perform();
        }

        private void SaveChanges()
        {
            string isEnabled = _saveButton.GetAttribute("IsEnabled");
            Assert.AreEqual("True", isEnabled);

            _saveButton.Click();

            isEnabled = _saveButton.GetAttribute("IsEnabled");
            Assert.AreEqual("False", isEnabled);
        }

        private void SaveAndCheckOpacitySettings(WindowsElement editor, int expected)
        {
            Assert.AreEqual(expected.ToString() + "\r\n", editor.Text);

            SaveChanges();
            WaitSeconds(1);

            int value = GetPropertyValue<int>("fancyzones_highlight_opacity");
            Assert.AreEqual(expected, value);
        }

        private void SetOpacity(WindowsElement editor, string key)
        {
            editor.Click(); //activate
            editor.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.Backspace); //clear previous value
            editor.SendKeys(key);
            editor.SendKeys(OpenQA.Selenium.Keys.Enter); //confirm changes
        }

        private void TestRgbInput(string name)
        {
            WindowsElement colorInput = session.FindElementByXPath("//Edit[@Name=\"" + name + "\"]");
            Assert.IsNotNull(colorInput);

            colorInput.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.Backspace);
            colorInput.SendKeys("0");
            colorInput.SendKeys(OpenQA.Selenium.Keys.Enter);
            Assert.AreEqual("0\r\n", colorInput.Text);

            string invalidSymbols = "qwertyuiopasdfghjklzxcvbnm,./';][{}:`~!@#$%^&*()_-+=\"\'\\";
            foreach (char symbol in invalidSymbols)
            {
                colorInput.SendKeys(symbol.ToString() + OpenQA.Selenium.Keys.Enter);
                Assert.AreEqual("0\r\n", colorInput.Text);
            }

            string validSymbols = "0123456789";
            foreach (char symbol in validSymbols)
            {
                colorInput.SendKeys(symbol.ToString() + OpenQA.Selenium.Keys.Enter);
                Assert.AreEqual(symbol.ToString() + "\r\n", colorInput.Text);
                colorInput.SendKeys(OpenQA.Selenium.Keys.Backspace);
            }

            //print zero first
            colorInput.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.Backspace);
            colorInput.SendKeys("0");
            colorInput.SendKeys("1");
            Assert.AreEqual("1\r\n", colorInput.Text);

            //too many symbols
            colorInput.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.Backspace);
            colorInput.SendKeys("1");
            colorInput.SendKeys("2");
            colorInput.SendKeys("3");
            colorInput.SendKeys("4");
            Assert.AreEqual("123\r\n", colorInput.Text);

            //too big value
            colorInput.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.Backspace);
            colorInput.SendKeys("555");

            Actions action = new Actions(session); //reset focus from input
            action.MoveToElement(colorInput).MoveByOffset(0, colorInput.Rect.Height).Click().Perform();

            Assert.AreEqual("255\r\n", colorInput.Text);
        }

        private void ClearInput(WindowsElement input)
        {
            input.Click();
            input.SendKeys(OpenQA.Selenium.Keys.Control + "a");
            input.SendKeys(OpenQA.Selenium.Keys.Backspace);
        }

        private void TestHotkey(WindowsElement input, int modifierKeysState, string key, string keyString)
        {
            BitArray b = new BitArray(new int[] { modifierKeysState });
            int[] flags = b.Cast<bool>().Select(bit => bit ? 1 : 0).ToArray();

            Actions action = new Actions(session).MoveToElement(input).Click();
            string expectedText = "";
            if (flags[0] == 1)
            {
                action.KeyDown(OpenQA.Selenium.Keys.Command);
                expectedText += "Win + ";
            }
            if (flags[1] == 1)
            {
                action.KeyDown(OpenQA.Selenium.Keys.Control);
                expectedText += "Ctrl + ";
            }
            if (flags[2] == 1)
            {
                action.KeyDown(OpenQA.Selenium.Keys.Alt);
                expectedText += "Alt + ";
            }
            if (flags[3] == 1)
            {
                action.KeyDown(OpenQA.Selenium.Keys.Shift);
                expectedText += "Shift + ";
            }

            expectedText += keyString + "\r\n";

            action.SendKeys(key + key);
            action.MoveByOffset(0, (input.Rect.Height / 2) + 10).ContextClick();
            if (flags[0] == 1)
            {
                action.KeyUp(OpenQA.Selenium.Keys.Command);
            }
            if (flags[1] == 1)
            {
                action.KeyUp(OpenQA.Selenium.Keys.Control);
            }
            if (flags[2] == 1)
            {
                action.KeyUp(OpenQA.Selenium.Keys.Alt);
            }
            if (flags[3] == 1)
            {
                action.KeyUp(OpenQA.Selenium.Keys.Shift);
            }
            action.Perform();

            SaveChanges();
            WaitSeconds(1);

            //Assert.AreEqual(expectedText, input.Text);

            JObject props = GetProperties();
            JObject hotkey = props["fancyzones_editor_hotkey"].ToObject<JObject>()["value"].ToObject<JObject>();
            Assert.AreEqual(flags[0] == 1, hotkey.Value<bool>("win"));
            Assert.AreEqual(flags[1] == 1, hotkey.Value<bool>("ctrl"));
            Assert.AreEqual(flags[2] == 1, hotkey.Value<bool>("alt"));
            Assert.AreEqual(flags[3] == 1, hotkey.Value<bool>("shift"));
            //Assert.AreEqual(keyString, hotkey.Value<string>("key"));
        }

        private void TestColorSliders(WindowsElement saturationAndBrightness, WindowsElement hue, WindowsElement hex, WindowsElement red, WindowsElement green, WindowsElement blue, string propertyName)
        {
            System.Drawing.Rectangle satRect = saturationAndBrightness.Rect;
            System.Drawing.Rectangle hueRect = hue.Rect;

            //black on the bottom
            new Actions(session).MoveToElement(saturationAndBrightness).ClickAndHold().MoveByOffset(0, satRect.Height).Release().Perform();
            WaitSeconds(1);

            Assert.AreEqual("0\r\n", red.Text);
            Assert.AreEqual("0\r\n", green.Text);
            Assert.AreEqual("0\r\n", blue.Text);
            Assert.AreEqual("000000\r\n", hex.Text);

            SaveChanges();
            WaitSeconds(1);
            Assert.AreEqual("#000000", GetPropertyValue<string>(propertyName));

            //white in left corner
            new Actions(session).MoveToElement(saturationAndBrightness).ClickAndHold().MoveByOffset(-(satRect.Width / 2), -(satRect.Height / 2)).Release().Perform();
            Assert.AreEqual("255\r\n", red.Text);
            Assert.AreEqual("255\r\n", green.Text);
            Assert.AreEqual("255\r\n", blue.Text);
            Assert.AreEqual("ffffff\r\n", hex.Text);

            SaveChanges();
            WaitSeconds(1);
            Assert.AreEqual("#ffffff", GetPropertyValue<string>(propertyName));

            //color in right corner
            new Actions(session).MoveToElement(saturationAndBrightness).ClickAndHold().MoveByOffset((satRect.Width / 2), -(satRect.Height / 2)).Release()
                .MoveToElement(hue).ClickAndHold().MoveByOffset(-(hueRect.Width / 2), 0).Release().Perform();
            Assert.AreEqual("255\r\n", red.Text);
            Assert.AreEqual("0\r\n", green.Text);
            Assert.AreEqual("0\r\n", blue.Text);
            Assert.AreEqual("ff0000\r\n", hex.Text);

            SaveChanges();
            WaitSeconds(1);
            Assert.AreEqual("#ff0000", GetPropertyValue<string>(propertyName));
        }

        [TestMethod]
        public void FancyZonesSettingsOpen()
        {
            WindowsElement fzTitle = session.FindElementByName("FancyZones Settings");
            Assert.IsNotNull(fzTitle);
        }

        /*
         * click each toggle,
         * save changes,
         * check if settings are changed after clicking save button
         */
        [TestMethod]
        public void TogglesSingleClickSaveButtonTest()
        {
            List<WindowsElement> toggles = session.FindElementsByXPath("//Pane[@Name=\"PowerToys Settings\"]/*[@LocalizedControlType=\"toggleswitch\"]").ToList();
            Assert.AreEqual(_expectedTogglesCount, toggles.Count);

            List<bool> toggleValues = new List<bool>();
            foreach (WindowsElement toggle in toggles)
            {
                Assert.IsNotNull(toggle);

                bool isOn = toggle.GetAttribute("Toggle.ToggleState") == "1";
                toggleValues.Add(isOn);

                toggle.Click();

                SaveChanges();
            }

            WaitSeconds(1);

            //check saved settings
            JObject savedProps = GetProperties();
            Assert.AreNotEqual(toggleValues[0], GetPropertyValue<bool>(savedProps, "fancyzones_shiftDrag"));
            Assert.AreNotEqual(toggleValues[1], GetPropertyValue<bool>(savedProps, "fancyzones_mouseSwitch"));
            Assert.AreNotEqual(toggleValues[2], GetPropertyValue<bool>(savedProps, "fancyzones_overrideSnapHotkeys"));
            Assert.AreNotEqual(toggleValues[3], GetPropertyValue<bool>(savedProps, "fancyzones_moveWindowAcrossMonitors"));
            Assert.AreNotEqual(toggleValues[4], GetPropertyValue<bool>(savedProps, "fancyzones_moveWindowsBasedOnPosition"));
            Assert.AreNotEqual(toggleValues[5], GetPropertyValue<bool>(savedProps, "fancyzones_displayChange_moveWindows"));
            Assert.AreNotEqual(toggleValues[6], GetPropertyValue<bool>(savedProps, "fancyzones_zoneSetChange_moveWindows"));
            Assert.AreNotEqual(toggleValues[7], GetPropertyValue<bool>(savedProps, "fancyzones_appLastZone_moveWindows"));
            Assert.AreNotEqual(toggleValues[8], GetPropertyValue<bool>(savedProps, "fancyzones_restoreSize"));
            Assert.AreNotEqual(toggleValues[9], GetPropertyValue<bool>(savedProps, "use_cursorpos_editor_startupscreen"));
            Assert.AreNotEqual(toggleValues[10], GetPropertyValue<bool>(savedProps, "fancyzones_show_on_all_monitors"));
            Assert.AreNotEqual(toggleValues[11], GetPropertyValue<bool>(savedProps, "fancyzones_multi_monitor_mode"));
            Assert.AreNotEqual(toggleValues[12], GetPropertyValue<bool>(savedProps, "fancyzones_makeDraggedWindowTransparent"));
        }

        /*
         * click each toggle twice,
         * save changes,
         * check if settings are unchanged after clicking save button
         */
        [TestMethod]
        public void TogglesDoubleClickSave()
        {
            List<WindowsElement> toggles = session.FindElementsByXPath("//Pane[@Name=\"PowerToys Settings\"]/*[@LocalizedControlType=\"toggleswitch\"]").ToList();
            Assert.AreEqual(_expectedTogglesCount, toggles.Count);

            List<bool> toggleValues = new List<bool>();
            foreach (WindowsElement toggle in toggles)
            {
                Assert.IsNotNull(toggle);

                bool isOn = toggle.GetAttribute("Toggle.ToggleState") == "1";
                toggleValues.Add(isOn);

                toggle.Click();
                toggle.Click();
            }

            SaveChanges();
            WaitSeconds(1);

            JObject savedProps = GetProperties();
            Assert.AreEqual(toggleValues[0], GetPropertyValue<bool>(savedProps, "fancyzones_shiftDrag"));
            Assert.AreEqual(toggleValues[1], GetPropertyValue<bool>(savedProps, "fancyzones_mouseSwitch"));
            Assert.AreEqual(toggleValues[2], GetPropertyValue<bool>(savedProps, "fancyzones_overrideSnapHotkeys"));
            Assert.AreEqual(toggleValues[3], GetPropertyValue<bool>(savedProps, "fancyzones_moveWindowAcrossMonitors"));
            Assert.AreEqual(toggleValues[4], GetPropertyValue<bool>(savedProps, "fancyzones_moveWindowsBasedOnPosition"));
            Assert.AreEqual(toggleValues[5], GetPropertyValue<bool>(savedProps, "fancyzones_displayChange_moveWindows"));
            Assert.AreEqual(toggleValues[6], GetPropertyValue<bool>(savedProps, "fancyzones_zoneSetChange_moveWindows"));
            Assert.AreEqual(toggleValues[7], GetPropertyValue<bool>(savedProps, "fancyzones_appLastZone_moveWindows"));
            Assert.AreEqual(toggleValues[8], GetPropertyValue<bool>(savedProps, "fancyzones_restoreSize"));
            Assert.AreEqual(toggleValues[9], GetPropertyValue<bool>(savedProps, "use_cursorpos_editor_startupscreen"));
            Assert.AreEqual(toggleValues[10], GetPropertyValue<bool>(savedProps, "fancyzones_show_on_all_monitors"));
            Assert.AreEqual(toggleValues[11], GetPropertyValue<bool>(savedProps, "fancyzones_span_zones_across_monitors"));
            Assert.AreEqual(toggleValues[12], GetPropertyValue<bool>(savedProps, "fancyzones_makeDraggedWindowTransparent"));
        }

        [TestMethod]
        public void HighlightOpacitySetValue()
        {
            WindowsElement editor = session.FindElementByName("Zone opacity (%)");
            Assert.IsNotNull(editor);

            SetOpacity(editor, "50");
            SaveAndCheckOpacitySettings(editor, 50);

            SetOpacity(editor, "-50");
            SaveAndCheckOpacitySettings(editor, 0);

            SetOpacity(editor, "200");
            SaveAndCheckOpacitySettings(editor, 100);

            //for invalid input values previously saved value expected
            SetOpacity(editor, "asdf");
            SaveAndCheckOpacitySettings(editor, 100);

            SetOpacity(editor, "*");
            SaveAndCheckOpacitySettings(editor, 100);

            SetOpacity(editor, OpenQA.Selenium.Keys.Return);
            SaveAndCheckOpacitySettings(editor, 100);

            Clipboard.SetText("Hello, clipboard");
            SetOpacity(editor, OpenQA.Selenium.Keys.Control + "v");
            SaveAndCheckOpacitySettings(editor, 100);
        }

        [TestMethod]
        public void HighlightOpacityIncreaseValue()
        {
            WindowsElement editor = session.FindElementByName("Zone opacity (%)");
            Assert.IsNotNull(editor);

            SetOpacity(editor, "99");
            SaveAndCheckOpacitySettings(editor, 99);

            System.Drawing.Rectangle editorRect = editor.Rect;

            Actions action = new Actions(session);
            action.MoveToElement(editor).MoveByOffset(editorRect.Width / 2 + 10, -editorRect.Height / 4).Perform();
            WaitSeconds(1);

            action.Click().Perform();
            Assert.AreEqual("100\r\n", editor.Text);
            SaveAndCheckOpacitySettings(editor, 100);

            action.Click().Perform();
            Assert.AreEqual("100\r\n", editor.Text);
            SaveAndCheckOpacitySettings(editor, 100);
        }

        [TestMethod]
        public void HighlightOpacityDecreaseValue()
        {

            WindowsElement editor = session.FindElementByName("Zone opacity (%)");
            Assert.IsNotNull(editor);

            SetOpacity(editor, "1");
            SaveAndCheckOpacitySettings(editor, 1);

            System.Drawing.Rectangle editorRect = editor.Rect;

            Actions action = new Actions(session);
            action.MoveToElement(editor).MoveByOffset(editorRect.Width / 2 + 10, editorRect.Height / 4).Perform();
            WaitSeconds(1);

            action.Click().Perform();
            Assert.AreEqual("0\r\n", editor.Text);
            SaveAndCheckOpacitySettings(editor, 0);

            action.Click().Perform();
            Assert.AreEqual("0\r\n", editor.Text);
            SaveAndCheckOpacitySettings(editor, 0);
        }

        [TestMethod]
        public void HighlightOpacityClearValueButton()
        {
            ScrollDown(3);
            WindowsElement editor = session.FindElementByName("Zone opacity (%)");
            Assert.IsNotNull(editor);

            editor.Click(); //activate
            AppiumWebElement clearButton = editor.FindElementByName("Clear value");
            Assert.IsNotNull(clearButton);

            /*element is not pointer- or keyboard interactable.*/
            Actions action = new Actions(session);
            action.MoveToElement(clearButton).Click().Perform();

            Assert.AreEqual("\r\n", editor.Text);
        }

        [TestMethod]
        public void HighlightColorSlidersTest()
        {
            ScrollDown(4);

            ReadOnlyCollection<WindowsElement> saturationAndBrightness = session.FindElementsByName("Saturation and brightness");
            ReadOnlyCollection<WindowsElement> hue = session.FindElementsByName("Hue");
            ReadOnlyCollection<WindowsElement> hex = session.FindElementsByXPath("//Edit[@Name=\"Hex\"]");
            ReadOnlyCollection<WindowsElement> red = session.FindElementsByXPath("//Edit[@Name=\"Red\"]");
            ReadOnlyCollection<WindowsElement> green = session.FindElementsByXPath("//Edit[@Name=\"Green\"]");
            ReadOnlyCollection<WindowsElement> blue = session.FindElementsByXPath("//Edit[@Name=\"Blue\"]");

            TestColorSliders(saturationAndBrightness[2], hue[2], hex[2], red[2], green[2], blue[2], "fancyzones_zoneBorderColor");

            new Actions(session).MoveToElement(saturationAndBrightness[2]).MoveByOffset(saturationAndBrightness[2].Rect.Width / 2 + 10, 0)
                .Click().SendKeys(OpenQA.Selenium.Keys.PageUp).Perform();
            TestColorSliders(saturationAndBrightness[1], hue[1], hex[1], red[1], green[1], blue[1], "fancyzones_zoneColor");

            new Actions(session).MoveToElement(saturationAndBrightness[1]).MoveByOffset(saturationAndBrightness[1].Rect.Width / 2 + 10, 0)
                .Click().SendKeys(OpenQA.Selenium.Keys.PageDown + OpenQA.Selenium.Keys.PageDown).SendKeys(OpenQA.Selenium.Keys.PageUp + OpenQA.Selenium.Keys.PageUp).Perform();
            TestColorSliders(saturationAndBrightness[0], hue[0], hex[0], red[0], green[0], blue[0], "fancyzones_zoneHighlightColor");
        }

        [TestMethod]
        public void HighlightColorTest()
        {
            ScrollDown(2);

            WindowsElement saturationAndBrightness = session.FindElementByName("Saturation and brightness");
            WindowsElement hue = session.FindElementByName("Hue");
            WindowsElement hex = session.FindElementByXPath("//Edit[@Name=\"Hex\"]");

            Assert.IsNotNull(saturationAndBrightness);
            Assert.IsNotNull(hue);
            Assert.IsNotNull(hex);

            hex.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.Backspace);
            hex.SendKeys("63c99a");
            new Actions(session).MoveToElement(hex).MoveByOffset(0, hex.Rect.Height).Click().Perform();

            Assert.AreEqual("Saturation 51 brightness 79", saturationAndBrightness.Text);
            Assert.AreEqual("152", hue.Text);

            SaveChanges();
            WaitSeconds(1);
            Assert.AreEqual("#63c99a", GetPropertyValue<string>("fancyzones_zoneHighlightColor"));
        }

        [TestMethod]
        public void HighlightRGBInputsTest()
        {
            ScrollDown(2);

            TestRgbInput("Red");
            TestRgbInput("Green");
            TestRgbInput("Blue");
        }

        [TestMethod]
        public void HighlightHexInputTest()
        {
            ScrollDown(2);

            WindowsElement hexInput = session.FindElementByXPath("//Edit[@Name=\"Hex\"]");
            Assert.IsNotNull(hexInput);

            hexInput.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.Backspace);

            string invalidSymbols = "qwrtyuiopsghjklzxvnm,./';][{}:`~!#@$%^&*()_-+=\"\'\\";
            foreach (char symbol in invalidSymbols)
            {
                hexInput.SendKeys(symbol.ToString());
                Assert.AreEqual("", hexInput.Text.Trim());
            }

            string validSymbols = "0123456789abcdef";
            foreach (char symbol in validSymbols)
            {
                hexInput.SendKeys(symbol.ToString());
                Assert.AreEqual(symbol.ToString(), hexInput.Text.Trim());
                hexInput.SendKeys(OpenQA.Selenium.Keys.Backspace);
            }

            //too many symbols
            hexInput.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.Backspace);
            hexInput.SendKeys("000000");
            hexInput.SendKeys("1");
            Assert.AreEqual("000000\r\n", hexInput.Text);

            //short string
            hexInput.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.Backspace);
            hexInput.SendKeys("000");
            new Actions(session).MoveToElement(hexInput).MoveByOffset(0, hexInput.Rect.Height).Click().Perform();
            Assert.AreEqual("000000\r\n", hexInput.Text);

            hexInput.SendKeys(OpenQA.Selenium.Keys.Control + OpenQA.Selenium.Keys.Backspace);
            hexInput.SendKeys("1234");
            new Actions(session).MoveToElement(hexInput).MoveByOffset(0, hexInput.Rect.Height).Click().Perform();
            Assert.AreEqual("112233\r\n", hexInput.Text);
        }

        [TestMethod]
        public void ExcludeApps()
        {
            WindowsElement input = session.FindElementByXPath("//Edit[contains(@Name, \"exclude\")]");
            Assert.IsNotNull(input);
            ClearInput(input);

            string inputValue;

            //valid
            inputValue = "Notepad\nChrome";
            input.SendKeys(inputValue);
            SaveChanges();
            ClearInput(input);
            WaitSeconds(1);
            Assert.AreEqual(inputValue, GetPropertyValue<string>("fancyzones_excluded_apps"));

            //invalid
            inputValue = "Notepad Chrome";
            input.SendKeys(inputValue);
            SaveChanges();
            ClearInput(input);
            WaitSeconds(1);
            Assert.AreEqual(inputValue, GetPropertyValue<string>("fancyzones_excluded_apps"));

            inputValue = "Notepad,Chrome";
            input.SendKeys(inputValue);
            SaveChanges();
            ClearInput(input);
            WaitSeconds(1);
            Assert.AreEqual(inputValue, GetPropertyValue<string>("fancyzones_excluded_apps"));

            inputValue = "Note*";
            input.SendKeys(inputValue);
            SaveChanges();
            ClearInput(input);
            WaitSeconds(1);
            Assert.AreEqual(inputValue, GetPropertyValue<string>("fancyzones_excluded_apps"));

            inputValue = "Кириллица";
            input.SendKeys(inputValue);
            SaveChanges();
            ClearInput(input);
            WaitSeconds(1);
            Assert.AreEqual(inputValue, GetPropertyValue<string>("fancyzones_excluded_apps"));
        }

        [TestMethod]
        public void ExitDialogSave()
        {
            WindowsElement toggle = session.FindElementByXPath("//Pane[@Name=\"PowerToys Settings\"]/*[@LocalizedControlType=\"toggleswitch\"]");
            Assert.IsNotNull(toggle);

            bool initialToggleValue = toggle.GetAttribute("Toggle.ToggleState") == "1";

            toggle.Click();
            CloseSettings();
            WindowsElement exitDialog = session.FindElementByName("Changes not saved");
            Assert.IsNotNull(exitDialog);

            exitDialog.FindElementByName("Save").Click();

            //check if window still opened
            WindowsElement powerToysWindow = session.FindElementByXPath("//Window[@Name=\"PowerToys Settings\"]");
            Assert.IsNotNull(powerToysWindow);

            //check settings change
            JObject savedProps = GetProperties();

            Assert.AreNotEqual(initialToggleValue, GetPropertyValue<bool>(savedProps, "fancyzones_shiftDrag"));

            //return initial app state
            toggle.Click();
        }

        [TestMethod]
        public void ExitDialogExit()
        {
            WindowsElement toggle = session.FindElementByXPath("//Pane[@Name=\"PowerToys Settings\"]/*[@LocalizedControlType=\"toggleswitch\"]");
            Assert.IsNotNull(toggle);

            bool initialToggleValue = toggle.GetAttribute("Toggle.ToggleState") == "1";

            toggle.Click();
            CloseSettings();

            WindowsElement exitDialog = session.FindElementByName("Changes not saved");
            Assert.IsNotNull(exitDialog);

            exitDialog.FindElementByName("Exit").Click();

            //check if window still opened
            try
            {
                WindowsElement powerToysWindow = session.FindElementByXPath("//Window[@Name=\"PowerToys Settings\"]");
                Assert.IsNull(powerToysWindow);
            }
            catch (OpenQA.Selenium.WebDriverException)
            {
                //window is no longer available, which is expected
            }

            //return initial app state
            Init();

            //check settings change
            JObject savedProps = GetProperties();
            Assert.AreEqual(initialToggleValue, GetPropertyValue<bool>(savedProps, "fancyzones_shiftDrag"));
        }

        [TestMethod]
        public void ExitDialogCancel()
        {
            WindowsElement toggle = session.FindElementByXPath("//Pane[@Name=\"PowerToys Settings\"]/*[@LocalizedControlType=\"toggleswitch\"]");
            Assert.IsNotNull(toggle);

            toggle.Click();
            CloseSettings();
            WindowsElement exitDialog = session.FindElementByName("Changes not saved");
            Assert.IsNotNull(exitDialog);

            exitDialog.FindElementByName("Cancel").Click();

            //check if window still opened
            WindowsElement powerToysWindow = session.FindElementByXPath("//Window[@Name=\"PowerToys Settings\"]");
            Assert.IsNotNull(powerToysWindow);

            //check settings change
            JObject savedProps = GetProperties();
            JObject initialProps = _initialSettingsJson["properties"].ToObject<JObject>();
            Assert.AreEqual(GetPropertyValue<bool>(initialProps, "fancyzones_shiftDrag"), GetPropertyValue<bool>(savedProps, "fancyzones_shiftDrag"));

            //return initial app state
            toggle.Click();
            SaveChanges();
        }

        [TestMethod]
        public void ConfigureHotkey()
        {
            WindowsElement input = session.FindElementByXPath("//Edit[contains(@Name, \"hotkey\")]");
            Assert.IsNotNull(input);

            for (int i = 0; i < 16; i++)
            {
                TestHotkey(input, i, OpenQA.Selenium.Keys.End, "End");
            }
        }

        [TestMethod]
        public void ConfigureLocalSymbolHotkey()
        {
            WindowsElement input = session.FindElementByXPath("//Edit[contains(@Name, \"hotkey\")]");
            Assert.IsNotNull(input);
            TestHotkey(input, 0, "ё", "Ё");
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            Setup(context);
            Assert.IsNotNull(session);

            Init();
        }

        [ClassCleanup]
        public static void ClassCleanup()
        {
            CloseSettings();

            try
            {
                WindowsElement exitDialogButton = session.FindElementByName("Exit");
                if (exitDialogButton != null)
                {
                    exitDialogButton.Click();
                }
            }
            catch (OpenQA.Selenium.WebDriverException)
            {
                //element couldn't be located
            }

            ExitPowerToys();
            TearDown();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            if (session == null)
                return;

            try
            {
                _initialSettingsJson = JObject.Parse(_initialFancyZonesSettings);
            }
            catch (Newtonsoft.Json.JsonReaderException)
            {
                //empty settings
            }
        }

        [TestCleanup]
        public void TestCleanup()
        {
            ScrollUp();
        }
    }
}
