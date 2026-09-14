// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.PowerToys.Common.UI.Controls.Window;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using ScreenTranslator.Core.Capture;
using ScreenTranslator.Core.Layout;
using ScreenTranslator.Core.Ocr;
using ScreenTranslator.Core.Translation;
using ScreenTranslator.Helpers;
using Windows.Graphics;
using Windows.Graphics.Imaging;
using WinUIEx;
using WindowManager = ScreenTranslator.Helpers.WindowManager;

namespace ScreenTranslator;

public sealed partial class SelectionOverlay : TransparentWindow
{
    private readonly ScreenInfo _screenInfo;
    private readonly ITranslationProvider _translationProvider;
    private readonly string _sourceLanguage;
    private readonly string _targetLanguage;
    private readonly PhysicalRect? _foregroundWindowBounds;
    private readonly DispatcherQueue _dispatcherQueue;

    private bool _isSelecting;
    private Windows.Foundation.Point _startPoint;

    public SelectionOverlay(
        ScreenInfo screenInfo,
        ITranslationProvider? translationProvider = null,
        string sourceLanguage = "auto",
        string targetLanguage = "en-US",
        PhysicalRect? foregroundWindowBounds = null)
    {
        _screenInfo = screenInfo;
        _translationProvider = translationProvider ?? new PassthroughTranslationProvider();
        _sourceLanguage = sourceLanguage;
        _targetLanguage = targetLanguage;
        _foregroundWindowBounds = foregroundWindowBounds;
        _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

        InitializeComponent();
        ActiveWindowButton.IsEnabled = _foregroundWindowBounds.HasValue;

        var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);

        int style = OSInterop.GetWindowLong(hwnd, OSInterop.GwlStyle);
        _ = OSInterop.SetWindowLong(hwnd, OSInterop.GwlStyle, style & ~OSInterop.WsCaption & ~OSInterop.WsThickFrame);

        int exStyle = OSInterop.GetWindowLong(hwnd, OSInterop.GwlExStyle);
        _ = OSInterop.SetWindowLong(hwnd, OSInterop.GwlExStyle, exStyle | OSInterop.WsExNoActivate | OSInterop.WsExToolWindow | OSInterop.WsExTopMost);

        AppWindow.MoveAndResize(new RectInt32(
            (int)_screenInfo.Bounds.X,
            (int)_screenInfo.Bounds.Y,
            (int)_screenInfo.Bounds.Width,
            (int)_screenInfo.Bounds.Height));
        _ = OSInterop.SetWindowPos(
            hwnd,
            OSInterop.HwndTopMost,
            (int)_screenInfo.Bounds.X,
            (int)_screenInfo.Bounds.Y,
            (int)_screenInfo.Bounds.Width,
            (int)_screenInfo.Bounds.Height,
            OSInterop.SwpNoActivate | OSInterop.SwpNoOwnerZOrder | OSInterop.SwpShowWindow);

