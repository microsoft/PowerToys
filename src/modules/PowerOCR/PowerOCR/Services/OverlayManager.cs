// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Imaging;
using PowerOCR.Core.Models;
using PowerOCR.Core.Services;
using PowerOCR.Helpers;
using PowerOCR.Models;
using PowerOCR.Settings;
using PowerOCR.Telemetry;
using PowerOCR.ViewModels;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;
using Windows.Storage.Streams;

namespace PowerOCR.Services;

internal sealed class OverlayManager : IOverlayManager
{
    private readonly IActivationService _activationService;
    private readonly IScreenCaptureService _screenCaptureService;
    private readonly IOverlayWindowFactory _windowFactory;
    private readonly ITextExtractorService _textExtractorService;
    private readonly IClipboardService _clipboardService;
    private readonly IUserSettings _userSettings;
    private readonly IServiceProvider _serviceProvider;
    private readonly List<OCROverlay> _activeWindows = new();
    private readonly List<DisplayCapture> _activeCaptures = new();
    private CancellationTokenSource? _sessionCts;
    private OverlaySessionViewModel? _viewModel;
    private bool _sessionActive;
    private bool _disposed;

    public OverlayManager(
        IActivationService activationService,
        IScreenCaptureService screenCaptureService,
        IOverlayWindowFactory windowFactory,
        ITextExtractorService textExtractorService,
        IClipboardService clipboardService,
        IUserSettings userSettings,
        IServiceProvider serviceProvider)
    {
        _activationService = activationService;
        _screenCaptureService = screenCaptureService;
        _windowFactory = windowFactory;
        _textExtractorService = textExtractorService;
        _clipboardService = clipboardService;
        _userSettings = userSettings;
        _serviceProvider = serviceProvider;

        _activationService.ActivationRequested += OnActivationRequested;
    }

