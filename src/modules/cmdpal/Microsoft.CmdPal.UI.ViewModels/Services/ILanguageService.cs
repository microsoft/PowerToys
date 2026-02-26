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
    /// Gets the IETF BCP 47 language tag for current system language.
    /// </summary>
    string SystemLanguageTag { get; }

    /// <summary>
    ///Gets the IETF BCP 47 language tag for current application language.
    /// </summary>
    string CurrentLanguageTag { get; }

    /// <summary>
    /// Gets the list of IETF BCP 47 language tags for all available languages.
    /// </summary>
    IReadOnlyList<string> AvailableLanguages { get; }

    /// <summary>
    /// Overrides the app language to the language <paramref name="languageTag"/>.
    /// </summary>
    /// <param name="languageTag">IETF BCP 47 language tag of the requested language.</param>
    /// <returns>The IETF BCP 47 language tag of the actual language set.</returns>
    string ApplyLanguageOverride(string languageTag);

    /// <summary>
    /// Returns the most appropriate language tag to use based on the specified input language tag.
    /// </summary>
    /// <param name="languageTag">
    /// The input language tag to evaluate. This value should be a valid IETF BCP 47 language tag. Cannot be null.
    /// </param>
    /// <returns>
    /// A string containing the effective language tag determined from the input. The returned value may differ from the
    /// input if a fallback or normalization is applied.
    /// </returns>
    string GetEffectiveLanguageTag(string languageTag);
}
