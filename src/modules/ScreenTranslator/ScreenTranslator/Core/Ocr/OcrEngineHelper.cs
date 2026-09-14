// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ScreenTranslator.Core.Translation;
using Windows.Graphics.Imaging;

namespace ScreenTranslator.Core.Ocr;

/// <summary>
/// Static helper exposing unified OCR extraction with automatic capability selection.
/// </summary>
public static class OcrEngineHelper
{
    private static readonly OcrBackendSelector Selector = new();

    public static async Task<IReadOnlyList<TranslationLine>> ExtractLinesWithGeometryAsync(
        SoftwareBitmap bitmap,
        PhysicalRect capturedRegionPhysical,
        string? sourceLanguageTag = null,
        CancellationToken cancellationToken = default)
    {
        IOcrBackend backend = await Selector.GetOrSelectBackendAsync();
        return await backend.RecognizeTextAsync(bitmap, capturedRegionPhysical, sourceLanguageTag, cancellationToken);
    }
}
