// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace PowerAccent.Common;

/// <summary>
/// Describes which category a language belongs to in the Quick Accent settings UI.
/// </summary>
public enum LanguageGroup
{
    /// <summary>Standard spoken languages.</summary>
    Language,

    /// <summary>Special character sets (e.g. currencies, IPA, romanization).</summary>
    Special,

    /// <summary>User-defined custom character sets.</summary>
    UserDefined,
}
