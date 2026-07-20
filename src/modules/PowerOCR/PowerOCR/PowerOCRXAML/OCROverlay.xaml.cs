// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Windowing;
using PowerOCR.Models;
using PowerOCR.Services;
using PowerOCR.ViewModels;
using Windows.Graphics;
using WinUIEx;

namespace PowerOCR;

/// <summary>
/// Borderless topmost overlay window that covers a single display.
/// One instance is created per captured display.
/// </summary>
public sealed partial class OCROverlay : WindowEx
{
    private readonly IOverlayManager _manager;
    private bool _closingFromManager;

    public OCROverlay(
        DisplayCapture capture,
        OverlaySessionViewModel viewModel,
        IOverlayManager manager,
        SettingsDeepLink settingsDeepLink)
    {
        _manager = manager;

        InitializeComponent();

        AppWindow.Title = "Text Extractor";

        OverlayContent.Initialize(this, capture, manager, viewModel, settingsDeepLink);

        var bounds = capture.Bounds;
        AppWindow.MoveAndResize(new RectInt32(bounds.X, bounds.Y, bounds.Width, bounds.Height));

        AppWindow.Closing += OnAppWindowClosing;
    }

    /// <summary>
    /// Shows an error message in the overlay InfoBar.
    /// </summary>
    public void ShowError(string message)
    {
        OverlayContent.ShowError(message);
    }

    /// <summary>
    /// Closes this window without re-triggering CloseAll from the manager.
    /// Called by OverlayManager during session teardown.
    /// </summary>
    public void CloseFromManager()
    {
        _closingFromManager = true;
        Close();
    }

    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (!_closingFromManager)
        {
            // User-initiated close (e.g., Alt+F4) – cancel all overlays
            args.Cancel = true;
            _manager.CloseAll(cancelled: true);
        }
    }
}
