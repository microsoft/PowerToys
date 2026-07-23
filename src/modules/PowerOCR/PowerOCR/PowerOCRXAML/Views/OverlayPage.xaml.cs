// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using PowerOCR.Core.Geometry;
using PowerOCR.Core.Models;
using PowerOCR.Helpers;
using PowerOCR.Models;
using PowerOCR.Services;
using PowerOCR.ViewModels;
using Windows.System;

namespace PowerOCR.Views;

/// <summary>
/// Complete overlay page with selection masks, pointer capture, toolbar,
/// keyboard shortcuts, context menu, and progress/error states.
/// </summary>
public sealed partial class OverlayPage : UserControl
{
    private OCROverlay? _parentWindow;
    private IOverlayManager? _manager;
    private DisplayCapture? _capture;
    private SettingsDeepLink? _settingsDeepLink;
    private InputCursor? _selectionCursor;

    private bool _isSelecting;
    private double _anchorX;
    private double _anchorY;
    private double _selX;
    private double _selY;
    private double _selWidth;
    private double _selHeight;
    private double _lastPointerX;
    private double _lastPointerY;

    internal OverlaySessionViewModel ViewModel { get; private set; } = null!;

    public OverlayPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        SizeChanged += OnSizeChanged;
    }

    /// <summary>
    /// Initializes the page with display capture and overlay references.
    /// </summary>
    internal void Initialize(
        OCROverlay parentWindow,
        DisplayCapture capture,
        IOverlayManager manager,
        OverlaySessionViewModel viewModel,
        SettingsDeepLink settingsDeepLink)
    {
        _parentWindow = parentWindow;
        _manager = manager;
        _capture = capture;
        _settingsDeepLink = settingsDeepLink;
        ViewModel = viewModel;

        BackgroundImage.Source = capture.ImageSource;

        // Masks are sized against the real layout dimensions once the page is laid out
        // (see OnLoaded / OnSizeChanged). Computing them here would run before the first
        // layout pass, when ActualWidth/ActualHeight are still 0.
    }

    /// <summary>
    /// Displays an error message in the InfoBar.
    /// </summary>
    internal void ShowError(string message)
    {
        ViewModel.ErrorMessage = message;
        ViewModel.HasError = true;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RootGrid.Focus(FocusState.Programmatic);
        RefreshMasks();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        ProtectedCursor = null;
        _selectionCursor?.Dispose();
        _selectionCursor = null;
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        RefreshMasks();
    }

    /// <summary>
    /// Recomputes the selection masks against the current layout size. Preserves an
    /// in-progress selection; otherwise dims the whole surface. Called on the first
    /// rendered layout and on every later size change (e.g. DPI or resolution changes),
    /// so the full-cover mask always matches the real display bounds.
    /// </summary>
    private void RefreshMasks()
    {
        if (_isSelecting)
        {
            UpdateMasks();
        }
        else
        {
            UpdateMasksFullCover();
        }
    }

    private void UpdateMasksFullCover()
    {
        double pageHeight = ActualHeight;
        if (pageHeight <= 0)
        {
            // Layout has not run yet; masks are recomputed on Loaded/SizeChanged.
            return;
        }

        TopMask.Height = pageHeight;
        BottomMask.Height = 0;
        LeftMask.Width = 0;
        LeftMask.Margin = new Thickness(0, pageHeight, 0, 0);
        RightMask.Width = 0;
        RightMask.Margin = new Thickness(0, pageHeight, 0, 0);
        SelectionBorder.Visibility = Visibility.Collapsed;
    }

    private void UpdateMasks()
    {
        double pageWidth = ActualWidth > 0 ? ActualWidth : 1920;
        double pageHeight = ActualHeight > 0 ? ActualHeight : 1080;

        double selBottom = _selY + _selHeight;
        double selRight = _selX + _selWidth;

        // Top mask: full width, from top to selection top
        TopMask.Height = Math.Max(0, _selY);

        // Bottom mask: full width, from selection bottom to page bottom
        BottomMask.Height = Math.Max(0, pageHeight - selBottom);

        // Left mask: from left edge to selection left, height = selection height
        LeftMask.Width = Math.Max(0, _selX);
        LeftMask.Margin = new Thickness(0, _selY, 0, pageHeight - selBottom);

        // Right mask: from selection right to right edge, height = selection height
        RightMask.Width = Math.Max(0, pageWidth - selRight);
        RightMask.Margin = new Thickness(0, _selY, 0, pageHeight - selBottom);

        // Selection border
        SelectionBorder.Visibility = Visibility.Visible;
        SelectionBorder.Margin = new Thickness(_selX - 2, _selY - 2, 0, 0);
        SelectionBorder.Width = _selWidth + 4;
        SelectionBorder.Height = _selHeight + 4;
    }

    private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var props = e.GetCurrentPoint(RegionClickCanvas).Properties;
        if (!props.IsLeftButtonPressed)
        {
            return;
        }

        // Hide toolbar during selection
        Toolbar.Visibility = Visibility.Collapsed;

        // Capture pointer
        RegionClickCanvas.CapturePointer(e.Pointer);

        // Clip cursor to display bounds
        if (_capture is not null)
        {
            CursorClipper.Clip(_capture.Bounds);
        }

        // Store anchor
        var position = e.GetCurrentPoint(RegionClickCanvas).Position;
        _anchorX = position.X;
        _anchorY = position.Y;
        _lastPointerX = position.X;
        _lastPointerY = position.Y;

        // Initialize selection
        _selX = _anchorX;
        _selY = _anchorY;
        _selWidth = 0;
        _selHeight = 0;
        _isSelecting = true;

        UpdateMasks();
        e.Handled = true;
    }

    private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        var position = e.GetCurrentPoint(RegionClickCanvas).Position;
        double currentX = position.X;
        double currentY = position.Y;

        bool shiftDown = InputKeyboardSource
            .GetKeyStateForCurrentThread(VirtualKey.Shift)
            .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down);

        double pageWidth = ActualWidth > 0 ? ActualWidth : 1920;
        double pageHeight = ActualHeight > 0 ? ActualHeight : 1080;

        if (shiftDown)
        {
            // Translate existing selection by delta
            double deltaX = currentX - _lastPointerX;
            double deltaY = currentY - _lastPointerY;

            double newSelectionX = Math.Clamp(
                _selX + deltaX,
                0,
                Math.Max(0, pageWidth - _selWidth));
            double newSelectionY = Math.Clamp(
                _selY + deltaY,
                0,
                Math.Max(0, pageHeight - _selHeight));
            double appliedDeltaX = newSelectionX - _selX;
            double appliedDeltaY = newSelectionY - _selY;

            _selX = newSelectionX;
            _selY = newSelectionY;
            _anchorX += appliedDeltaX;
            _anchorY += appliedDeltaY;
        }
        else
        {
            // Resize from anchor
            double left = Math.Min(_anchorX, currentX);
            double top = Math.Min(_anchorY, currentY);
            double right = Math.Max(_anchorX, currentX);
            double bottom = Math.Max(_anchorY, currentY);

            _selX = Math.Clamp(left, 0, pageWidth);
            _selY = Math.Clamp(top, 0, pageHeight);
            _selWidth = Math.Clamp(right - _selX, 0, pageWidth - _selX);
            _selHeight = Math.Clamp(bottom - _selY, 0, pageHeight - _selY);
        }

        _lastPointerX = currentX;
        _lastPointerY = currentY;

        UpdateMasks();
        e.Handled = true;
    }

    /// <summary>
    /// Shared cleanup for normal release, pointer-canceled, and capture-lost paths.
    /// When <paramref name="pointer"/> is non-null the capture is still held and must be
    /// released explicitly; pass null when capture has already been taken away by the system.
    /// </summary>
    private void EndSelectionCleanup(Pointer? pointer)
    {
        _isSelecting = false;

        if (pointer is not null)
        {
            RegionClickCanvas.ReleasePointerCapture(pointer);
        }

        CursorClipper.UnClip();
        Toolbar.Visibility = Visibility.Visible;
        ResetSelectionVisuals();
    }

    private void ResetSelectionVisuals()
    {
        _selX = 0;
        _selY = 0;
        _selWidth = 0;
        _selHeight = 0;
        SelectionBorder.Visibility = Visibility.Collapsed;
        RefreshMasks();
    }

    private async void Canvas_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (!_isSelecting)
        {
            return;
        }

        if (_capture is null || _manager is null || XamlRoot is null)
        {
            EndSelectionCleanup(e.Pointer);
            return;
        }

        // Convert DIP selection to physical pixels
        double scale = XamlRoot.RasterizationScale;
        var firstDip = new OcrPoint(_selX, _selY);
        var secondDip = new OcrPoint(_selX + _selWidth, _selY + _selHeight);
        var pixelSelection = SelectionGeometry.ToPixels(firstDip, secondDip, scale, _capture.Bounds);

        // Treat width or height below 3 physical pixels as clicked-word mode
        bool isClick = pixelSelection.Local.Width < 3 || pixelSelection.Local.Height < 3;

        EndSelectionCleanup(e.Pointer);
        await _manager.CaptureAsync(_capture, pixelSelection, isClick);
        e.Handled = true;
    }

    private void Canvas_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        // Fires when the system revokes pointer capture mid-drag (e.g. task-switch, touch
        // cancellation). Capture is already gone so pass null to skip the redundant release.
        if (_isSelecting)
        {
            EndSelectionCleanup(null);
        }
    }

    private void Canvas_PointerCanceled(object sender, PointerRoutedEventArgs e)
    {
        // Fires when the system cancels the pointer interaction (e.g. stylus lifted out of
        // range, device disconnected). Capture is still ours; release it explicitly.
        if (_isSelecting)
        {
            EndSelectionCleanup(e.Pointer);
        }
    }

    private void RootGrid_ContextRequested(UIElement sender, ContextRequestedEventArgs e)
    {
        if (e.TryGetPosition(RegionClickCanvas, out var position))
        {
            // Preserve the previous pointer behavior: toolbar controls should handle their
            // own context requests rather than opening the canvas language menu.
            if (!ReferenceEquals(e.OriginalSource, RegionClickCanvas))
            {
                return;
            }

            ContextMenuFlyout.ShowAt(RegionClickCanvas, position);
        }
        else
        {
            ShowContextMenuForKeyboard();
        }

        e.Handled = true;
    }

    private void Canvas_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != PointerDeviceType.Mouse)
        {
            return;
        }

        _selectionCursor ??= InputSystemCursor.Create(InputSystemCursorShape.Cross);
        ProtectedCursor = _selectionCursor;
    }

    private void Canvas_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType == PointerDeviceType.Mouse)
        {
            ProtectedCursor = null;
        }
    }

    private void ContextMenuFlyout_Opening(object sender, object e)
    {
        PopulateLanguageFlyoutItems();
    }

    private void ShowContextMenuForKeyboard()
    {
        // With no pointer position, place the menu near the toolbar where keyboard focus starts.
        double x = Math.Max(0, RegionClickCanvas.ActualWidth / 2);
        double y = Math.Max(0, Toolbar.ActualHeight + Toolbar.Margin.Top);
        ContextMenuFlyout.ShowAt(RegionClickCanvas, new Windows.Foundation.Point(x, y));
    }

    private void PopulateLanguageFlyoutItems()
    {
        // Remove the dynamic language items while preserving the fixed commands.
        while (ContextMenuFlyout.Items.Count > 0
               && !ReferenceEquals(ContextMenuFlyout.Items[0], LanguageSeparator))
        {
            ContextMenuFlyout.Items.RemoveAt(0);
        }

        LanguageSeparator.Visibility = ViewModel.Languages.Count > 0
            ? Visibility.Visible
            : Visibility.Collapsed;

        if (ViewModel.Languages.Count > 0)
        {
            int idx = 0;
            foreach (var lang in ViewModel.Languages)
            {
                var item = new RadioMenuFlyoutItem
                {
                    GroupName = "OCRLanguage",
                    IsChecked = string.Equals(
                        lang.LanguageTag,
                        ViewModel.SelectedLanguage?.LanguageTag,
                        StringComparison.OrdinalIgnoreCase),
                    Text = lang.NativeName,
                };
                item.SetValue(
                    Microsoft.UI.Xaml.Automation.AutomationProperties.AutomationIdProperty,
                    $"OCRLanguageMenuItem_{idx}");
                var capturedLang = lang;
                item.Click += (s, e) => ViewModel.SelectedLanguage = capturedLang;
                ContextMenuFlyout.Items.Insert(idx, item);
                idx++;
            }
        }
    }

    private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        switch (e.Key)
        {
            case VirtualKey.Application:
                ShowContextMenuForKeyboard();
                e.Handled = true;
                break;

            case VirtualKey.F10:
                if (InputKeyboardSource
                    .GetKeyStateForCurrentThread(VirtualKey.Shift)
                    .HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
                {
                    ShowContextMenuForKeyboard();
                    e.Handled = true;
                }

                break;

            case VirtualKey.Escape:
                _manager?.CloseAll(cancelled: true);
                e.Handled = true;
                break;

            case VirtualKey.S:
                ViewModel.IsSingleLine = !ViewModel.IsSingleLine;
                e.Handled = true;
                break;

            case VirtualKey.T:
                ViewModel.IsTable = !ViewModel.IsTable;
                e.Handled = true;
                break;

            case VirtualKey.Number1:
            case VirtualKey.Number2:
            case VirtualKey.Number3:
            case VirtualKey.Number4:
            case VirtualKey.Number5:
            case VirtualKey.Number6:
            case VirtualKey.Number7:
            case VirtualKey.Number8:
            case VirtualKey.Number9:
                int langIndex = e.Key - VirtualKey.Number1;
                if (langIndex < ViewModel.Languages.Count)
                {
                    ViewModel.SelectedLanguage = ViewModel.Languages[langIndex];
                }

                e.Handled = true;
                break;
        }
    }

    private void SettingsButton_Click(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }

    private void SettingsMenuItem_Click(object sender, RoutedEventArgs e)
    {
        OpenSettings();
    }

    private void OpenSettings()
    {
        _settingsDeepLink?.Open();
        _manager?.CloseAll(cancelled: false);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _manager?.CloseAll(cancelled: true);
    }

    private void CancelMenuItem_Click(object sender, RoutedEventArgs e)
    {
        _manager?.CloseAll(cancelled: true);
    }
}
