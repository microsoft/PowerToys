// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Reflection;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Services;
using Microsoft.Windows.Globalization;
using Windows.System.UserProfile;

namespace Microsoft.CmdPal.UI.Services;

internal sealed class LanguageService : ILanguageService
{
    private static readonly string[] FallbackLanguageTags =
    [
        "ar-SA", "cs-CZ", "de-DE", "en-US", "es-ES", "fa-IR", "fr-FR",
        "he-IL", "hu-HU", "it-IT", "ja-JP", "ko-KR", "nl-NL", "pl-PL",
        "pt-BR", "pt-PT", "ru-RU", "sv-SE", "tr-TR", "uk-UA", "zh-CN",
        "zh-TW",
    ];

    private static readonly string[] PseudoLocalizationCultureTags = ["qps-PLOC"];

    public string SystemLanguageTag { get; }

    public IReadOnlyList<string> AvailableLanguages { get; }

    public string CurrentLanguageTag
    {
        get => string.IsNullOrEmpty(ApplicationLanguages.PrimaryLanguageOverride)
            ? SystemLanguageTag
            : ApplicationLanguages.PrimaryLanguageOverride;
    }

    public LanguageService()
    {
        SystemLanguageTag = ResolveSystemLanguage(GlobalizationPreferences.Languages);

        AvailableLanguages = BuildLanguageList();
    }

    private List<string> BuildLanguageList()
    {
        List<string> languages = [];

        // Try loading from manifest, null in unpackaged apps
        var manifestLanguages = ApplicationLanguages.ManifestLanguages;
        if (manifestLanguages is not null)
        {
            languages = [.. manifestLanguages];
        }

        // Fallback 1: find satelitte assemblies
        if (languages.Count == 0)
        {
            var assembly = Assembly.GetEntryAssembly()!;
            if (assembly is not null)
            {
                var satelitteLanguages = GetSatelliteCultures(assembly);
                languages ??= [];
                foreach (var satelitteLanguage in satelitteLanguages)
                {
                    languages.Add(satelitteLanguage.Name);
                }
            }
        }

        // Fallback 2: hard-coded list
        if (languages.Count == 0)
        {
            languages = [.. FallbackLanguageTags];
        }

        // Include pseudo-localization language in dev builds
        if (!BuildInfo.IsCiBuild)
        {
            foreach (var languageTag in PseudoLocalizationCultureTags)
            {
                if (!languages.Contains(languageTag))
                {
                    languages.Add(languageTag);
                }
            }
        }

        return languages;
    }

    public string ApplyLanguageOverride(string languageTag)
    {
        if (AvailableLanguages.Any(tag => tag.Equals(languageTag, StringComparison.OrdinalIgnoreCase)))
        {
            // For Resw
            ApplicationLanguages.PrimaryLanguageOverride = languageTag;

            // For Resx
            var culture = new CultureInfo(languageTag);
            CultureInfo.CurrentUICulture = culture;
            CultureInfo.DefaultThreadCurrentUICulture = culture;
        }

        return ApplicationLanguages.PrimaryLanguageOverride;
    }

    public string GetEffectiveLanguageTag(string languageTag)
    {
        return string.IsNullOrEmpty(languageTag) ? SystemLanguageTag : languageTag;
    }

    // GlobalizationPreferences.Languages can rank pseudo-localization tags
    // (e.g. qps-PLOC) above the real display language on dev machines that
    // have pseudo-loc satellite assemblies installed. Skip those so the UI
    // defaults to the user's actual language.
    private static string ResolveSystemLanguage(IReadOnlyList<string> languages)
    {
        if (languages.Count == 0)
        {
            return string.Empty;
        }

        foreach (var tag in languages)
        {
            if (!tag.StartsWith("qps-", StringComparison.OrdinalIgnoreCase))
            {
                return tag;
            }
        }

        // Every entry is a pseudo-loc tag; fall back to the first one.
        return languages[0];
    }

    private static CultureInfo[] GetSatelliteCultures(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);

        var baseDir = AppContext.BaseDirectory;
        if (string.IsNullOrEmpty(baseDir) || !Directory.Exists(baseDir))
        {
            return [];
        }

        var asmName = assembly.GetName().Name;
        if (string.IsNullOrEmpty(asmName))
        {
            return [];
        }

        var satelliteFileName = asmName + ".resources.dll";

        // Use a dictionary to dedupe by culture name (case-insensitive).
        var culturesByName = new Dictionary<string, CultureInfo>(StringComparer.OrdinalIgnoreCase);

        IEnumerable<string> files;
        try
        {
            files = Directory.EnumerateFiles(baseDir, satelliteFileName, SearchOption.AllDirectories);
        }
        catch (IOException)
        {
            return [];
        }
        catch (UnauthorizedAccessException)
        {
            return [];
        }

        foreach (var filePath in files)
        {
            if (string.IsNullOrEmpty(filePath))
            {
                continue;
            }

            string? dirPath;
            try
            {
                dirPath = Path.GetDirectoryName(filePath);
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrEmpty(dirPath))
            {
                continue;
            }

            string cultureDirName;
            try
            {
                cultureDirName = new DirectoryInfo(dirPath).Name;
            }
            catch
            {
                continue;
            }

            if (string.IsNullOrEmpty(cultureDirName))
            {
                continue;
            }

            CultureInfo culture;
            try
            {
                culture = CultureInfo.GetCultureInfo(cultureDirName);
            }
            catch (CultureNotFoundException)
            {
                continue;
            }

            // Dedupe.
            culturesByName.TryAdd(culture.Name, culture);
        }

        // Sort by culture name.
        var list = new List<CultureInfo>(culturesByName.Values);
        list.Sort(static (a, b) => StringComparer.OrdinalIgnoreCase.Compare(a.Name, b.Name));

        return [.. list];
    }
}
