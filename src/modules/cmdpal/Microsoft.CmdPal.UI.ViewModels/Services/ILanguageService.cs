// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.Services;

/// <summary>
///  Service encapsulating display language state and manipulation.
/// </summary>
public interface ILanguageService
{
    /// <summary>
    /// Gets the IETF BCP 47 tag of the supported app language selected from Windows preferences.
    /// </summary>
    string SystemLanguageTag { get; }

    /// <summary>
    /// Gets the IETF BCP 47 tag of the UI culture applied to the running process.
    /// </summary>
    string CurrentLanguageTag { get; }

    /// <summary>
    /// Gets the list of IETF BCP 47 language tags for all available languages.
    /// </summary>
    IReadOnlyList<string> AvailableLanguages { get; }

    /// <summary>
    /// Gets whether this development build offers pseudolocalization without its resources in the manifest.
    /// </summary>
    bool IsPseudoLocalizationMissing { get; }

    /// <summary>
    /// Overrides the app language to the language <paramref name="languageTag"/>.
    /// </summary>
    /// <param name="languageTag">IETF BCP 47 language tag, or an empty string to use <see cref="SystemLanguageTag"/>.</param>
    /// <returns>The accepted tag, or an empty string when the input is empty, unavailable, or could not be applied.</returns>
    string ApplyLanguageOverride(string languageTag);

    /// <summary>
    /// Returns the most appropriate language tag to use based on the specified input language tag.
    /// </summary>
    /// <param name="languageTag">
    /// An IETF BCP 47 language tag, such as "en-US" or "cs-CZ", or an empty string to use <see cref="SystemLanguageTag"/>.
    /// </param>
    /// <returns>
    /// The IETF BCP 47 tag after Windows language mapping, or en-US if mapping fails. This does not validate language availability.
    /// </returns>
    string GetEffectiveLanguageTag(string languageTag);
}