        try
        {
            this.SetIsShownInSwitchers(false);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"SetIsShownInSwitchers failed: {ex.Message}");
        }
    }

    private void FullScreenButton_Click(object sender, RoutedEventArgs e)
    {
        WindowManager.CloseAllSelectionOverlays();
        _ = ProcessCaptureAndTranslateAsync(_screenInfo.Bounds);
    }

    private void ActiveWindowButton_Click(object sender, RoutedEventArgs e)
    {
        if (!_foregroundWindowBounds.HasValue)
        {
            return;
        }

        WindowManager.CloseAllSelectionOverlays();
        _ = ProcessCaptureAndTranslateAsync(_foregroundWindowBounds.Value);
    }

    private void SelectionCanvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        _isSelecting = true;
        _startPoint = e.GetCurrentPoint(SelectionCanvas).Position;

        Canvas.SetLeft(SelectionRectangle, _startPoint.X);
        Canvas.SetTop(SelectionRectangle, _startPoint.Y);
        SelectionRectangle.Width = 0;
        SelectionRectangle.Height = 0;
        SelectionRectangle.Visibility = Visibility.Visible;

        CursorClipper.ClipCursorToRect(_screenInfo.Bounds);
        SelectionCanvas.CapturePointer(e.Pointer);
    }

    private void SelectionCanvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        var currentPoint = e.GetCurrentPoint(SelectionCanvas).Position;

        double x = Math.Min(_startPoint.X, currentPoint.X);
        double y = Math.Min(_startPoint.Y, currentPoint.Y);
        double width = Math.Abs(currentPoint.X - _startPoint.X);
        double height = Math.Abs(currentPoint.Y - _startPoint.Y);

        Canvas.SetLeft(SelectionRectangle, x);
        Canvas.SetTop(SelectionRectangle, y);
        SelectionRectangle.Width = width;
        SelectionRectangle.Height = height;
    }

    private async void SelectionCanvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        _isSelecting = false;
        SelectionCanvas.ReleasePointerCapture(e.Pointer);
        CursorClipper.UnclipCursor();

        var endPoint = e.GetCurrentPoint(SelectionCanvas).Position;

        double leftDip = Math.Min(_startPoint.X, endPoint.X);
        double topDip = Math.Min(_startPoint.Y, endPoint.Y);
        double widthDip = Math.Abs(endPoint.X - _startPoint.X);
        double heightDip = Math.Abs(endPoint.Y - _startPoint.Y);

        WindowManager.CloseAllSelectionOverlays();

        if (widthDip < 8 || heightDip < 8)
        {
            Logger.LogInfo("Selection region too small; cancelling capture.");
            return;
        }

        PhysicalRect physicalRect = OverlayLayoutHelper.DipToPhysical(
            leftDip,
            topDip,
            widthDip,
            heightDip,
            _screenInfo.Bounds,
            _screenInfo.DpiScaleX,
            _screenInfo.DpiScaleY);

        physicalRect = OverlayLayoutHelper.ClampToScreen(physicalRect, _screenInfo.Bounds);

        await ProcessCaptureAndTranslateAsync(physicalRect);
    }

    private async Task ProcessCaptureAndTranslateAsync(
        PhysicalRect capturedRegionPhysical,
        string? sourceLanguage = null,
        string? targetLanguage = null)
    {
        SoftwareBitmap? capturedBitmap = null;
        ProcessingOverlay? processingOverlay = null;
        using CancellationTokenSource cancellationTokenSource = new();
        sourceLanguage ??= _sourceLanguage;
        targetLanguage ??= _targetLanguage;

        try
        {
            Logger.LogInfo($"Capturing region: {capturedRegionPhysical.X},{capturedRegionPhysical.Y} {capturedRegionPhysical.Width}x{capturedRegionPhysical.Height}");
            capturedBitmap = ScreenCaptureHelper.CaptureRegion(capturedRegionPhysical);

            if (capturedBitmap == null)
            {
                Logger.LogWarning("Screen capture returned null.");
                return;
            }

            processingOverlay = WindowManager.ShowProcessingOverlay(capturedRegionPhysical, cancellationTokenSource.Cancel);
            processingOverlay.UpdateStatus("Recognizing text...");

            var recognizedLines = await OcrEngineHelper.ExtractLinesWithGeometryAsync(
                capturedBitmap,
                capturedRegionPhysical,
                sourceLanguage,
                cancellationTokenSource.Token);
            Logger.LogInfo($"Recognized {recognizedLines.Count} text lines.");

            if (recognizedLines.Count == 0)
            {
                return;
            }

            IReadOnlyList<TranslationLine> groupedLines = OverlayLayoutHelper.GroupAdjacentTextLines(recognizedLines);
            Logger.LogInfo($"Grouped recognized text into {groupedLines.Count} layout blocks.");

            processingOverlay.UpdateStatus("Translating text...");
            TranslationRequest request = new(groupedLines, sourceLanguage, targetLanguage);
            TranslationResult result = await _translationProvider.TranslateAsync(request, cancellationTokenSource.Token);

            if (!result.Success)
            {
                Logger.LogWarning($"Translation failed: {result.ErrorMessage}");
                if (result.Lines.Count == 0 && !string.IsNullOrEmpty(result.ErrorMessage))
                {
                    // Surface translation error directly in overlay so user knows why translation did not happen
                    string errorMessage = BuildTranslationErrorMessage(result.ErrorMessage);
                    var errorLines = new List<TranslatedLine>
                    {
                        new(
                            OriginalText: "Screen Translator",
                            TranslatedText: errorMessage,
                            BoundingBox: capturedRegionPhysical,
                            Confidence: 1.0,
                            PolygonVertices: null,
                            SourceLineCount: 1),
                    };
                    errorLines = errorLines.ConvertAll(line => line with
                    {
                        OverlayBackgroundColorArgb = 0xFF4A1F1Fu,
                        OverlayForegroundColorArgb = 0xFFFFFFFFu,
                    });

                    _dispatcherQueue.TryEnqueue(() =>
                    {
                        WindowManager.ShowResultOverlay(
                            capturedRegionPhysical,
                            errorLines,
                            sourceLanguage,
                            targetLanguage,
                            (newSource, newTarget) => ProcessCaptureAndTranslateAsync(capturedRegionPhysical, newSource, newTarget),
                            TranslateLineAsync);
                    });
                    return;
                }
            }

            IReadOnlyList<TranslatedLine> styledLines = await Task.Run(
                () => OverlayAppearanceHelper.ApplySampledAppearance(
                    capturedBitmap,
                    capturedRegionPhysical,
                    result.Lines),
                cancellationTokenSource.Token);

            _dispatcherQueue.TryEnqueue(() =>
            {
                WindowManager.ShowResultOverlay(
                    capturedRegionPhysical,
                    styledLines,
                    sourceLanguage,
                    targetLanguage,
                    (newSource, newTarget) => ProcessCaptureAndTranslateAsync(capturedRegionPhysical, newSource, newTarget),
                    TranslateLineAsync);
            });
        }
        catch (OperationCanceledException)
        {
            Logger.LogInfo("Screen translation was cancelled.");
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error in ProcessCaptureAndTranslateAsync: {ex}");
        }
        finally
        {
            WindowManager.CloseProcessingOverlay(processingOverlay);
            capturedBitmap?.Dispose();
        }
    }

    private async Task<TranslationResult> TranslateLineAsync(
        TranslationLine line,
        string sourceLanguage,
        string targetLanguage)
    {
        return await _translationProvider.TranslateAsync(
            new TranslationRequest(new[] { line }, sourceLanguage, targetLanguage));
    }

    private string BuildTranslationErrorMessage(string? errorMessage)
    {
        string providerName = _translationProvider.DisplayName;
        string details = string.IsNullOrWhiteSpace(errorMessage)
            ? "The translation provider returned an unknown error."
            : errorMessage;

        if (providerName.Contains("LibreTranslate", StringComparison.OrdinalIgnoreCase) &&
            (details.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
             details.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase)))
        {
            return $"Translation unavailable\nProvider: {providerName}\n{details}\nStart the local LibreTranslate service or choose another provider in Screen Translator settings.";
        }

        return $"Translation unavailable\nProvider: {providerName}\n{details}\nCheck Screen Translator settings or choose another provider.";
    }
}
