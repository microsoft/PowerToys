// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace ScreenTranslator.Core.Translation;

/// <summary>
/// Represents a recognized text line and its canonical physical bounding geometry.
/// </summary>
public sealed record TranslationLine(
    string Text,
    PhysicalRect BoundingBox,
    double Confidence = 1.0,
    IReadOnlyList<PhysicalPoint>? PolygonVertices = null,
    int SourceLineCount = 1);
