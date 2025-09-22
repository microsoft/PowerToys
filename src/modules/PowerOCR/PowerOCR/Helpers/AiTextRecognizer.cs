// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// Stub AI text recognizer backend. This will later integrate Windows AI Foundry Text Recognition APIs.
// For now it delegates to legacy OcrEngine to keep behavior identical while wiring selection logic.
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

// Removed ManagedCommon dependency for now (logger not yet referenced by project)
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace PowerOCR.Helpers
{
    internal sealed class AiTextRecognizer : ITextRecognizerBackend
    {
        private static readonly Lazy<AiTextRecognizer> _instance = new(() => new AiTextRecognizer());

        public static AiTextRecognizer Instance => _instance.Value;

        private bool _initialized;
        private bool _usable;

        public string Name => "AI";

        public bool IsUsable => _usable; // Will reflect model load success later

        private AiTextRecognizer()
        {
        }

        private Task EnsureInitializedAsync(CancellationToken ct)
        {
            if (_initialized)
            {
                return Task.CompletedTask;
            }

            try
            {
                var osVersion = Environment.OSVersion.Version;
                _usable = osVersion.Major >= 10; // placeholder capability check
            }
            catch (Exception ex)
            {
                System.Diagnostics.Trace.TraceError($"AI Text Recognizer initialization failed: {ex.Message}");
                _usable = false; // leave unusable for remainder of process lifetime
            }
            finally
            {
                _initialized = true;
            }

            return Task.CompletedTask;
        }

        public async Task<string> RecognizeAsync(Bitmap bitmap, Language language, bool singleLine, CancellationToken ct)
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            if (!_usable)
            {
                return string.Empty;
            }

            // TEMP IMPLEMENTATION: Use legacy OCR until AI integration added.
            // Convert bitmap to SoftwareBitmap then run OcrEngine like legacy path.
            using var memStream = new MemoryStream();
            bitmap.Save(memStream, System.Drawing.Imaging.ImageFormat.Bmp);
            memStream.Position = 0;
            var ras = memStream.AsRandomAccessStream();
            var decoder = await BitmapDecoder.CreateAsync(ras);
            var softwareBmp = await decoder.GetSoftwareBitmapAsync();
            var ocr = OcrEngine.TryCreateFromLanguage(language);
            var result = await ocr.RecognizeAsync(softwareBmp).AsTask(ct);

            var isSpaceJoining = LanguageHelper.IsLanguageSpaceJoining(language);
            System.Text.StringBuilder sb = new();
            foreach (var line in result.Lines)
            {
                line.GetTextFromOcrLine(isSpaceJoining, sb);
            }

            var text = sb.ToString();
            if (singleLine)
            {
                text = text.MakeStringSingleLine();
            }

            return text.Trim();
        }
    }
}
