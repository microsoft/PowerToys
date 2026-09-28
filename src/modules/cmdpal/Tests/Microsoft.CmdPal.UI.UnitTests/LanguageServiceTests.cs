// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CmdPal.UI.Services;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.CmdPal.UI.UnitTests;

[TestClass]
[DoNotParallelize]
public class LanguageServiceTests
{
    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void MissingManifestLanguages_UsesFallbackLanguages(bool nullLanguages)
    {
        var service = new LanguageService(["en-US"], nullLanguages ? null : [], isCiBuild: true);

        string[] expectedLanguages = ["en-US", "cs-CZ", "zh-TW"];
        CollectionAssert.IsSubsetOf(expectedLanguages, service.AvailableLanguages.ToArray());
        Assert.IsFalse(string.IsNullOrEmpty(service.CurrentLanguageTag));
    }

    [TestMethod]
    [DataRow("en-GB", "", "en-US")]
    [DataRow("cs", "", "cs-CZ")]
    [DataRow("sk-SK", "fr-FR", "en-US")]
    [DataRow("cs-CZ", "qps-PLOC", "cs-CZ")]
    public void DefaultOrUnavailableLanguage_ClearsDiagnosticOverrideAndRestoresCulture(string systemLanguage, string setting, string expectedCulture)
    {
        var appliedOverride = string.Empty;
        var previousCulture = CultureInfo.CurrentUICulture;
        var previousDefaultCulture = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            var service = new LanguageService(
                [systemLanguage],
                ["en-US", "cs-CZ", "de-DE", "qps-PLOC"],
                isCiBuild: true,
                setLanguageOverride: tag =>
                {
                    Assert.IsTrue(global::Windows.Globalization.Language.IsWellFormed(tag), "The SDK requires a nonempty BCP 47 tag.");
                    appliedOverride = tag;
                });
            Assert.AreEqual("de-DE", service.ApplyLanguageOverride("de-DE"));
            Assert.AreEqual("de-DE", appliedOverride);

            Assert.AreEqual(string.Empty, service.ApplyLanguageOverride(setting));
            Assert.AreEqual(expectedCulture, appliedOverride);
            Assert.AreEqual(expectedCulture, CultureInfo.CurrentUICulture.Name);
            Assert.AreEqual(expectedCulture, CultureInfo.DefaultThreadCurrentUICulture?.Name);
            Assert.AreEqual(expectedCulture, service.CurrentLanguageTag);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
            CultureInfo.DefaultThreadCurrentUICulture = previousDefaultCulture;
        }
    }

    [TestMethod]
    [DataRow("invalid_language", "en-US")]
    [DataRow("en-US", "invalid_language")]
    public void InvalidLanguages_FallBackToEnglish(string preferredLanguage, string availableLanguage)
    {
        var service = new LanguageService([preferredLanguage], [availableLanguage], isCiBuild: true);

        Assert.AreEqual("en-US", service.SystemLanguageTag);
        Assert.AreEqual("en-US", service.CurrentLanguageTag);
        Assert.AreEqual("en-US", service.GetEffectiveLanguageTag("invalid_language"));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void OverrideFailure_FallsBackWithoutPreventingStartup(bool fallbackFails)
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        var previousDefaultCulture = CultureInfo.DefaultThreadCurrentUICulture;
        var attemptedOverrides = new List<string>();
        try
        {
            var service = new LanguageService(["en-US"], ["en-US", "de-DE"], isCiBuild: true, setLanguageOverride: tag =>
            {
                attemptedOverrides.Add(tag);
                if (tag == "de-DE" || fallbackFails)
                {
                    throw new InvalidOperationException("Language API unavailable");
                }
            });

            Assert.AreEqual(string.Empty, service.ApplyLanguageOverride("de-DE"));
            string[] expectedOverrides = ["de-DE", "en-US"];
            CollectionAssert.AreEqual(expectedOverrides, attemptedOverrides);
            Assert.AreEqual("en-US", service.CurrentLanguageTag);
            Assert.AreEqual("en-US", CultureInfo.CurrentUICulture.Name);
            Assert.AreEqual("en-US", CultureInfo.DefaultThreadCurrentUICulture?.Name);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
            CultureInfo.DefaultThreadCurrentUICulture = previousDefaultCulture;
        }
    }

    [TestMethod]
    public void NoMatchingLanguage_KeepsTheDefaultWithoutFallbackMatching()
    {
        // Windows rejects zh-CN for a zh-Hant reader as a script mismatch. That answer must stand:
        // a clean no-match is not an API failure, so the fallback must not substitute zh-CN here.
        var service = new LanguageService(["zh-Hant"], ["en-US", "zh-CN"], isCiBuild: true);

        Assert.AreEqual("en-US", service.SystemLanguageTag);
    }

    [TestMethod]
    [DataRow("cs-CZ", "cs-CZ")]
    [DataRow("cs", "cs-CZ")]
    [DataRow("cs-SK", "cs-CZ")]
    [DataRow("sk-SK;cs-CZ", "cs-CZ")]
    [DataRow("sk-SK;de-DE;cs-CZ", "de-DE")]
    [DataRow("sk-SK", "en-US")]
    [DataRow("", "en-US")]
    public void PreferenceMatching_WithoutScoringApi_FollowsPreferenceOrder(string preferences, string expectedLanguage)
    {
        string[] candidates = ["en-US", "cs-CZ", "de-DE"];

        var matched = LanguageService.MatchPreferredLanguage(
            preferences.Split(';', StringSplitOptions.RemoveEmptyEntries),
            candidates,
            "en-US");

        Assert.AreEqual(expectedLanguage, matched);
    }

    [TestMethod]
    [DataRow("zh-Hant", "en-US;zh-CN;zh-TW", "zh-TW")]
    [DataRow("zh-Hans", "en-US;zh-CN;zh-TW", "zh-CN")]
    [DataRow("zh-HK", "en-US;zh-CN;zh-TW", "zh-TW")]
    [DataRow("zh-MO", "en-US;zh-CN;zh-TW", "zh-TW")]
    [DataRow("cs", "en-US;cs-CZ", "cs-CZ")]
    [DataRow("en", "en-US", "en-US")]
    public void PreferenceMatching_KeepsTheRequestedScript(string preference, string candidates, string expectedLanguage)
    {
        var matched = LanguageService.MatchPreferredLanguage([preference], candidates.Split(';'), "en-US");

        Assert.AreEqual(expectedLanguage, matched);
    }

    [TestMethod]
    [DataRow("zh-Hant", "en-US;zh-Hans")]
    [DataRow("zh-Hant", "en-US;zh-CN")]
    [DataRow("zh-Hans", "en-US;zh-TW")]
    [DataRow("zh", "en-US;zh-CN")]
    [DataRow("zh", "en-US;zh-Hant")]
    [DataRow("sr-Latn", "en-US;sr-Cyrl")]
    [DataRow("sr-Latn-RS", "en-US;sr-Cyrl-RS")]
    [DataRow("sr", "en-US;sr-Latn")]
    public void PreferenceMatching_RejectsAnotherScript(string preference, string candidates)
    {
        // Windows treats a script mismatch as a nonmatch, so the default must stand instead of the
        // wrong script. The fallback also declines an omitted script against a named one, which is
        // narrower than Windows: it matches a redundant script such as "cs-Latn-CZ" with "cs-CZ".
        var matched = LanguageService.MatchPreferredLanguage([preference], candidates.Split(';'), "en-US");

        Assert.AreEqual("en-US", matched);
    }

    [TestMethod]
    public void PreferenceMatching_PrefersExactTagOverCompatibleTag()
    {
        string[] candidates = ["pt-BR", "pt-PT"];

        Assert.AreEqual("pt-PT", LanguageService.MatchPreferredLanguage(["pt-PT"], candidates, "en-US"));
        Assert.AreEqual("pt-BR", LanguageService.MatchPreferredLanguage(["pt"], candidates, "en-US"));
    }

    [TestMethod]
    public void CiBuild_RemovesDiscoveredPseudoLocales()
    {
        var service = new LanguageService(["en-US"], ["en-US", "QPS-PLOC", "qps-ploca", "qps-plocm"], isCiBuild: true);

        string[] expected = ["en-US"];
        CollectionAssert.AreEqual(expected, service.AvailableLanguages.ToArray());
    }

    [TestMethod]
    public void DevelopmentBuild_DeduplicatesPseudoLocaleIgnoringCase()
    {
        var service = new LanguageService(["en-US"], ["EN-US", "QPS-PLOC", "qps-ploc"], isCiBuild: false);

        string[] expected = ["EN-US", "QPS-PLOC"];
        CollectionAssert.AreEqual(expected, service.AvailableLanguages.ToArray());
    }

    [TestMethod]
    public void DevelopmentBuild_AddsMissingPseudoLocale()
    {
        var service = new LanguageService(["en-US"], ["en-US"], isCiBuild: false);

        string[] expected = ["en-US", "qps-PLOC"];
        CollectionAssert.AreEqual(expected, service.AvailableLanguages.ToArray());
    }

    [TestMethod]
    [DataRow(false, null, true)]
    [DataRow(false, "en-US", true)]
    [DataRow(false, "qps-PLOC", false)]
    [DataRow(false, "QPS-PLOC", false)]
    [DataRow(false, "qps-PLOCA", true)]
    [DataRow(true, null, false)]
    [DataRow(true, "en-US", false)]
    [DataRow(true, "qps-PLOC", false)]
    public void PseudoLocalizationNotice_OnlyShownInDevelopmentBuildsWithoutResources(bool isCiBuild, string? manifestLanguage, bool showNotice)
    {
        string[]? manifestLanguages = manifestLanguage is null ? null : ["en-US", manifestLanguage];
        var languageService = new LanguageService(["en-US"], manifestLanguages, isCiBuild);
        var settingsService = new Mock<ISettingsService>();
        settingsService.Setup(s => s.Settings).Returns(new SettingsModel());
        var page = CreateLanguageSettings(settingsService.Object, languageService);

        Assert.AreEqual(showNotice, page.IsPseudoLocalizationMissing);
    }

    [TestMethod]
    [DataRow("cs", "cs-CZ")]
    [DataRow("zh-Hans", "zh-CN")]
    [DataRow("zh-Hant", "zh-TW")]
    public void EffectiveLanguage_UsesWindowsMapping(string systemLanguage, string expectedLanguage)
    {
        var service = new LanguageService([systemLanguage], [expectedLanguage], isCiBuild: true);

        Assert.AreEqual(expectedLanguage, service.GetEffectiveLanguageTag(string.Empty));
        Assert.AreEqual(expectedLanguage, service.GetEffectiveLanguageTag(expectedLanguage));
    }

    [TestMethod]
    [DataRow("pt-PT", "pt-BR")]
    [DataRow("zh-CN", "zh-TW")]
    public void EffectiveLanguage_PreservesDistinctRegionalAndScriptChoices(string firstLanguage, string secondLanguage)
    {
        var service = new LanguageService([firstLanguage], [firstLanguage, secondLanguage], isCiBuild: true);

        Assert.AreNotEqual(service.GetEffectiveLanguageTag(firstLanguage), service.GetEffectiveLanguageTag(secondLanguage));
    }

    [TestMethod]
    public void ReopeningSettings_PreservesPendingRestartAndAllowsReverting()
    {
        var settings = new SettingsModel();
        var settingsService = new Mock<ISettingsService>();
        settingsService.Setup(s => s.Settings).Returns(() => settings);
        settingsService.Setup(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()))
            .Callback<Func<SettingsModel, SettingsModel>, bool>((update, _) => settings = update(settings));
        var languageService = new LanguageService(["en-US"], ["en-US", "de-DE"], isCiBuild: true);
        var firstPage = CreateLanguageSettings(settingsService.Object, languageService);
        firstPage.LanguageIndex = firstPage.Languages.FindIndex(language => language.Tag == "de-DE");
        Assert.IsTrue(firstPage.LanguageChanged);
        Assert.AreEqual("de-DE", settings.Language);

        var reopenedPage = CreateLanguageSettings(settingsService.Object, languageService);
        Assert.IsTrue(reopenedPage.LanguageChanged);

        reopenedPage.LanguageIndex = 0;
        Assert.IsFalse(reopenedPage.LanguageChanged);
        Assert.AreEqual(string.Empty, settings.Language);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(int.MaxValue)]
    public void InvalidSelection_PreservesSavedLanguageAndRestartState(int index)
    {
        var settingsService = new Mock<ISettingsService>();
        settingsService.Setup(s => s.Settings).Returns(new SettingsModel { Language = "de-DE" });
        var languageService = new LanguageService(["en-US"], ["en-US", "de-DE"], isCiBuild: true);
        var page = CreateLanguageSettings(settingsService.Object, languageService);
        var selectedIndex = page.LanguageIndex;
        page.LanguageRestartFailed = true;
        var notifications = 0;
        page.PropertyChanged += (_, _) => notifications++;

        page.LanguageIndex = index;

        Assert.AreEqual(selectedIndex, page.LanguageIndex);
        Assert.AreEqual("de-DE", settingsService.Object.Settings.Language);
        Assert.IsTrue(page.LanguageChanged);
        Assert.IsTrue(page.LanguageRestartFailed);
        Assert.AreEqual(0, notifications);
        settingsService.Verify(s => s.UpdateSettings(It.IsAny<Func<SettingsModel, SettingsModel>>(), It.IsAny<bool>()), Times.Never);
    }

    [TestMethod]
    [DataRow("cs", "cs-CZ", false)]
    [DataRow("pt-PT", "pt-BR", true)]
    [DataRow("zh-Hans", "zh-TW", true)]
    public void RestartState_ComparesMappedLanguages(string systemLanguage, string selectedLanguage, bool restartNeeded)
    {
        var settingsService = new Mock<ISettingsService>();
        settingsService.Setup(s => s.Settings).Returns(new SettingsModel { Language = selectedLanguage });
        var languageService = new LanguageService([systemLanguage], ["en-US", "cs-CZ", "pt-PT", "pt-BR", "zh-CN", "zh-TW"], isCiBuild: true);
        var page = CreateLanguageSettings(settingsService.Object, languageService);

        Assert.AreEqual(restartNeeded, page.LanguageChanged);
    }

    [TestMethod]
    [DataRow("sk-SK;cs-CZ", "cs-CZ")]
    [DataRow("sk-SK;cs", "cs-CZ")]
    [DataRow("de-DE;cs-CZ", "de-DE")]
    [DataRow("cs-CZ;de-DE", "cs-CZ")]
    [DataRow("en-GB;cs-CZ", "en-US")]
    [DataRow("zh-Hant;cs-CZ", "zh-TW")]
    [DataRow("sk-SK;sl-SI", "en-US")]
    [DataRow("qps-ploc;sk-SK;cs-CZ", "cs-CZ")]
    [DataRow("", "en-US")]
    public void DefaultLanguage_UsesSupportedWindowsPreferenceWithoutSpuriousRestart(string preferences, string expectedLanguage)
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        var previousDefaultCulture = CultureInfo.DefaultThreadCurrentUICulture;
        var appliedOverride = "de-DE";
        try
        {
            var service = new LanguageService(
                preferences.Split(';', StringSplitOptions.RemoveEmptyEntries),
                ["en-US", "cs-CZ", "de-DE", "zh-CN", "zh-TW"],
                isCiBuild: false,
                setLanguageOverride: tag => appliedOverride = tag);
            service.ApplyLanguageOverride(string.Empty);
            Assert.AreEqual(expectedLanguage, appliedOverride);
            Assert.AreEqual(expectedLanguage, CultureInfo.CurrentUICulture.Name);
            Assert.AreEqual(expectedLanguage, CultureInfo.DefaultThreadCurrentUICulture?.Name);
            Assert.AreEqual(expectedLanguage, service.CurrentLanguageTag);
            Assert.AreEqual(expectedLanguage, service.GetEffectiveLanguageTag(string.Empty));

            var settingsService = new Mock<ISettingsService>();
            settingsService.Setup(s => s.Settings).Returns(new SettingsModel());
            var page = CreateLanguageSettings(settingsService.Object, service);

            page.LanguageIndex = page.Languages.FindIndex(language => language.Tag == expectedLanguage);
            Assert.IsFalse(page.LanguageChanged);
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
            CultureInfo.DefaultThreadCurrentUICulture = previousDefaultCulture;
        }
    }

    [TestMethod]
    [DataRow(true)]
    [DataRow(false)]
    public void ChangingLanguage_ClearsRestartFailure(bool revertFirst)
    {
        var settingsService = new Mock<ISettingsService>();
        settingsService.Setup(s => s.Settings).Returns(new SettingsModel());
        var languageService = new LanguageService(["en-US"], ["en-US", "de-DE", "fr-FR"], isCiBuild: true);
        var page = CreateLanguageSettings(settingsService.Object, languageService);
        page.LanguageIndex = page.Languages.FindIndex(language => language.Tag == "de-DE");
        page.LanguageRestartFailed = true;
        var failureNotifications = 0;
        page.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(SettingsViewModel.LanguageRestartFailed))
            {
                failureNotifications++;
            }
        };
        if (revertFirst)
        {
            page.LanguageIndex = 0;
            Assert.IsFalse(page.LanguageRestartFailed);
            Assert.IsFalse(page.LanguageChanged);
        }

        page.LanguageIndex = page.Languages.FindIndex(language => language.Tag == "fr-FR");
        Assert.IsFalse(page.LanguageRestartFailed);
        Assert.IsTrue(page.LanguageChanged);
        Assert.AreEqual(1, failureNotifications);
    }

    [TestMethod]
    public void LanguageNames_UseNativeNamesAndUiCultureOrderingAfterDefault()
    {
        var previousCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("cs-CZ");
            var settingsService = new Mock<ISettingsService>();
            settingsService.Setup(s => s.Settings).Returns(new SettingsModel());
            var service = new LanguageService(["en-US"], ["en-US", "de-DE", "cs-CZ"], isCiBuild: true);
            var page = CreateLanguageSettings(settingsService.Object, service);

            CollectionAssert.AreEqual(new[] { string.Empty, "cs-CZ", "de-DE", "en-US" }, page.Languages.Select(language => language.Tag).ToArray());
            StringAssert.StartsWith(page.Languages[2].DisplayName, "Deutsch");
            StringAssert.StartsWith(page.Languages[3].DisplayName, "English");
        }
        finally
        {
            CultureInfo.CurrentUICulture = previousCulture;
        }
    }

    private static SettingsViewModel CreateLanguageSettings(ISettingsService settingsService, ILanguageService languageService)
    {
        // Isolate language initialization from the constructor's unrelated WinUI appearance services.
        var viewModel = (SettingsViewModel)RuntimeHelpers.GetUninitializedObject(typeof(SettingsViewModel));
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        typeof(SettingsViewModel).GetField("_settingsService", flags)!.SetValue(viewModel, settingsService);
        typeof(SettingsViewModel).GetField("_languageService", flags)!.SetValue(viewModel, languageService);
        typeof(SettingsViewModel).GetMethod("InitializeLanguages", flags)!.Invoke(viewModel, [languageService]);
        return viewModel;
    }
}
