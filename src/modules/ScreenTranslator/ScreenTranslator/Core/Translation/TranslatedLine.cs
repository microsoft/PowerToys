// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace ScreenTranslator.Core.Translation;

/// <summary>
/// Represents a translated text line and its corresponding on-screen bounding geometry.
/// </summary>
public sealed record TranslatedLine(
    string OriginalText,
    string TranslatedText,
    PhysicalRect BoundingBox,
    double Confidence = 1.0,
    IReadOnlyList<PhysicalPoint>? PolygonVertices = null,
    int SourceLineCount = 1)
{
    public uint? OverlayBackgroundColorArgb { get; init; }

    public uint? OverlayForegroundColorArgb { get; init; }
}
