// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using System.IO.Abstractions.TestingHelpers;
using System.Text.Json;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace ViewModelTests
{
    [TestClass]
    public class PowerOcr
    {
        private PowerOcrSettings _settings;
        private Mock<SettingsUtils> _settingsUtils;
        private List<string> _messages;

        [TestInitialize]
        public void Initialize()
        {
            _settings = new PowerOcrSettings();
            _settingsUtils = new Mock<SettingsUtils>(MockBehavior.Strict, new MockFileSystem(), null);
            _messages = new List<string>();
        }

        [TestMethod]
        [DataRow("Hungarian")]
        [DataRow("magyar")]
        public void RefreshMatchesPersistedNativeNameWithoutSendingSettings(string displayName)
        {
            _settings.Properties.PreferredLanguage = "magyar";
            var culture = CultureInfo.GetCultureInfo("en-US");
            var languages = new[] { ("magyar", displayName), ("English", culture.DisplayName) };
            using var viewModel = CreateViewModel();

            viewModel.UpdateLanguages(languages, culture);
            viewModel.UpdateLanguages(languages, culture);

            Assert.AreEqual(1, viewModel.LanguageIndex);
            Assert.AreEqual("Magyar", viewModel.AvailableLanguages[viewModel.LanguageIndex]);
            Assert.AreEqual("magyar", _settings.Properties.PreferredLanguage);
            Assert.AreEqual(0, _messages.Count);
        }

        [TestMethod]
        public void ChangedLanguagePersistsNativeNameAcrossDropdownAndPageReopening()
        {
            _settings.Properties.PreferredLanguage = "English";
            var culture = CultureInfo.GetCultureInfo("en-US");
            var languages = new[] { ("English", culture.DisplayName), ("magyar", "Hungarian") };
            using (var viewModel = CreateViewModel())
            {
                viewModel.UpdateLanguages(languages, culture);
                viewModel.LanguageIndex = 1;
                viewModel.LanguageIndex = 1;
                viewModel.UpdateLanguages(languages, culture);

                Assert.AreEqual(1, viewModel.LanguageIndex);
                Assert.AreEqual("magyar", _settings.Properties.PreferredLanguage);
                Assert.AreEqual(1, _messages.Count);
            }

            using var message = JsonDocument.Parse(_messages[0]);
            Assert.AreEqual(
                "magyar",
                message.RootElement.GetProperty("powertoys").GetProperty(PowerOcrSettings.ModuleName)
                    .GetProperty("properties").GetProperty("PreferredLanguage").GetString());

            using var reopenedViewModel = CreateViewModel();
            reopenedViewModel.UpdateLanguages(languages, culture);

            Assert.AreEqual(1, reopenedViewModel.LanguageIndex);
            Assert.AreEqual("magyar", _settings.Properties.PreferredLanguage);
            Assert.AreEqual(1, _messages.Count);
        }

        [TestMethod]
        public void RefreshAfterComboBoxSelectionResetDoesNotSendUnchangedSettings()
        {
            _settings.Properties.PreferredLanguage = "magyar";
            var culture = CultureInfo.GetCultureInfo("en-US");
            var languages = new[] { ("English", culture.DisplayName), ("magyar", "Hungarian") };
            using var viewModel = CreateViewModel();
            viewModel.AvailableLanguages.CollectionChanged += (_, args) =>
            {
                if (args.Action == NotifyCollectionChangedAction.Reset)
                {
                    viewModel.LanguageIndex = -1;
                }
            };

            viewModel.UpdateLanguages(languages, culture);
            viewModel.UpdateLanguages(languages, culture);

            Assert.AreEqual(1, viewModel.LanguageIndex);
            Assert.AreEqual("magyar", _settings.Properties.PreferredLanguage);
            Assert.AreEqual(0, _messages.Count);
        }

        [TestMethod]
        public void RefreshAfterInstallingLanguageUpdatesIndexWithoutSendingSettings()
        {
            _settings.Properties.PreferredLanguage = "magyar";
            var culture = CultureInfo.GetCultureInfo("en-US");
            using var viewModel = CreateViewModel();
            viewModel.UpdateLanguages(new[] { ("English", culture.DisplayName), ("magyar", "Hungarian") }, culture);

            viewModel.UpdateLanguages(
                new[] { ("magyar", "Hungarian"), ("Afrikaans", "Afrikaans"), ("English", culture.DisplayName) },
                culture);

            Assert.AreEqual(2, viewModel.LanguageIndex);
            Assert.AreEqual("magyar", _settings.Properties.PreferredLanguage);
            Assert.AreEqual(0, _messages.Count);
        }

        [TestMethod]
        [DataRow("en-US")]
        [DataRow("en")]
        public void UnavailablePreferenceFallsBackToSystemLanguageOrParent(string fallbackCultureName)
        {
            _settings.Properties.PreferredLanguage = "Unavailable";
            var culture = CultureInfo.GetCultureInfo("en-US");
            var languages = new[]
            {
                ("Afrikaans", "Afrikaans"),
                ("English", CultureInfo.GetCultureInfo(fallbackCultureName).DisplayName),
            };
            using var viewModel = CreateViewModel();

            viewModel.UpdateLanguages(languages, culture);
            viewModel.UpdateLanguages(languages, culture);

            Assert.AreEqual(1, viewModel.LanguageIndex);
            Assert.AreEqual("English", _settings.Properties.PreferredLanguage);
            Assert.AreEqual(1, _messages.Count);
        }

        [TestMethod]
        [DataRow(null)]
        [DataRow("")]
        [DataRow("Unavailable")]
        public void MissingPreferenceWithoutSystemLanguageSelectsFirstLanguage(string preferredLanguage)
        {
            _settings.Properties.PreferredLanguage = preferredLanguage;
            using var viewModel = CreateViewModel();

            viewModel.UpdateLanguages(
                new[] { ("Zulu", "Zulu"), ("Afrikaans", "Afrikaans") },
                CultureInfo.GetCultureInfo("en-US"));

            Assert.AreEqual(0, viewModel.LanguageIndex);
            Assert.AreEqual("Afrikaans", viewModel.AvailableLanguages[viewModel.LanguageIndex]);
        }

        [TestMethod]
        public void RemovedPreferenceWithoutSystemLanguageFallsBackToFirstLanguage()
        {
            _settings.Properties.PreferredLanguage = "magyar";
            var culture = CultureInfo.GetCultureInfo("en-US");
            using var viewModel = CreateViewModel();
            viewModel.UpdateLanguages(new[] { ("Afrikaans", "Afrikaans"), ("magyar", "Hungarian") }, culture);

            viewModel.UpdateLanguages(new[] { ("Afrikaans", "Afrikaans") }, culture);
            viewModel.UpdateLanguages(new[] { ("Afrikaans", "Afrikaans") }, culture);

            Assert.AreEqual(0, viewModel.LanguageIndex);
            Assert.AreEqual("Afrikaans", _settings.Properties.PreferredLanguage);
            Assert.AreEqual(1, _messages.Count);
        }

        [TestMethod]
        public void NoAvailableLanguagesDoesNotReplacePreferredLanguage()
        {
            _settings.Properties.PreferredLanguage = "magyar";
            var culture = CultureInfo.GetCultureInfo("en-US");
            using var viewModel = CreateViewModel();
            viewModel.UpdateLanguages(new[] { ("English", culture.DisplayName), ("magyar", "Hungarian") }, culture);

            viewModel.UpdateLanguages(Array.Empty<(string NativeName, string DisplayName)>(), culture);

            Assert.AreEqual(0, viewModel.AvailableLanguages.Count);
            Assert.AreEqual("magyar", _settings.Properties.PreferredLanguage);
            Assert.AreEqual(0, _messages.Count);
        }

        [TestMethod]
        [DataRow(-1)]
        [DataRow(2)]
        public void InvalidSelectionDoesNotReplacePreferredLanguage(int languageIndex)
        {
            _settings.Properties.PreferredLanguage = "magyar";
            var culture = CultureInfo.GetCultureInfo("en-US");
            using var viewModel = CreateViewModel();
            viewModel.UpdateLanguages(new[] { ("English", culture.DisplayName), ("magyar", "Hungarian") }, culture);

            viewModel.LanguageIndex = languageIndex;

            Assert.AreEqual("magyar", _settings.Properties.PreferredLanguage);
            Assert.AreEqual(0, _messages.Count);
        }

        private PowerOcrViewModel CreateViewModel()
        {
            var generalSettingsRepository = new Mock<ISettingsRepository<GeneralSettings>>();
            generalSettingsRepository.SetupGet(repository => repository.SettingsConfig).Returns(new GeneralSettings());
            var powerOcrSettingsRepository = new Mock<ISettingsRepository<PowerOcrSettings>>();
            powerOcrSettingsRepository.SetupGet(repository => repository.SettingsConfig).Returns(_settings);

            return new PowerOcrViewModel(
                _settingsUtils.Object,
                generalSettingsRepository.Object,
                powerOcrSettingsRepository.Object,
                message =>
                {
                    _messages.Add(message);
                    return 0;
                });
        }
    }
}
