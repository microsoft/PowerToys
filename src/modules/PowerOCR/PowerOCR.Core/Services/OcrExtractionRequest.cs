// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using PowerOCR.Core.Models;
using Windows.Globalization;

namespace PowerOCR.Core.Services;

public sealed record OcrExtractionRequest(
    Bitmap Bitmap,
    Language Language,
    OcrCaptureMode Mode,
    OcrPoint? ClickPoint = null);
