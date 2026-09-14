// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ScreenTranslator.Core.Translation;
using Windows.Graphics.Imaging;

namespace ScreenTranslator.Core.Ocr;

/// <summary>
/// Abstraction for OCR backends.
/// </summary>
public interface IOcrBackend
{
    string BackendName { get; }

    bool IsAvailable { get; }

    Task<IReadOnlyList<TranslationLine>> RecognizeTextAsync(
        SoftwareBitmap bitmap,
        PhysicalRect capturedRegionPhysical,
        string? sourceLanguageTag = null,
        CancellationToken cancellationToken = default);
}
