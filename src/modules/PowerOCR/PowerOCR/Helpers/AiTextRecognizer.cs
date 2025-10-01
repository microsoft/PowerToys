// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

// AI text recognizer backend using Windows AI Foundry Text Recognition APIs.
// Returns empty string on failure/unavailability so caller can fall back to legacy Windows.Media.Ocr.
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon; // PowerToys logger
using Microsoft.Graphics.Imaging; // ImageBuffer
using Microsoft.Windows.AI; // AIFeatureReadyState
using Microsoft.Windows.AI.Imaging; // TextRecognizer APIs
using Windows.Globalization;
using Windows.Graphics.Imaging;

namespace PowerOCR.Helpers
{
    internal sealed class AiTextRecognizer : ITextRecognizerBackend
    {
        private static readonly Lazy<AiTextRecognizer> _instance = new(() => new AiTextRecognizer());

        public static AiTextRecognizer Instance => _instance.Value;

        private bool _initialized;
        private bool _usable;
        private TextRecognizer? _session;

        public string Name => "AI";

        public bool IsUsable => _usable; // Will reflect model load success later

        private AiTextRecognizer()
        {
        }

        private async Task EnsureInitializedAsync(CancellationToken ct)
        {
            if (_initialized)
            {
                return;
            }

            try
            {
                Logger.LogDebug("Initializing AI Text Recognizer backend");

                // Guard: require package identity (sparse package) for Windows AI Foundry APIs
                bool hasIdentity;
                try
                {
                    _ = Windows.ApplicationModel.Package.Current.Id; // accessing triggers exception if no identity
                    hasIdentity = true;
                }
                catch
                {
                    hasIdentity = false;
                }

                if (!hasIdentity)
                {
                    Logger.LogWarning("AI Text Recognizer: no package identity detected; skipping AI initialization.");
                    _usable = false;
                    return; // _initialized will be set in finally
                }

                var osVersion = Environment.OSVersion.Version;
                bool osOk = osVersion.Major >= 10; // Basic gate
                if (osOk)
                {
                    var readyState = TextRecognizer.GetReadyState();
                    if (readyState == AIFeatureReadyState.NotReady)
                    {
                        Logger.LogInfo("TextRecognizer not ready. Calling EnsureReadyAsync().");
                        var op = TextRecognizer.EnsureReadyAsync();
                        using var registration = ct.Register(() => op.Cancel());
                        await op; // propagate cancellation if any
                    }

                    _session = await TextRecognizer.CreateAsync();
                    _usable = _session is not null;
                }
                else
                {
                    _usable = false;
                }

                Logger.LogInfo($"AI Text Recognizer initialized. OS={osVersion} usable={_usable}");
            }
            catch (OperationCanceledException)
            {
                Logger.LogInfo("AI Text Recognizer initialization canceled");
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError("AI Text Recognizer initialization failed", ex);
                _usable = false;
            }
            finally
            {
                _initialized = true;
            }
        }

        public async Task<string> RecognizeAsync(Bitmap bitmap, Language language, bool singleLine, CancellationToken ct)
        {
            await EnsureInitializedAsync(ct).ConfigureAwait(false);
            if (!_usable)
            {
                Logger.LogWarning("AI Text Recognizer requested while unusable. Returning empty result to trigger caller fallback.");
                return string.Empty; // caller will fallback to legacy path
            }

            if (ct.IsCancellationRequested)
            {
                Logger.LogInfo("AI Text Recognizer recognize canceled before start");
                ct.ThrowIfCancellationRequested();
            }

            if (_session is null)
            {
                Logger.LogWarning("AI Text Recognizer session null after initialization. Fallback.");
                return string.Empty;
            }

            try
            {
                var sw = System.Diagnostics.Stopwatch.StartNew();
                using var memStream = new MemoryStream();
                bitmap.Save(memStream, System.Drawing.Imaging.ImageFormat.Bmp);
                memStream.Position = 0;
                var ras = memStream.AsRandomAccessStream();
                var decoder = await BitmapDecoder.CreateAsync(ras);
                var softwareBmp = await decoder.GetSoftwareBitmapAsync();
                using var imageBuffer = ImageBuffer.CreateForSoftwareBitmap(softwareBmp);

                var recognized = _session.RecognizeTextFromImage(imageBuffer);
                sw.Stop();
                var lineCount = recognized.Lines?.Length ?? 0;
                Logger.LogDebug($"AI text recognition completed in {sw.ElapsedMilliseconds} ms lines={lineCount}");

                if (lineCount == 0)
                {
                    return string.Empty; // trigger fallback
                }

                bool isSpaceJoining = LanguageHelper.IsLanguageSpaceJoining(language);
                System.Text.StringBuilder sb = new();
                for (int i = 0; i < recognized?.Lines?.Length; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(isSpaceJoining ? ' ' : '\n');
                    }

                    sb.Append(recognized.Lines[i].Text);
                }

                var text = sb.ToString();
                if (singleLine)
                {
                    text = text.MakeStringSingleLine();
                }

                return text.Trim();
            }
            catch (OperationCanceledException)
            {
                Logger.LogInfo("AI Text recognition canceled");
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError("AI Text recognition failed", ex);
                return string.Empty; // fallback
            }
        }
    }
}
