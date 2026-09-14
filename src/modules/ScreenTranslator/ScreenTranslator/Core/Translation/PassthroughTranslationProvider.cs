// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenTranslator.Core.Translation;

/// <summary>
/// Explicitly labeled offline prototype translation provider.
/// Performs identity / prefixed translation without calling external services or requiring credentials.
/// </summary>
public sealed class PassthroughTranslationProvider : ITranslationProvider
{
    private readonly string? _prefix;

    public string ProviderId => "passthrough-prototype";

    public string DisplayName => "Passthrough (Offline Prototype)";

    public PassthroughTranslationProvider(string? prefix = null)
    {
        _prefix = prefix;
    }

    public Task<TranslationResult> TranslateAsync(
        TranslationRequest request,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        ArgumentNullException.ThrowIfNull(request);

        List<TranslatedLine> translatedLines = new();

        foreach (var line in request.Lines)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string translatedText;
            if (_prefix != null)
            {
                translatedText = $"{_prefix}{line.Text}";
            }
            else if (!string.IsNullOrWhiteSpace(request.TargetLanguage))
            {
                translatedText = $"[{request.TargetLanguage}] {line.Text}";
            }
            else
            {
                translatedText = line.Text;
            }

            translatedLines.Add(new TranslatedLine(
                OriginalText: line.Text,
                TranslatedText: translatedText,
                BoundingBox: line.BoundingBox,
                Confidence: line.Confidence,
                PolygonVertices: line.PolygonVertices,
                SourceLineCount: line.SourceLineCount));
        }

        return Task.FromResult(new TranslationResult(
            translatedLines,
            Success: true,
            ErrorMessage: null,
            SourceLanguage: request.SourceLanguage,
            TargetLanguage: request.TargetLanguage));
    }
}
