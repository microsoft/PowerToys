// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;
using PowerOCR.Core.Models;
using Windows.Globalization;

namespace PowerOCR.Core.Ocr;

public interface IOcrRecognizer
{
    Task<OcrDocument> RecognizeAsync(
        Bitmap bitmap,
        Language language,
        CancellationToken cancellationToken);
}
