using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.PowerToys.Settings.UI.Lib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.Json;

namespace ViewModelTests
{
    [TestClass]
    public class PowerLauncher
    {
        class SendCallbackMock
        {
            public int TimesSent { get; set; }
            public void OnSend(PowerLauncherSettings settings)
            {
                TimesSent++;
            }
        }
        private PowerLauncherViewModel viewModel;
        private PowerLauncherSettings mockSettings;
        private SendCallbackMock sendCallbackMock;


        [TestInitialize]
        public void Initialize()
        {
            mockSettings = new PowerLauncherSettings();
            sendCallbackMock = new SendCallbackMock();

            viewModel = new PowerLauncherViewModel(
                mockSettings,
                new PowerLauncherViewModel.SendCallback(sendCallbackMock.OnSend)
                );
        }

        [TestMethod]
        public void SearchPreference_ShouldUpdatePreferences()
        {
            viewModel.SearchResultPreference = "SearchOptionsAreNotValidated";
            viewModel.SearchTypePreference = "SearchOptionsAreNotValidated";

            Assert.AreEqual(sendCallbackMock.TimesSent, 2);
            Assert.IsTrue(mockSettings.properties.search_result_preference == "SearchOptionsAreNotValidated");
            Assert.IsTrue(mockSettings.properties.search_type_preference == "SearchOptionsAreNotValidated");
        }

        public void AssertHotkeySettings(HotkeySettings setting, bool win, bool ctrl, bool alt, bool shift, int code)
        {
            Assert.AreEqual(win, setting.Win);
            Assert.AreEqual(ctrl, setting.Ctrl);
            Assert.AreEqual(alt, setting.Alt);
            Assert.AreEqual(shift, setting.Shift);
            Assert.AreEqual(code, setting.Code);
        }

        [TestMethod]
        public void Hotkeys_ShouldUpdateHotkeys()
        {
            var openPowerLauncher = new HotkeySettings();
            openPowerLauncher.Win = true;
            openPowerLauncher.Code = (int)Windows.System.VirtualKey.S;


            var openFileLocation = new HotkeySettings();
            openFileLocation.Ctrl = true;
            openFileLocation.Code = (int)Windows.System.VirtualKey.A;

            var openConsole = new HotkeySettings();
            openConsole.Alt = true;
            openConsole.Code = (int)Windows.System.VirtualKey.D;

            var copyFileLocation = new HotkeySettings();
            copyFileLocation.Shift = true;
            copyFileLocation.Code = (int)Windows.System.VirtualKey.F;

            viewModel.OpenPowerLauncher = openPowerLauncher;
            viewModel.OpenFileLocation = openFileLocation;
            viewModel.OpenConsole = openConsole;
            viewModel.CopyPathLocation = copyFileLocation;

            Assert.AreEqual(4, sendCallbackMock.TimesSent);

            AssertHotkeySettings(
                mockSettings.properties.open_powerlauncher,
                true,
                false,
                false,
                false,
                (int)Windows.System.VirtualKey.S
                );
            AssertHotkeySettings(
                mockSettings.properties.open_file_location,
                false,
                true,
                false,
                false,
                (int)Windows.System.VirtualKey.A
                );
            AssertHotkeySettings(
                mockSettings.properties.open_console,
                false,
                false,
                true,
                false,
                (int)Windows.System.VirtualKey.D
                );
            AssertHotkeySettings(
                mockSettings.properties.copy_path_location,
                false,
                false,
                false,
                true,
                (int)Windows.System.VirtualKey.F
                );
        }

        [TestMethod]
        public void Override_ShouldUpdateOverrides()
        {
            viewModel.OverrideWinRKey = true;
            viewModel.OverrideWinSKey = false;


            Assert.AreEqual(1, sendCallbackMock.TimesSent);

            Assert.IsTrue(mockSettings.properties.override_win_r_key);
            Assert.IsFalse(mockSettings.properties.override_win_s_key);
        }
    }
}
