// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;

using ManagedCommon;
using Microsoft.UI.Xaml;
using PowerOCR.Core.Models;
using PowerOCR.Helpers;
using PowerOCR.ViewModels;
using Windows.Foundation;
using Windows.System;
using WinUIEx;

namespace PowerOCR.Views;

[System.Diagnostics.CodeAnalysis.SuppressMessage("Design", "CA1001:Types that own disposable fields should be disposable", Justification = "WinUI owns the control lifetime. StopNativeSelection disposes the native window on Unloaded, Window.Closed, explicit session close, and initialization failure.")]
public sealed partial class OverlayPage
{
    private NativeSelectionWindow? _nativeSelectionWindow;
    private Microsoft.UI.Dispatching.DispatcherQueue? _nativeDispatcher;
    private PixelRect[] _nativeExclusions = Array.Empty<PixelRect>();
    private bool _nativePrototypeEnabled;
    private bool _nativeStarted;
    private bool _nativeClosing;
    private bool _nativeLanguagePopupOpen;
    private bool _nativeContextPopupOpen;
    private bool? _nativeVisible;

    private void InitializeNativeSelection()
    {
        if (_parentWindow is null || _capture is null ||
            !File.Exists(Path.Combine(Logger.AppLogDirectoryPath, "cursor-native-overlay.enabled")))
        {
            return;
        }

        _nativePrototypeEnabled = true;
        _nativeDispatcher = _parentWindow.DispatcherQueue;
        try
        {
            _nativeSelectionWindow = new NativeSelectionWindow(
                _parentWindow.GetWindowHandle(),
                _capture,
                (selection, isClick) => EnqueueNativeCallback(() => CompleteNativeSelection(selection, isClick)),
                () => EnqueueNativeCallback(UpdateNativeSelectionExclusions),
                (key, shiftDown) => EnqueueNativeCallback(() => HandleNativeKey((VirtualKey)key, shiftDown)),
                point => EnqueueNativeCallback(() => ShowNativeContextMenu(point)),
                message => EnqueueNativeCallback(() => FailNativeSelection(message)));

            LayoutUpdated += OnNativeLayoutUpdated;
            ViewModel.PropertyChanged += OnNativeViewModelChanged;
            LanguagesComboBox.DropDownOpened += OnNativeLanguagePopupOpened;
            LanguagesComboBox.DropDownClosed += OnNativeLanguagePopupClosed;
            ContextMenuFlyout.Opening += OnNativeContextPopupOpening;
            ContextMenuFlyout.Closed += OnNativeContextPopupClosed;
            _parentWindow.Closed += OnNativeParentClosed;
            _parentWindow.Activated += OnNativeParentActivated;
            Logger.LogInfo("PowerOCR native selection prototype enabled. The selection surface uses a separate Win32 HWND and UI thread.");
        }
        catch (Exception exception)
        {
            StopNativeSelection();
            Logger.LogError("Could not initialize the native selection prototype.", exception);
            throw;
        }
    }

    private void StartNativeSelection()
    {
        if (_nativeSelectionWindow is null || _nativeStarted || _nativeClosing || !IsLoaded || Toolbar.ActualWidth <= 0 || Toolbar.ActualHeight <= 0)
        {
            return;
        }

        try
        {
            UpdateNativeSelectionExclusions();
            _nativeStarted = true;
            UpdateNativeSelectionVisibility();
            _nativeSelectionWindow.Start();
        }
        catch (Exception exception)
        {
            StopNativeSelection();
            Logger.LogError("Could not start the native selection prototype.", exception);
            _manager?.CloseAll(cancelled: true);
        }
    }

    internal void PrepareForWindowClose()
    {
        _nativeClosing = true;
        _cursorDiagnostics?.RecordCanvasState("window-close-preparing");
    }

    internal void StopNativeSelection()
    {
        _nativeClosing = true;
        NativeSelectionWindow? nativeWindow = _nativeSelectionWindow;
        _nativeSelectionWindow = null;
        _nativeStarted = false;
        _nativeVisible = null;
        _nativePrototypeEnabled = false;

        LayoutUpdated -= OnNativeLayoutUpdated;
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= OnNativeViewModelChanged;
        }

