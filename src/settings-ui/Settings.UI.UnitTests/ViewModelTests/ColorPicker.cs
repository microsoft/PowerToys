// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.UnitTests.BackwardsCompatibility;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class ColorPicker
    {
        /// <summary>
        /// Test if the original settings files were modified.
        /// </summary>
        [TestMethod]
        [DataRow("v0.20.1", "settings.json")] // Color picker was introduced in .20
        [DataRow("v0.21.1", "settings.json")]
        [DataRow("v0.22.0", "settings.json")]
        public void OriginalFilesModificationTest(string version, string fileName)
        {
            // Arrange
            var mockIOProvider = BackCompatTestProperties.GetModuleIOProvider(version, ColorPickerSettings.ModuleName, fileName);
            var settingPathMock = new Mock<ISettingsPath>();

            var mockSettingsUtils = new SettingsUtils(mockIOProvider.Object, settingPathMock.Object);
            ColorPickerSettings originalSettings = mockSettingsUtils.GetSettingsOrDefault<ColorPickerSettings>(ColorPickerSettings.ModuleName);

            var mockGeneralIOProvider = BackCompatTestProperties.GetGeneralSettingsIOProvider(version);

            var mockGeneralSettingsUtils = new SettingsUtils(mockGeneralIOProvider.Object, settingPathMock.Object);
            GeneralSettings originalGeneralSettings = mockGeneralSettingsUtils.GetSettingsOrDefault<GeneralSettings>();
            var generalSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<GeneralSettings>(mockGeneralSettingsUtils);
            var colorPickerSettingsRepository = new BackCompatTestProperties.MockSettingsRepository<ColorPickerSettings>(mockSettingsUtils);

            // Act
            // Initialise View Model with test Config files
            using (var viewModel = new ColorPickerViewModel(
                mockSettingsUtils,
                generalSettingsRepository,
                colorPickerSettingsRepository,
                ColorPickerIsEnabledByDefaultIPC))
            {
                // Assert
                // Verify that the old settings persisted
                Assert.AreEqual(originalGeneralSettings.Enabled.ColorPicker, viewModel.IsEnabled);
                Assert.AreEqual(originalSettings.Properties.ActivationShortcut.ToString(), viewModel.ActivationShortcut.ToString());
                Assert.AreEqual(originalSettings.Properties.ChangeCursor, viewModel.ChangeCursor);

                // Verify that the stub file was used
                var expectedCallCount = 2;  // once via the view model, and once by the test (GetSettings<T>)
                BackCompatTestProperties.VerifyModuleIOProviderWasRead(mockIOProvider, ColorPickerSettings.ModuleName, expectedCallCount);
                BackCompatTestProperties.VerifyGeneralSettingsIOProviderWasRead(mockGeneralIOProvider, expectedCallCount);
            }
        }

        [TestMethod]
        public void ColorPickerIsEnabledByDefault()
        {
            var mockSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<ColorPickerSettings>();
            using (var viewModel = new ColorPickerViewModel(
                ISettingsUtilsMocks.GetStubSettingsUtils<ColorPickerSettings>().Object,
                SettingsRepository<GeneralSettings>.GetInstance(ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>().Object),
                SettingsRepository<ColorPickerSettings>.GetInstance(new SettingsUtils()),
                ColorPickerIsEnabledByDefaultIPC))
            {
                Assert.IsTrue(viewModel.IsEnabled);
            }
        }

        private static int ColorPickerIsEnabledByDefaultIPC(string msg)
        {
            OutGoingGeneralSettings snd = JsonSerializer.Deserialize<OutGoingGeneralSettings>(msg);
            Assert.IsTrue(snd.GeneralSettings.Enabled.ColorPicker);
            return 0;
        }
    }
}
