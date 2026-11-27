// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO.Abstractions;
using System.Linq;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.Library.Interfaces;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Windows.Globalization;

namespace ViewModelTests
{
    [TestClass]
    public class PowerOcr
    {
        private static readonly string[] LanguageTags = { "en-US", "fr-CA", "ja-JP" };

        [TestMethod]
        public void PreferredLanguageIsReselectedWhenNativeNameDiffersFromDisplayName()
        {
            var languages = LanguageTags
                .Select(tag => new Language(tag))
                .OrderBy(language => language.NativeName)
                .ToList();
            var powerOcrSettings = new PowerOcrSettings();

            using var viewModel = CreateViewModel(powerOcrSettings);
            viewModel.UpdateLanguages(languages);
            int fallbackIndex = viewModel.LanguageIndex;

            // Pick a language that is not the fallback and whose localized name differs from its native name.
            int preferredIndex = Enumerable.Range(0, languages.Count)
                .FirstOrDefault(
                    i => i != fallbackIndex && !languages[i].NativeName.Equals(languages[i].DisplayName, StringComparison.OrdinalIgnoreCase),
                    -1);
            if (preferredIndex < 0)
            {
                Assert.Inconclusive("No test language has a native name that differs from its display name.");
            }

            viewModel.LanguageIndex = preferredIndex;
            Assert.AreEqual(languages[preferredIndex].NativeName, powerOcrSettings.Properties.PreferredLanguage);

            // Reopening the dropdown refreshes the same view model.
            viewModel.UpdateLanguages(languages);
            Assert.AreEqual(preferredIndex, viewModel.LanguageIndex);

            // Reopening the Settings page creates a new view model over the persisted settings.
            using var reopenedViewModel = CreateViewModel(powerOcrSettings);
            reopenedViewModel.UpdateLanguages(languages);
            Assert.AreEqual(preferredIndex, reopenedViewModel.LanguageIndex);
            Assert.AreEqual(languages[preferredIndex].NativeName, powerOcrSettings.Properties.PreferredLanguage);
        }

        private static PowerOcrViewModel CreateViewModel(PowerOcrSettings powerOcrSettings)
        {
            var settingsUtils = new Mock<SettingsUtils>(new FileSystem(), null);
            var generalSettingsRepository = new Mock<ISettingsRepository<GeneralSettings>>();
            generalSettingsRepository.SetupGet(repository => repository.SettingsConfig).Returns(new GeneralSettings());
            var powerOcrSettingsRepository = new Mock<ISettingsRepository<PowerOcrSettings>>();
            powerOcrSettingsRepository.SetupGet(repository => repository.SettingsConfig).Returns(powerOcrSettings);

            return new PowerOcrViewModel(
                settingsUtils.Object,
                generalSettingsRepository.Object,
                powerOcrSettingsRepository.Object,
                _ => 0);
        }
    }
}