    public async Task ShowAsync()
    {
        if (_sessionActive)
        {
            return;
        }

        _sessionActive = true;
        _sessionCts = new CancellationTokenSource();
        var token = _sessionCts.Token;

        // Create shared view model for this session
        _viewModel = _serviceProvider.GetRequiredService<OverlaySessionViewModel>();
        PopulateLanguages(_viewModel);

        var displays = DisplayArea.FindAll();
        if (displays.Count == 0)
        {
            Logger.LogWarning("No displays found for capture.");
            CloseAll(cancelled: false);
            return;
        }

        var captures = new List<DisplayCapture>();
        bool captureFailed = false;

        try
        {
            foreach (var display in displays)
            {
                token.ThrowIfCancellationRequested();
                var capture = await _screenCaptureService.CaptureAsync(display, token);
                captures.Add(capture);
            }
        }
        catch (OperationCanceledException)
        {
            foreach (var cap in captures)
            {
                cap.Dispose();
            }

            CloseAll(cancelled: false);
            return;
        }
        catch (Exception ex)
        {
            Logger.LogError("Screen capture failed", ex);
            captureFailed = true;
        }

        // A terminate/disable request can run on the dispatcher while the awaited captures
        // above complete. If the session was torn down, release every locally captured image
        // and abort before resolving or creating any overlay windows.
        if (!_sessionActive || token.IsCancellationRequested)
        {
            foreach (var cap in captures)
            {
                cap.Dispose();
            }

            return;
        }

        if (captureFailed)
        {
            // Dispose any successful captures
            foreach (var cap in captures)
            {
                cap.Dispose();
            }

            captures.Clear();

            // Show error-only overlay on the first display
            try
            {
                var errorCapture = await CreateErrorCaptureAsync(displays[0]);
                captures.Add(errorCapture);
                _activeCaptures.AddRange(captures);

                var errorWindow = _windowFactory.Create(errorCapture, _viewModel, this);
                _activeWindows.Add(errorWindow);
                errorWindow.Activate();

                string errorMessage = ResourceLoaderInstance.ResourceLoader.GetString("ScreenCaptureFailed");
                errorWindow.ShowError(errorMessage);
            }
            catch (Exception ex2)
            {
                Logger.LogError("Failed to show error overlay", ex2);
                CloseAll(cancelled: false);
            }

            return;
        }

        // All captures succeeded – create overlay windows
        _activeCaptures.AddRange(captures);

        try
        {
            foreach (var capture in captures)
            {
                var window = _windowFactory.Create(capture, _viewModel, this);
                _activeWindows.Add(window);
            }

            // Activate all windows; last one gets keyboard focus
            foreach (var window in _activeWindows)
            {
                window.Activate();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to create or activate overlay windows", ex);
            CloseAll(cancelled: false);
        }
    }

    public async Task CaptureAsync(DisplayCapture capture, PixelSelection selection, bool isClick)
    {
        if (_viewModel is null || !_sessionActive)
        {
            return;
        }

        // Reject a second capture while processing
        if (_viewModel.IsProcessing)
        {
            return;
        }

        // Reject if no language is available
        if (_viewModel.SelectedLanguage is null)
        {
            string noLangMsg = ResourceLoaderInstance.ResourceLoader.GetString("NoOcrLanguages");
            ShowErrorOnAll(noLangMsg);
            return;
        }

        _viewModel.IsProcessing = true;
        _viewModel.HasError = false;

        try
        {
            var token = _sessionCts?.Token ?? CancellationToken.None;

            OcrCaptureMode mode;
            OcrPoint? clickPoint = null;

            if (isClick)
            {
                // Clicked-word mode: use full cached bitmap with local click point
                mode = OcrCaptureMode.Word;
                clickPoint = new OcrPoint(selection.Local.X, selection.Local.Y);
            }
            else if (_viewModel.IsTable)
            {
                mode = OcrCaptureMode.Table;
            }
            else if (_viewModel.IsSingleLine)
            {
                mode = OcrCaptureMode.SingleLine;
            }
            else
            {
                mode = OcrCaptureMode.Region;
            }

            // OCR boundary: any failure here (including a WinRT COMException raised by the
            // OCR engine) surfaces as OcrFailed, never ClipboardFailed.
            string result;
            try
            {
                // Word mode reuses the shared capture bitmap, while region/table/single-line
                // modes own a cropped bitmap that must be disposed once OCR completes.
                using Bitmap? croppedBitmap = isClick ? null : capture.Crop(selection.Local);
                Bitmap bitmapForOcr = isClick ? capture.Bitmap : croppedBitmap!;

                var request = new OcrExtractionRequest(
                    bitmapForOcr,
                    _viewModel.SelectedLanguage,
                    mode,
                    clickPoint);

                result = await _textExtractorService.ExtractAsync(request, token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError("OCR extraction failed", ex);
                ShowErrorOnAll(ResourceLoaderInstance.ResourceLoader.GetString("OcrFailed"));
                return;
            }

            // Keep overlays open for whitespace/empty output
            if (string.IsNullOrWhiteSpace(result))
            {
                string noTextMsg = ResourceLoaderInstance.ResourceLoader.GetString("NoTextFound");
                ShowErrorOnAll(noTextMsg);
                return;
            }

            // Clipboard boundary: failures here surface as ClipboardFailed so the extracted
            // content is not silently lost.
            try
            {
                await _clipboardService.SetTextAsync(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogError("Clipboard write failed", ex);
                ShowErrorOnAll(ResourceLoaderInstance.ResourceLoader.GetString("ClipboardFailed"));
                return;
            }

            // Emit telemetry and close
            PowerToysTelemetry.Log.WriteEvent(new PowerOCRCaptureEvent());
            CloseAll(cancelled: false);
        }
        catch (OperationCanceledException)
        {
            // Session was cancelled - do nothing
        }
        catch (Exception ex)
        {
            // Defensive net: OCR and clipboard failures are handled at their own boundaries
            // above; this only guards the async void caller against unexpected errors.
            Logger.LogError("Capture pipeline failed unexpectedly", ex);
        }
        finally
        {
            if (_sessionActive && _viewModel is not null)
            {
                _viewModel.IsProcessing = false;
            }
        }
    }

    public void CloseAll(bool cancelled)
    {
        if (!_sessionActive)
        {
            return;
        }

        // Unconditionally unclip the cursor — covers Escape, settings, successful capture,
        // native termination, and external window close paths.
        CursorClipper.UnClip();

        _sessionCts?.Cancel();
        _sessionCts?.Dispose();
        _sessionCts = null;

        foreach (var window in _activeWindows)
        {
            window.CloseFromManager();
        }

        _activeWindows.Clear();

        foreach (var capture in _activeCaptures)
        {
            capture.Dispose();
        }

        _activeCaptures.Clear();

        _viewModel = null;
        _sessionActive = false;

        if (cancelled)
        {
            PowerToysTelemetry.Log.WriteEvent(new PowerOCRCancelledEvent());
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _activationService.ActivationRequested -= OnActivationRequested;

        if (_sessionActive)
        {
            CloseAll(cancelled: false);
        }
    }

    private void PopulateLanguages(OverlaySessionViewModel viewModel)
    {
        var recognizers = OcrEngine.AvailableRecognizerLanguages;
        foreach (var lang in recognizers)
        {
            viewModel.Languages.Add(lang);
        }

        if (viewModel.Languages.Count == 0)
        {
            return;
        }

        // Try preferred language from settings
        string preferredName = _userSettings.PreferredLanguage.Value;
        if (!string.IsNullOrEmpty(preferredName))
        {
            var preferred = viewModel.Languages.FirstOrDefault(
                l => l.NativeName.Equals(preferredName, StringComparison.OrdinalIgnoreCase));
            if (preferred is not null)
            {
                viewModel.SelectedLanguage = preferred;
                return;
            }
        }

        // Fallback to current input language
        string inputTag = Language.CurrentInputMethodLanguageTag;
        if (!string.IsNullOrEmpty(inputTag))
        {
            var inputLang = viewModel.Languages.FirstOrDefault(
                l => l.LanguageTag.Equals(inputTag, StringComparison.OrdinalIgnoreCase));
            if (inputLang is not null)
            {
                viewModel.SelectedLanguage = inputLang;
                return;
            }
        }

        // Final fallback: first installed language
        viewModel.SelectedLanguage = viewModel.Languages[0];
    }

    private void ShowErrorOnAll(string message)
    {
        foreach (var window in _activeWindows)
        {
            window.ShowError(message);
        }
    }

    private async void OnActivationRequested(object? sender, EventArgs e)
    {
        if (_sessionActive)
        {
            return;
        }

        try
        {
            await ShowAsync();
        }
        catch (Exception ex)
        {
            Logger.LogError("Overlay activation failed unexpectedly", ex);
            CloseAll(cancelled: false);
        }
    }

    private static async Task<DisplayCapture> CreateErrorCaptureAsync(DisplayArea display)
    {
        var outerBounds = display.OuterBounds;
        var bounds = new DisplayBounds(outerBounds.X, outerBounds.Y, outerBounds.Width, outerBounds.Height);

        Bitmap? bitmap = null;
        SoftwareBitmapSource? source = null;
        SoftwareBitmap? softwareBitmap = null;
        DisplayCapture? capture = null;

        try
        {
            // Create a black error bitmap
            bitmap = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap))
            {
                graphics.Clear(Color.Black);
            }

            // Convert to SoftwareBitmapSource
            using var memoryStream = new MemoryStream();
            bitmap.Save(memoryStream, ImageFormat.Bmp);
            memoryStream.Position = 0;

            using var randomAccessStream = new InMemoryRandomAccessStream();
            using (var outputStream = randomAccessStream.GetOutputStreamAt(0))
            {
                await RandomAccessStream.CopyAsync(memoryStream.AsInputStream(), outputStream);
                await outputStream.FlushAsync();
            }

            var decoder = await BitmapDecoder.CreateAsync(randomAccessStream);
            softwareBitmap = await decoder.GetSoftwareBitmapAsync(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied);

            source = new SoftwareBitmapSource();
            await source.SetBitmapAsync(softwareBitmap);

            capture = new DisplayCapture(bounds, bitmap, source);
            return capture;
        }
        finally
        {
            softwareBitmap?.Dispose();

            if (capture is null)
            {
                source?.Dispose();
                bitmap?.Dispose();
            }
        }
    }
}
