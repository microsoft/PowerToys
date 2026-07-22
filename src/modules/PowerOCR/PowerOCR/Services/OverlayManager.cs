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
    private OverlaySession? _session;
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
        if (_session is not null)
        {
            return;
        }

        // Create shared view model for this session
        var viewModel = _serviceProvider.GetRequiredService<OverlaySessionViewModel>();
        var session = new OverlaySession(viewModel);
        _session = session;
        PopulateLanguages(viewModel);

        var displays = DisplayArea.FindAll();
        if (displays.Count == 0)
        {
            Logger.LogWarning("No displays found for capture.");
            CloseSession(session, cancelled: false);
            return;
        }

        var captures = new List<DisplayCapture>();
        bool captureFailed = false;

        try
        {
            // DisplayArea.FindAll returns a WinRT IVectorView. Its IEnumerable projection can
            // throw InvalidCastException in AOT builds, so iterate through the indexed API.
            for (int index = 0; index < displays.Count; index++)
            {
                var display = displays[index];
                session.Token.ThrowIfCancellationRequested();
                var capture = await _screenCaptureService.CaptureAsync(display, session.Token);
                captures.Add(capture);
            }
        }
        catch (OperationCanceledException)
        {
            foreach (var cap in captures)
            {
                cap.Dispose();
            }

            CloseSession(session, cancelled: false);
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
        if (!IsCurrentSession(session))
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
                if (!IsCurrentSession(session))
                {
                    errorCapture.Dispose();
                    return;
                }

                captures.Add(errorCapture);
                session.Captures.AddRange(captures);

                var errorWindow = _windowFactory.Create(errorCapture, viewModel, this);
                session.Windows.Add(errorWindow);
                ActivateAndPositionWindow(session, errorWindow, bringToForeground: true);

                string errorMessage = ResourceLoaderInstance.ResourceLoader.GetString("ScreenCaptureFailed");
                errorWindow.ShowError(errorMessage);
            }
            catch (Exception ex2)
            {
                Logger.LogError("Failed to show error overlay", ex2);
                CloseSession(session, cancelled: false);
            }

            return;
        }

        // All captures succeeded – create overlay windows
        session.Captures.AddRange(captures);

        try
        {
            foreach (var capture in captures)
            {
                var window = _windowFactory.Create(capture, viewModel, this);
                session.Windows.Add(window);
            }

            // Activate every overlay, then explicitly give the last one foreground focus.
            // Window.Activate alone cannot reliably cross the foreground-process boundary
            // when this session was started by the Runner's named event.
            for (int index = 0; index < session.Windows.Count; index++)
            {
                ActivateAndPositionWindow(
                    session,
                    session.Windows[index],
                    bringToForeground: index == session.Windows.Count - 1);
            }

            if (IsCurrentSession(session))
            {
                PowerToysTelemetry.Log.WriteEvent(new PowerOCRInvokedEvent());
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to create or activate overlay windows", ex);
            CloseSession(session, cancelled: false);
        }
    }

    public async Task CaptureAsync(DisplayCapture capture, PixelSelection selection, bool isClick)
    {
        var session = _session;
        if (session is null || !session.Captures.Contains(capture))
        {
            return;
        }

        var viewModel = session.ViewModel;

        // Reject a second capture while processing
        if (viewModel.IsProcessing)
        {
            return;
        }

        // Reject if no language is available
        var selectedLanguage = viewModel.SelectedLanguage;
        if (selectedLanguage is null)
        {
            string noLangMsg = ResourceLoaderInstance.ResourceLoader.GetString("NoOcrLanguages");
            ShowErrorOnAll(session, noLangMsg);
            return;
        }

        viewModel.IsProcessing = true;
        viewModel.HasError = false;

        try
        {
            OcrCaptureMode mode;
            OcrPoint? clickPoint = null;

            if (isClick)
            {
                // Clicked-word mode: use full cached bitmap with local click point
                mode = OcrCaptureMode.Word;
                clickPoint = new OcrPoint(selection.Local.X, selection.Local.Y);
            }
            else if (viewModel.IsTable)
            {
                mode = OcrCaptureMode.Table;
            }
            else if (viewModel.IsSingleLine)
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
                    selectedLanguage,
                    mode,
                    clickPoint);

                result = await _textExtractorService.ExtractAsync(request, session.Token);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                if (IsCurrentSession(session))
                {
                    Logger.LogError("OCR extraction failed", ex);
                    ShowErrorOnAll(session, ResourceLoaderInstance.ResourceLoader.GetString("OcrFailed"));
                }

                return;
            }

            if (!IsCurrentSession(session))
            {
                return;
            }

            // Keep overlays open for whitespace/empty output
            if (string.IsNullOrWhiteSpace(result))
            {
                string noTextMsg = ResourceLoaderInstance.ResourceLoader.GetString("NoTextFound");
                ShowErrorOnAll(session, noTextMsg);
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
                if (IsCurrentSession(session))
                {
                    Logger.LogError("Clipboard write failed", ex);
                    ShowErrorOnAll(session, ResourceLoaderInstance.ResourceLoader.GetString("ClipboardFailed"));
                }

                return;
            }

            if (!IsCurrentSession(session))
            {
                return;
            }

            // Emit telemetry and close
            PowerToysTelemetry.Log.WriteEvent(new PowerOCRCaptureEvent());
            CloseSession(session, cancelled: false);
        }
        catch (OperationCanceledException)
        {
            // Session was cancelled - do nothing
        }
        catch (Exception ex)
        {
            // Defensive net: OCR and clipboard failures are handled at their own boundaries
            // above; this only guards the async void caller against unexpected errors.
            if (IsCurrentSession(session))
            {
                Logger.LogError("Capture pipeline failed unexpectedly", ex);
            }
        }
        finally
        {
            if (IsCurrentSession(session))
            {
                viewModel.IsProcessing = false;
            }
        }
    }

    public void CloseAll(bool cancelled)
    {
        var session = _session;
        if (session is null)
        {
            return;
        }

        CloseSession(session, cancelled);
    }

    private void CloseSession(OverlaySession session, bool cancelled)
    {
        if (!ReferenceEquals(_session, session))
        {
            return;
        }

        // Unconditionally unclip the cursor — covers Escape, settings, successful capture,
        // native termination, and external window close paths.
        CursorClipper.UnClip();

        session.CancellationSource.Cancel();

        // The hidden lifetime host is only safe to create after WinUI has successfully
        // activated at least one overlay. Keep that overlay alive until the host exists.
        if (session.HasActivatedWindow)
        {
            App.Current.EnsureLifetimeWindow();
        }

        foreach (var window in session.Windows)
        {
            window.CloseFromManager();
        }

        session.Windows.Clear();

        foreach (var capture in session.Captures)
        {
            capture.Dispose();
        }

        session.Captures.Clear();

        session.CancellationSource.Dispose();
        _session = null;

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

        if (_session is not null)
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

            if (inputLang is null)
            {
                var inputLanguage = new Language(inputTag);
                inputLang = viewModel.Languages.FirstOrDefault(
                    l => l.AbbreviatedName.Equals(inputLanguage.AbbreviatedName, StringComparison.OrdinalIgnoreCase));
            }

            if (inputLang is not null)
            {
                viewModel.SelectedLanguage = inputLang;
                return;
            }
        }

        // Final fallback: first installed language
        viewModel.SelectedLanguage = viewModel.Languages[0];
    }

    private static void ActivateAndPositionWindow(
        OverlaySession session,
        OCROverlay window,
        bool bringToForeground)
    {
        window.Activate();
        session.HasActivatedWindow = true;
        window.PositionOnDisplay();

        if (bringToForeground)
        {
            WindowHelpers.BringToForeground(WinRT.Interop.WindowNative.GetWindowHandle(window));
        }
    }

    private bool IsCurrentSession(OverlaySession session)
    {
        return ReferenceEquals(_session, session) && !session.Token.IsCancellationRequested;
    }

    private void ShowErrorOnAll(OverlaySession session, string message)
    {
        if (!IsCurrentSession(session))
        {
            return;
        }

        foreach (var window in session.Windows)
        {
            window.ShowError(message);
        }
    }

    private async void OnActivationRequested(object? sender, EventArgs e)
    {
        if (_session is not null)
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

    private sealed class OverlaySession
    {
        public OverlaySession(OverlaySessionViewModel viewModel)
        {
            ViewModel = viewModel;
            Token = CancellationSource.Token;
        }

        public CancellationTokenSource CancellationSource { get; } = new();

        public CancellationToken Token { get; }

        public OverlaySessionViewModel ViewModel { get; }

        public List<OCROverlay> Windows { get; } = new();

        public List<DisplayCapture> Captures { get; } = new();

        public bool HasActivatedWindow { get; set; }
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
