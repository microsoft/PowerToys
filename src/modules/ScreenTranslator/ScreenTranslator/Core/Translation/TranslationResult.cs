// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace ScreenTranslator.Core.Translation;

public sealed record TranslationResult(
    IReadOnlyList<TranslatedLine> TranslatedLines,
    bool Success = true,
    string? ErrorMessage = null,
    string? SourceLanguage = null,
    string? TargetLanguage = null)
{
    public IReadOnlyList<TranslatedLine> Lines => TranslatedLines;
}
