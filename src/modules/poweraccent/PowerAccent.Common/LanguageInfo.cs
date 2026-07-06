// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerAccent.Common;

/// <summary>
/// Describes a single language entry: its enum identity, the resource key identifier
/// used to look up its localized display name, which group it belongs to, and its
/// character mappings.
/// </summary>
/// <param name="Id">The <see cref="Language"/> enum value for this entry.</param>
/// <param name="Identifier">
/// The stable string identifier used to construct the settings resource key
/// (e.g. <c>"Bulgarian"</c> -> <c>QuickAccent_SelectedLanguage_Bulgarian</c>).
/// </param>
/// <param name="Group">Which <see cref="LanguageGroup"/> category this entry belongs to.
/// </param>
/// <param name="Characters">The character mappings for this language.</param>
public sealed record LanguageInfo(
    Language Id,
    string Identifier,
    LanguageGroup Group,
    IReadOnlyDictionary<LetterKey, string[]> Characters);
