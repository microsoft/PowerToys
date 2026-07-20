// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml.Media.Imaging;
using PowerOCR.Core.Models;
using PowerOCR.Helpers;
using PowerOCR.Models;
using PowerOCR.Telemetry;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace PowerOCR.Services;

internal sealed class OverlayManager : IOverlayManager
{
    private readonly IActivationService _activationService;
    private readonly IScreenCaptureService _screenCaptureService;
    private readonly IOverlayWindowFactory _windowFactory;
    private readonly List<OCROverlay> _activeWindows = new();
    private readonly List<DisplayCapture> _activeCaptures = new();
    private CancellationTokenSource? _sessionCts;
    private bool _sessionActive;
    private bool _disposed;

    public OverlayManager(
        IActivationService activationService,
        IScreenCaptureService screenCaptureService,
        IOverlayWindowFactory windowFactory)
    {
        _activationService = activationService;
        _screenCaptureService = screenCaptureService;
        _windowFactory = windowFactory;

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

        var displays = DisplayArea.FindAll();
        if (displays.Count == 0)
        {
            Logger.LogWarning("No displays found for capture.");
            _sessionActive = false;
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

            _sessionActive = false;
            return;
        }
        catch (Exception ex)
        {
            Logger.LogError("Screen capture failed", ex);
            captureFailed = true;
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

                var errorWindow = _windowFactory.Create(errorCapture, this);
                _activeWindows.Add(errorWindow);
                errorWindow.Activate();

                string errorMessage = ResourceLoaderInstance.ResourceLoader.GetString("ScreenCaptureFailed");
                errorWindow.ShowError(errorMessage);
            }
            catch (Exception ex2)
            {
                Logger.LogError("Failed to show error overlay", ex2);
                foreach (var cap in captures)
                {
                    cap.Dispose();
                }

                _sessionActive = false;
            }

            return;
        }

        // All captures succeeded – create overlay windows
        _activeCaptures.AddRange(captures);

        try
        {
            foreach (var capture in captures)
            {
                var window = _windowFactory.Create(capture, this);
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

            foreach (var window in _activeWindows)
            {
                window.CloseFromManager();
            }

            _activeWindows.Clear();

            foreach (var cap in _activeCaptures)
            {
                cap.Dispose();
            }

            _activeCaptures.Clear();

            _sessionCts?.Cancel();
            _sessionCts?.Dispose();
            _sessionCts = null;

            _sessionActive = false;
        }
    }

    public void CloseAll(bool cancelled)
    {
        if (!_sessionActive)
        {
            return;
        }

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
