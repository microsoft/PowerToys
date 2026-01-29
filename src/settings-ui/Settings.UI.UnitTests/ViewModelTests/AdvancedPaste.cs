// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.UnitTests.Mocks;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class AdvancedPaste
    {
        private Mock<SettingsUtils> mockAdvancedPasteSettingsUtils;

        [TestInitialize]
        public void SetUpStubSettingUtils()
        {
            mockAdvancedPasteSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<AdvancedPasteSettings>();
        }

        private sealed class TestAdvancedPasteViewModel : AdvancedPasteViewModel
        {
            public TestAdvancedPasteViewModel(
                SettingsUtils settingsUtils,
                ISettingsRepository<GeneralSettings> generalSettingsRepository,
                ISettingsRepository<AdvancedPasteSettings> advancedPasteSettingsRepository,
                Func<string, int> ipcMSGCallBackFunc)
                : base(settingsUtils, generalSettingsRepository, advancedPasteSettingsRepository, ipcMSGCallBackFunc)
            {
            }
        }

        [TestMethod]
        public void ViewModelInitialization_WithEmptySettings_ShouldNotCrash()
        {
            // Arrange
            var mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            var generalSettingsRepository = new Mock<ISettingsRepository<GeneralSettings>>();
            generalSettingsRepository.Setup(x => x.SettingsConfig).Returns(new GeneralSettings());

            var advancedPasteSettings = new AdvancedPasteSettings();
            var advancedPasteSettingsRepository = new Mock<ISettingsRepository<AdvancedPasteSettings>>();
            advancedPasteSettingsRepository.Setup(x => x.SettingsConfig).Returns(advancedPasteSettings);

            Func<string, int> sendMockIPCConfigMSG = msg => 0;

            // Act - Creating the ViewModel should not throw
            var viewModel = new TestAdvancedPasteViewModel(
                mockAdvancedPasteSettingsUtils.Object,
                generalSettingsRepository.Object,
                advancedPasteSettingsRepository.Object,
                sendMockIPCConfigMSG);

            // Assert
            Assert.IsNotNull(viewModel);
            Assert.IsNotNull(viewModel.PasteAIConfiguration, "PasteAIConfiguration should be initialized");
        }

        [TestMethod]
        public void ViewModelInitialization_WithNullPasteAIConfiguration_ShouldInitialize()
        {
            // Arrange
            var mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            var generalSettingsRepository = new Mock<ISettingsRepository<GeneralSettings>>();
            generalSettingsRepository.Setup(x => x.SettingsConfig).Returns(new GeneralSettings());

            var advancedPasteSettings = new AdvancedPasteSettings();
            // Simulate old settings file where PasteAIConfiguration might be null
            advancedPasteSettings.Properties.PasteAIConfiguration = null;

            var advancedPasteSettingsRepository = new Mock<ISettingsRepository<AdvancedPasteSettings>>();
            advancedPasteSettingsRepository.Setup(x => x.SettingsConfig).Returns(advancedPasteSettings);

            Func<string, int> sendMockIPCConfigMSG = msg => 0;

            // Act - Creating the ViewModel should not throw even with null PasteAIConfiguration
            var viewModel = new TestAdvancedPasteViewModel(
                mockAdvancedPasteSettingsUtils.Object,
                generalSettingsRepository.Object,
                advancedPasteSettingsRepository.Object,
                sendMockIPCConfigMSG);

            // Assert
            Assert.IsNotNull(viewModel);
            Assert.IsNotNull(viewModel.PasteAIConfiguration, "PasteAIConfiguration should be initialized even when starting as null");
            Assert.IsNotNull(viewModel.PasteAIConfiguration.Providers, "Providers collection should be initialized");
        }

        [TestMethod]
        public void ViewModelInitialization_WithNullAdditionalActions_ShouldInitialize()
        {
            // Arrange
            var mockGeneralSettingsUtils = ISettingsUtilsMocks.GetStubSettingsUtils<GeneralSettings>();
            var generalSettingsRepository = new Mock<ISettingsRepository<GeneralSettings>>();
            generalSettingsRepository.Setup(x => x.SettingsConfig).Returns(new GeneralSettings());

            var advancedPasteSettings = new AdvancedPasteSettings();
            // Simulate old settings file where AdditionalActions might be null
            advancedPasteSettings.Properties.AdditionalActions = null;

            var advancedPasteSettingsRepository = new Mock<ISettingsRepository<AdvancedPasteSettings>>();
            advancedPasteSettingsRepository.Setup(x => x.SettingsConfig).Returns(advancedPasteSettings);

            Func<string, int> sendMockIPCConfigMSG = msg => 0;

            // Act - Creating the ViewModel should not throw
            var viewModel = new TestAdvancedPasteViewModel(
                mockAdvancedPasteSettingsUtils.Object,
                generalSettingsRepository.Object,
                advancedPasteSettingsRepository.Object,
                sendMockIPCConfigMSG);

            // Assert
            Assert.IsNotNull(viewModel);
        }
    }
}
