// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Runtime.InteropServices;
using ManagedCommon;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Windows.Globalization;
using Windows.System.UserProfile;
using Language = Windows.Globalization.Language;

namespace Microsoft.CmdPal.UI.Services;

internal sealed partial class LanguageService : ILanguageService
{
    // HRESULT_FROM_WIN32(ERROR_NO_MATCH)
    private const int NoMatchHResult = unchecked((int)0x80070491);

    private static readonly string[] FallbackLanguageTags =
    [
        "ar-SA",
        "cs-CZ",
        "de-DE",
        "en-US",
        "es-ES",
        "fa-IR",
        "fr-FR",
        "he-IL",
        "hu-HU",
        "it-IT",
        "ja-JP",
        "ko-KR",
        "nl-NL",
        "pl-PL",
        "pt-BR",
        "pt-PT",
        "ru-RU",
        "sv-SE",
        "tr-TR",
        "uk-UA",
        "zh-CN",
        "zh-TW",
    ];

    private static readonly string[] PseudoLocalizationCultureTags = ["qps-PLOC"];

    private readonly Action<string> _setLanguageOverride;

    public string SystemLanguageTag { get; } = "en-US";

    public IReadOnlyList<string> AvailableLanguages { get; }

    public bool IsPseudoLocalizationMissing { get; }

    public string CurrentLanguageTag { get; private set; }

    public LanguageService()
        : this(ReadLanguages(() => GlobalizationPreferences.Languages), ReadLanguages(() => ApplicationLanguages.ManifestLanguages), BuildInfo.IsCiBuild)
    {
    }

    internal LanguageService(IReadOnlyList<string> preferredLanguages, IReadOnlyList<string>? availableLanguages, bool isCiBuild, Action<string>? setLanguageOverride = null)
    {
        _setLanguageOverride = setLanguageOverride ?? (tag => ApplicationLanguages.PrimaryLanguageOverride = tag);

        // Check the manifest before adding the developer option, which may not have generated resources.
        IsPseudoLocalizationMissing = !isCiBuild &&
            PseudoLocalizationCultureTags.Any(tag => availableLanguages?.Contains(tag, StringComparer.OrdinalIgnoreCase) != true);

        // Unpackaged apps have no manifest languages. The UI uses PRI resources, not satellite assemblies.
        AvailableLanguages = (availableLanguages is { Count: > 0 } ? availableLanguages : FallbackLanguageTags)
            .Where(tag => !isCiBuild || !IsPseudoLocale(tag))
            .Concat(isCiBuild ? [] : PseudoLocalizationCultureTags)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        try
        {
            SystemLanguageTag = ResolveSystemLanguage(preferredLanguages);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to resolve the Windows display language; using en-US", ex);
        }

        SystemLanguageTag = GetEffectiveLanguageTag(SystemLanguageTag);
        CurrentLanguageTag = SystemLanguageTag;
    }

    public string ApplyLanguageOverride(string languageTag)
    {
        var languageOverride = AvailableLanguages.Contains(languageTag, StringComparer.OrdinalIgnoreCase)
            ? languageTag : string.Empty;

        // Use the same Windows language mapping for RESX resources and restart comparisons.
        var culture = CultureInfo.GetCultureInfo(GetEffectiveLanguageTag(languageOverride));
        try
        {
            // The Windows App SDK requires a nonempty tag, so resolve the default again on every startup.
            _setLanguageOverride(string.IsNullOrEmpty(languageOverride) ? SystemLanguageTag : languageOverride);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to apply the language override; using the default language", ex);
            languageOverride = string.Empty;
            culture = CultureInfo.GetCultureInfo(GetEffectiveLanguageTag(string.Empty));
            try
            {
                _setLanguageOverride(culture.Name);
            }
            catch (Exception fallbackException)
            {
                Logger.LogError("Failed to apply the default language override", fallbackException);
            }
        }

        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
        CurrentLanguageTag = culture.Name;

        return languageOverride;
    }

    public string GetEffectiveLanguageTag(string languageTag)
    {
        var effectiveTag = string.IsNullOrEmpty(languageTag) ? SystemLanguageTag : languageTag;
        try
        {
            var mappedTag = Language.GetMuiCompatibleLanguageListFromLanguageTags([effectiveTag]).FirstOrDefault() ?? effectiveTag;
            return CultureInfo.GetCultureInfo(mappedTag).Name;
        }
        catch (Exception ex)
        {
            Logger.LogError($"Failed to map language '{effectiveTag}'; using en-US", ex);
            return "en-US";
        }
    }

    private static IReadOnlyList<string> ReadLanguages(Func<IReadOnlyList<string>?> getLanguages)
    {
        try
        {
            return getLanguages() ?? [];
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to read Windows languages; using fallback languages", ex);
            return [];
        }
    }

