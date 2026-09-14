// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.Graphics.Imaging;
using Microsoft.Windows.AI;
using Microsoft.Windows.AI.Imaging;
using ScreenTranslator.Core.Translation;
using Windows.Graphics.Imaging;

namespace ScreenTranslator.Core.Ocr;

/// <summary>
/// NPU/WCR accelerated OCR backend utilizing Microsoft.Windows.AI.Imaging.TextRecognizer.
/// Returns fine-grained polygon bounding boxes and normalized confidence metrics.
/// </summary>
public sealed class WindowsAiTextRecognizerOcrBackend : IOcrBackend, IDisposable
{
    private readonly TextRecognizer _textRecognizer;
    private bool _disposed;

    public string BackendName => "Windows.AI.TextRecognizer";

    public bool IsAvailable => !_disposed;

    public WindowsAiTextRecognizerOcrBackend(TextRecognizer textRecognizer)
    {
        ArgumentNullException.ThrowIfNull(textRecognizer);
        _textRecognizer = textRecognizer;
    }

    /// <summary>
    /// Attempts to create an instance of the Windows AI Text Recognizer backend if supported and ready.
    /// </summary>
    public static async Task<WindowsAiTextRecognizerOcrBackend?> TryCreateAsync()
    {
        try
        {
            var readyState = TextRecognizer.GetReadyState();
            if (readyState != AIFeatureReadyState.Ready)
            {
                Logger.LogInfo($"Windows AI TextRecognizer not ready (state={readyState}).");
                return null;
            }

            var operation = await TextRecognizer.CreateAsync();
            if (operation == null)
            {
                Logger.LogWarning("TextRecognizer.CreateAsync returned null.");
                return null;
            }

            return new WindowsAiTextRecognizerOcrBackend(operation);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Exception creating WindowsAiTextRecognizerOcrBackend: {ex.Message}");
            return null;
        }
    }

    public async Task<IReadOnlyList<TranslationLine>> RecognizeTextAsync(
        SoftwareBitmap bitmap,
        PhysicalRect capturedRegionPhysical,
        string? sourceLanguageTag = null,
        CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();

        if (bitmap == null)
        {
            return Array.Empty<TranslationLine>();
        }

        cancellationToken.ThrowIfCancellationRequested();

        return await Task.Run(
            () =>
            {
                cancellationToken.ThrowIfCancellationRequested();

                bool convertedLocally = false;
                SoftwareBitmap convertedBitmap;
                if (bitmap.BitmapPixelFormat == BitmapPixelFormat.Bgra8 && bitmap.BitmapAlphaMode == BitmapAlphaMode.Premultiplied)
                {
                    convertedBitmap = bitmap;
                }
                else
                {
                    convertedBitmap = SoftwareBitmap.Convert(bitmap, BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);
                    convertedLocally = true;
                }

                try
                {
                    uint capacity = (uint)(convertedBitmap.PixelWidth * convertedBitmap.PixelHeight * 4);
                    var buffer = new Windows.Storage.Streams.Buffer(capacity);
                    convertedBitmap.CopyToBuffer(buffer);

                    using var imageBuffer = ImageBuffer.CreateForBuffer(
                        buffer,
                        ImageBufferPixelFormat.Bgra8,
                        convertedBitmap.PixelWidth,
                        convertedBitmap.PixelHeight,
                        convertedBitmap.PixelWidth * 4);
                    cancellationToken.ThrowIfCancellationRequested();

                    RecognizedText? result = _textRecognizer.RecognizeTextFromImage(imageBuffer);
                    cancellationToken.ThrowIfCancellationRequested();

                    if (result?.Lines == null || result.Lines.Length == 0)
                    {
                        return (IReadOnlyList<TranslationLine>)Array.Empty<TranslationLine>();
                    }

                    List<TranslationLine> lines = new();

                    foreach (var line in result.Lines)
                    {
                        if (line == null || string.IsNullOrWhiteSpace(line.Text))
                        {
                            continue;
                        }

                        PhysicalPoint topLeft = new(capturedRegionPhysical.X + line.BoundingBox.TopLeft.X, capturedRegionPhysical.Y + line.BoundingBox.TopLeft.Y);
                        PhysicalPoint topRight = new(capturedRegionPhysical.X + line.BoundingBox.TopRight.X, capturedRegionPhysical.Y + line.BoundingBox.TopRight.Y);
                        PhysicalPoint bottomRight = new(capturedRegionPhysical.X + line.BoundingBox.BottomRight.X, capturedRegionPhysical.Y + line.BoundingBox.BottomRight.Y);
                        PhysicalPoint bottomLeft = new(capturedRegionPhysical.X + line.BoundingBox.BottomLeft.X, capturedRegionPhysical.Y + line.BoundingBox.BottomLeft.Y);

                        double minX = Math.Min(Math.Min(topLeft.X, topRight.X), Math.Min(bottomLeft.X, bottomRight.X));
                        double maxX = Math.Max(Math.Max(topLeft.X, topRight.X), Math.Max(bottomLeft.X, bottomRight.X));
                        double minY = Math.Min(Math.Min(topLeft.Y, topRight.Y), Math.Min(bottomLeft.Y, bottomRight.Y));
                        double maxY = Math.Max(Math.Max(topLeft.Y, topRight.Y), Math.Max(bottomLeft.Y, bottomRight.Y));

                        PhysicalRect lineRect = new(minX, minY, Math.Max(1.0, maxX - minX), Math.Max(1.0, maxY - minY));
                        List<PhysicalPoint> polygonVertices = new() { topLeft, topRight, bottomRight, bottomLeft };

                        double confidence = CalculateConfidence(line);

                        lines.Add(new TranslationLine(
                            line.Text.Trim(),
                            lineRect,
                            confidence,
                            polygonVertices));
                    }

                    return lines;
                }
                finally
                {
                    if (convertedLocally)
                    {
                        convertedBitmap.Dispose();
                    }
                }
            },
            cancellationToken);
    }

    private static double CalculateConfidence(RecognizedLine line)
    {
        if (line.Words != null && line.Words.Length > 0)
        {
            double sum = 0.0;
            int count = 0;
            foreach (var word in line.Words)
            {
                if (word != null)
                {
                    sum += Math.Clamp(word.MatchConfidence, 0.0f, 1.0f);
                    count++;
                }
            }

            if (count > 0)
            {
                return sum / count;
            }
        }

        return Math.Clamp(line.LineStyleConfidence, 0.0f, 1.0f);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, typeof(WindowsAiTextRecognizerOcrBackend));
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _textRecognizer?.Dispose();
            _disposed = true;
        }
    }
}
