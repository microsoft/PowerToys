// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PowerOCR.Models;
using PowerOCR.Services;

namespace PowerOCR.Views;

/// <summary>
/// Minimal overlay page showing the captured screenshot with a dim layer,
/// an error InfoBar, and a Cancel button.
/// </summary>
public sealed partial class OverlayPage : UserControl
{
    private OCROverlay? _parentWindow;
    private IOverlayManager? _manager;

    public OverlayPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    /// <summary>
    /// Initializes the page with the display capture and overlay references.
    /// </summary>
    internal void Initialize(OCROverlay parentWindow, DisplayCapture capture, IOverlayManager manager)
    {
        _parentWindow = parentWindow;
        _manager = manager;
        BackgroundImage.Source = capture.ImageSource;
    }

    /// <summary>
    /// Displays an error message in the InfoBar.
    /// </summary>
    internal void ShowError(string message)
    {
        ErrorInfoBar.Message = message;
        ErrorInfoBar.IsOpen = true;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        RootGrid.Focus(FocusState.Programmatic);
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _manager?.CloseAll(cancelled: true);
    }
}