        LanguagesComboBox.DropDownOpened -= OnNativeLanguagePopupOpened;
        LanguagesComboBox.DropDownClosed -= OnNativeLanguagePopupClosed;
        ContextMenuFlyout.Opening -= OnNativeContextPopupOpening;
        ContextMenuFlyout.Closed -= OnNativeContextPopupClosed;
        if (_parentWindow is not null)
        {
            _parentWindow.Closed -= OnNativeParentClosed;
            _parentWindow.Activated -= OnNativeParentActivated;
        }

        // The native thread owns its screenshot clone and performs HWND/capture cleanup.
        // It never waits for a UI callback, and the WinUI dispatcher must not wait for it.
        nativeWindow?.Dispose();
    }

    private void EnqueueNativeCallback(Action callback)
    {
        bool enqueued = _nativeDispatcher?.TryEnqueue(() =>
        {
            if (_nativeSelectionWindow is null || _nativeClosing || !IsLoaded)
            {
                return;
            }

            try
            {
                callback();
            }
            catch (Exception exception)
            {
                Logger.LogError("Native selection callback failed.", exception);
                _manager?.CloseAll(cancelled: true);
            }
        }) ?? false;

        if (!enqueued)
        {
            _nativeSelectionWindow?.Dispose();
        }
    }

    private async void CompleteNativeSelection(PixelSelection selection, bool isClick)
    {
        if (_capture is null || _manager is null || ViewModel.IsProcessing)
        {
            return;
        }

        try
        {
            _parentWindow?.Activate();
            LanguagesComboBox.Focus(FocusState.Programmatic);
            await _manager.CaptureAsync(_capture, selection, isClick);
        }
        catch (Exception exception)
        {
            Logger.LogError("Native selection capture failed.", exception);
            _manager.CloseAll(cancelled: true);
        }
    }

    private void FailNativeSelection(string message)
    {
        Logger.LogError($"Native selection prototype failed: {message}");
        _manager?.CloseAll(cancelled: true);
    }

    private void HandleNativeKey(VirtualKey key, bool shiftDown)
    {
        if (key == VirtualKey.Tab)
        {
            _nativeSelectionWindow?.CancelSelection();
            _parentWindow?.Activate();
            if (shiftDown)
            {
                CancelButton.Focus(FocusState.Keyboard);
            }
            else
            {
                LanguagesComboBox.Focus(FocusState.Keyboard);
            }

            return;
        }

        _ = HandleOverlayKey(key, shiftDown);
    }

    private void OnNativeParentClosed(object sender, WindowEventArgs args) => StopNativeSelection();

    private void OnNativeParentActivated(object sender, WindowActivatedEventArgs args)
    {
        if (!_nativeClosing && args.WindowActivationState != WindowActivationState.Deactivated)
        {
            UpdateNativeSelectionVisibility();
            if (_nativeVisible == true)
            {
                _nativeSelectionWindow?.BringToFront();
            }
        }
    }

    private void OnNativeLayoutUpdated(object? sender, object args)
    {
        try
        {
            UpdateNativeSelectionExclusions();
            StartNativeSelection();
        }
        catch (Exception exception)
        {
            Logger.LogError("Could not update the native selection layout.", exception);
            _manager?.CloseAll(cancelled: true);
        }
    }

    private void OnNativeViewModelChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(OverlaySessionViewModel.IsProcessing))
        {
            UpdateNativeSelectionVisibility();
        }

        if (args.PropertyName == nameof(OverlaySessionViewModel.HasError))
        {
            UpdateNativeSelectionExclusions();
        }
    }

    private void OnNativeLanguagePopupOpened(object? sender, object args)
    {
        _nativeLanguagePopupOpen = true;
        UpdateNativeSelectionVisibility();
    }

    private void OnNativeLanguagePopupClosed(object? sender, object args)
    {
        _nativeLanguagePopupOpen = false;
        UpdateNativeSelectionVisibility();
    }

    private void OnNativeContextPopupOpening(object? sender, object args)
    {
        _nativeContextPopupOpen = true;
        UpdateNativeSelectionVisibility();
    }

    private void OnNativeContextPopupClosed(object? sender, object args)
    {
        _nativeContextPopupOpen = false;
        UpdateNativeSelectionVisibility();
    }

    private void PrepareNativeContextMenu()
    {
        if (!_nativePrototypeEnabled)
        {
            return;
        }

        _nativeContextPopupOpen = true;
        UpdateNativeSelectionVisibility();
        _parentWindow?.Activate();
    }

    private void ShowNativeContextMenu(Point physicalPoint)
    {
        double scale = XamlRoot?.RasterizationScale ?? 1;
        Point clientOffset = GetNativeClientOffset();
        PrepareNativeContextMenu();
        ContextMenuFlyout.ShowAt(RegionClickCanvas, new Point((physicalPoint.X - clientOffset.X) / scale, (physicalPoint.Y - clientOffset.Y) / scale));
    }

    private void UpdateNativeSelectionVisibility()
    {
        if (_nativeSelectionWindow is null || !_nativeStarted || _nativeClosing)
        {
            return;
        }

        // WinUI owns the popup and processing UI. Expose it while it is active.
        bool visible = _parentWindow?.AppWindow.IsVisible == true && !ViewModel.IsProcessing
            && !_nativeLanguagePopupOpen && !_nativeContextPopupOpen;
        if (_nativeVisible != visible)
        {
            _nativeVisible = visible;
            _nativeSelectionWindow.SetVisible(visible);
        }
    }

    private void UpdateNativeSelectionExclusions()
    {
        if (_nativeSelectionWindow is null || _nativeClosing || _capture is null || XamlRoot is null)
        {
            return;
        }

        var exclusions = new List<PixelRect>(2);
        AddNativeExclusion(Toolbar, exclusions);
        if (ErrorInfoBar.IsOpen)
        {
            AddNativeExclusion(ErrorInfoBar, exclusions);
        }

        if (!_nativeExclusions.SequenceEqual(exclusions))
        {
            _nativeExclusions = exclusions.ToArray();
            _nativeSelectionWindow.SetExclusions(_nativeExclusions);
        }
    }

    private void AddNativeExclusion(FrameworkElement element, List<PixelRect> exclusions)
    {
        if (_capture is null || element.Visibility != Visibility.Visible || element.ActualWidth <= 0 || element.ActualHeight <= 0)
        {
            return;
        }

        double scale = XamlRoot.RasterizationScale;
        Point clientOffset = GetNativeClientOffset();
        Rect bounds = element.TransformToVisual(this).TransformBounds(new Rect(0, 0, element.ActualWidth, element.ActualHeight));
        int left = Math.Clamp((int)Math.Floor((bounds.Left * scale) + clientOffset.X), 0, _capture.Bounds.Width);
        int top = Math.Clamp((int)Math.Floor((bounds.Top * scale) + clientOffset.Y), 0, _capture.Bounds.Height);
        int right = Math.Clamp((int)Math.Ceiling((bounds.Right * scale) + clientOffset.X), left, _capture.Bounds.Width);
        int bottom = Math.Clamp((int)Math.Ceiling((bounds.Bottom * scale) + clientOffset.Y), top, _capture.Bounds.Height);
        if (right > left && bottom > top)
        {
            exclusions.Add(new PixelRect(left, top, right - left, bottom - top));
        }
    }

    private Point GetNativeClientOffset()
    {
        if (_parentWindow is null || _capture is null)
        {
            return default;
        }

        NativeSelectionInterop.Point origin = default;
        if (NativeSelectionInterop.ClientToScreen(_parentWindow.GetWindowHandle(), ref origin) == 0)
        {
            throw new Win32Exception("Could not locate the WinUI toolbar's client origin.");
        }

        return new Point(origin.X - _capture.Bounds.X, origin.Y - _capture.Bounds.Y);
    }
}
