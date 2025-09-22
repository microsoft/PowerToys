// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Abstraction for text recognition backends (legacy OCR vs AI model)
// This minimal interface supports asynchronous recognition from a bitmap capture.
// Future: extend with layout/blocks or table extraction if AI backend provides richer structure.
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using Windows.Globalization;

namespace PowerOCR.Helpers
{
    internal interface ITextRecognizerBackend
    {
        string Name { get; }

        bool IsUsable { get; }

        Task<string> RecognizeAsync(Bitmap bitmap, Language language, bool singleLine, CancellationToken ct);
    }
}