    /// <summary>
    /// Picks the supported language that best matches the Windows language preferences.
    /// </summary>
    private string ResolveSystemLanguage(IReadOnlyList<string> languages)
    {
        var candidates = AvailableLanguages.Where(tag => !IsPseudoLocale(tag)).ToArray();
        var defaultLanguage = candidates.FirstOrDefault(tag => tag.Equals("en-US", StringComparison.OrdinalIgnoreCase))
            ?? candidates.FirstOrDefault() ?? "en-US";

        var preferredLanguages = languages.Where(tag => !IsPseudoLocale(tag)).ToArray();
        if (preferredLanguages.Length == 0)
        {
            return defaultLanguage;
        }

        // Windows accounts for preference order, regional fallbacks, and script differences.
        var preferenceList = string.Join(';', preferredLanguages);
        var bestLanguage = defaultLanguage;
        var bestScore = 0.0;
        var scoringFailed = false;
        try
        {
            foreach (var candidate in candidates)
            {
                var result = GetDistanceOfClosestLanguageInList(candidate, preferenceList, ';', out var score);
                if (result == NoMatchHResult)
                {
                    // Windows compared the languages and rejected this one. That is an answer, not a failure.
                    continue;
                }

                if (result < 0)
                {
                    // One language must not discard the scores already collected.
                    Logger.LogWarning($"Failed to score language '{candidate}' (HRESULT 0x{result:X8}).");
                    scoringFailed = true;
                    continue;
                }

                if (score > bestScore)
                {
                    bestLanguage = candidate;
                    bestScore = score;
                }
            }
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            Logger.LogError("Windows language matching is unavailable; comparing the preferences directly", ex);
            return MatchPreferredLanguage(preferredLanguages, candidates, defaultLanguage);
        }

        // Only substitute when Windows never gave an answer. A clean no-match means it rejected every language.
        return scoringFailed && bestScore == 0
            ? MatchPreferredLanguage(preferredLanguages, candidates, defaultLanguage)
            : bestLanguage;
    }

    /// <summary>
    /// Matches the Windows language preferences against the supported languages when the scoring
    /// API is unavailable. Preference order wins, and an exact tag beats a compatible one. This is a
    /// conservative approximation of Windows matching: it prefers no match over a doubtful one.
    /// </summary>
    /// <param name="preferences">The Windows language preferences, most preferred first.</param>
    /// <param name="candidates">The supported app languages.</param>
    /// <param name="fallback">The language to use when nothing matches.</param>
    /// <returns>The best supported language for these preferences.</returns>
    internal static string MatchPreferredLanguage(IReadOnlyList<string> preferences, IReadOnlyList<string> candidates, string fallback)
    {
        foreach (var preference in preferences)
        {
            var exactMatch = candidates.FirstOrDefault(candidate => candidate.Equals(preference, StringComparison.OrdinalIgnoreCase));
            if (exactMatch is not null)
            {
                return exactMatch;
            }

            var compatibleMatch = candidates.FirstOrDefault(candidate => IsCompatibleWith(candidate, preference));
            if (compatibleMatch is not null)
            {
                return compatibleMatch;
            }
        }

        return fallback;
    }

    /// <summary>
    /// Tests whether a supported language can serve a preference. The languages must be written in
    /// the same script, and then be related through the culture parent chain, so that "cs" and
    /// "cs-SK" match "cs-CZ", while "zh-Hant" matches "zh-TW" but never "zh-CN".
    /// See <see cref="SharesScript"/> for where this is narrower than Windows.
    /// </summary>
    private static bool IsCompatibleWith(string candidate, string preference)
    {
        var candidateCulture = FindCulture(candidate);
        var preferenceCulture = FindCulture(preference);
        if (candidateCulture is null || preferenceCulture is null || !SharesScript(candidateCulture, preferenceCulture))
        {
            return false;
        }

        return IsSelfOrAncestor(preferenceCulture, candidateCulture)
            || IsSelfOrAncestor(candidateCulture, preferenceCulture)
            || SharesParent(candidateCulture, preferenceCulture);
    }

    /// <summary>
    /// Tests whether two languages are regional siblings, such as "cs-CZ" and "cs-SK", which Windows
    /// matches. "zh-CN" and "zh-TW" are not siblings, because their parents carry the script.
    /// </summary>
    private static bool SharesParent(CultureInfo candidate, CultureInfo preference)
    {
        var parent = candidate.Parent.Name;
        return !string.IsNullOrEmpty(parent) &&
               parent.Equals(preference.Parent.Name, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Tests whether two languages are written in the same script, counting an omitted script as a
    /// value of its own, so neither "zh-Hans" nor plain "zh" can serve a "zh-Hant" reader.
    /// This is narrower than Windows, which resolves a language's default script and therefore
    /// matches "cs-Latn-CZ" with "cs-CZ" where this returns no match. The fallback cannot tell a
    /// redundant script from a meaningful one, and for a recovery path declining to match is safer
    /// than serving a reader the wrong script.
    /// </summary>
    private static bool SharesScript(CultureInfo candidate, CultureInfo preference) =>
        string.Equals(FindScript(candidate), FindScript(preference), StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns the script subtag that applies to a language, such as "Hant" for "zh-TW" through its
    /// "zh-Hant" parent, or <see langword="null"/> when no language in the chain names a script.
    /// </summary>
    private static string? FindScript(CultureInfo culture)
    {
        for (var current = culture; !string.IsNullOrEmpty(current.Name); current = current.Parent)
        {
            foreach (var subtag in current.Name.Split('-'))
            {
                // A four-letter subtag is a script; primary language subtags are two or three letters.
                if (subtag.Length == 4 && subtag.All(char.IsAsciiLetter))
                {
                    return subtag;
                }
            }
        }

        return null;
    }

    private static bool IsSelfOrAncestor(CultureInfo ancestor, CultureInfo culture)
    {
        // The chain ends at the invariant culture, whose name is empty.
        for (; !string.IsNullOrEmpty(culture.Name); culture = culture.Parent)
        {
            if (culture.Name.Equals(ancestor.Name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsPseudoLocale(string tag)
    {
        return tag.StartsWith("qps-", StringComparison.OrdinalIgnoreCase);
    }

    private static CultureInfo? FindCulture(string tag)
    {
        try
        {
            return CultureInfo.GetCultureInfo(tag);
        }
        catch (CultureNotFoundException)
        {
            return null;
        }
    }

    [LibraryImport("bcp47mrm.dll", StringMarshalling = StringMarshalling.Utf16)]
    [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
    private static partial int GetDistanceOfClosestLanguageInList(string language, string languageList, ushort delimiter, out double score);
}
