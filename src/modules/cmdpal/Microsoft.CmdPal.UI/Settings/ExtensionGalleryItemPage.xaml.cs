// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using System.Runtime.CompilerServices;
using Microsoft.CmdPal.UI.ViewModels.Gallery;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Microsoft.UI.Xaml.Navigation;
using Windows.System;

namespace Microsoft.CmdPal.UI.Settings;

public sealed partial class ExtensionGalleryItemPage : Page, INotifyPropertyChanged
{
    private const string ScreenshotOpenAnimationKey = "ExtensionGalleryScreenshotOpenAnimation";

    private ExtensionGalleryScreenshotViewModel? _currentScreenshot;
    private ImageSource? _currentScreenshotViewerSource;
    private bool _isScreenshotViewerOpen;

    public event PropertyChangedEventHandler? PropertyChanged;

    public ExtensionGalleryItemViewModel? ViewModel { get; private set; }

    public Visibility ScreenshotViewerVisibility => _isScreenshotViewerOpen ? Visibility.Visible : Visibility.Collapsed;

    public Visibility ScreenshotViewerNavigationVisibility =>
        ViewModel?.Screenshots.Count > 1 ? Visibility.Visible : Visibility.Collapsed;

    public ImageSource? CurrentScreenshotViewerSource => _currentScreenshotViewerSource;

    public string CurrentScreenshotDisplayName => _currentScreenshot?.DisplayName ?? string.Empty;

    public string CurrentScreenshotPositionText =>
        _currentScreenshot is null || ViewModel is null
            ? string.Empty
            : $"{_currentScreenshot.Index + 1} / {ViewModel.Screenshots.Count}";

    public ExtensionGalleryItemPage()
    {
        this.InitializeComponent();
    }

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        if (e.Parameter is ExtensionGalleryItemViewModel vm)
        {
            ViewModel = vm;
            CloseScreenshotViewer();
            Bindings.Update();
        }
    }

    private void StoreMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.InstallViaStoreCommand.Execute(null);
    }

    private async void WinGetMenuItem_Click(object sender, RoutedEventArgs e)
    {
        WinGetDialog.XamlRoot = XamlRoot;
        await WinGetDialog.ShowAsync();
    }

    private void WebMenuItem_Click(object sender, RoutedEventArgs e)
    {
        ViewModel?.OpenInstallUrlCommand.Execute(null);
    }

    private void ScreenshotThumbnail_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button
            && button.Tag is ExtensionGalleryScreenshotViewModel screenshot)
        {
            PrepareScreenshotOpenAnimation(button.Content as UIElement);
            OpenScreenshotViewer(screenshot);
        }
    }

    private void CloseScreenshotViewerButton_Click(object sender, RoutedEventArgs e)
    {
        CloseScreenshotViewer();
    }

    private void PreviousScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        ShowPreviousScreenshot();
    }

    private void NextScreenshotButton_Click(object sender, RoutedEventArgs e)
    {
        ShowNextScreenshot();
    }

    private void ScreenshotViewerOverlay_PointerWheelChanged(object sender, PointerRoutedEventArgs e)
    {
        if (!_isScreenshotViewerOpen || ViewModel?.Screenshots.Count <= 1)
        {
            return;
        }

        var delta = e.GetCurrentPoint(ScreenshotViewerOverlay).Properties.MouseWheelDelta;
        if (delta > 0)
        {
            ShowPreviousScreenshot();
            e.Handled = true;
        }
        else if (delta < 0)
        {
            ShowNextScreenshot();
            e.Handled = true;
        }
    }

    private void ScreenshotViewerOverlay_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (!_isScreenshotViewerOpen)
        {
            return;
        }

        switch (e.Key)
        {
            case VirtualKey.Escape:
                CloseScreenshotViewer();
                e.Handled = true;
                break;
            case VirtualKey.Left:
                ShowPreviousScreenshot();
                e.Handled = true;
                break;
            case VirtualKey.Right:
                ShowNextScreenshot();
                e.Handled = true;
                break;
        }
    }

    private void OpenScreenshotViewer(ExtensionGalleryScreenshotViewModel screenshot)
    {
        _currentScreenshot = screenshot;
        _currentScreenshotViewerSource = CreateViewerImageSource(screenshot.Uri);
        _isScreenshotViewerOpen = true;
        NotifyScreenshotViewerStateChanged();

        DispatcherQueue.TryEnqueue(() =>
        {
            ScreenshotViewerOverlay.UpdateLayout();

            var animation = ConnectedAnimationService.GetForCurrentView().GetAnimation(ScreenshotOpenAnimationKey);
            animation?.TryStart(ScreenshotViewerImageHost);

            ScreenshotViewerOverlay.Focus(FocusState.Programmatic);
        });
    }

    private void CloseScreenshotViewer()
    {
        _currentScreenshot = null;
        _currentScreenshotViewerSource = null;
        _isScreenshotViewerOpen = false;
        NotifyScreenshotViewerStateChanged();
    }

    private void ShowPreviousScreenshot()
    {
        ChangeScreenshot(-1);
    }

    private void ShowNextScreenshot()
    {
        ChangeScreenshot(1);
    }

    private void ChangeScreenshot(int delta)
    {
        if (_currentScreenshot is null || ViewModel is null || ViewModel.Screenshots.Count <= 1)
        {
            return;
        }

        var nextIndex = (_currentScreenshot.Index + delta) % ViewModel.Screenshots.Count;
        if (nextIndex < 0)
        {
            nextIndex += ViewModel.Screenshots.Count;
        }

        OpenScreenshotViewer(ViewModel.Screenshots[nextIndex]);
    }

    private void NotifyScreenshotViewerStateChanged()
    {
        OnPropertyChanged(nameof(ScreenshotViewerVisibility));
        OnPropertyChanged(nameof(ScreenshotViewerNavigationVisibility));
        OnPropertyChanged(nameof(CurrentScreenshotViewerSource));
        OnPropertyChanged(nameof(CurrentScreenshotDisplayName));
        OnPropertyChanged(nameof(CurrentScreenshotPositionText));
    }

    private static ImageSource? CreateViewerImageSource(Uri? screenshotUri)
    {
        if (screenshotUri is null)
        {
            return null;
        }

        var bitmap = new BitmapImage
        {
            UriSource = screenshotUri,
        };

        return bitmap;
    }

    private static void PrepareScreenshotOpenAnimation(UIElement? sourceElement)
    {
        if (sourceElement is null)
        {
            return;
        }

        ConnectedAnimationService.GetForCurrentView().PrepareToAnimate(ScreenshotOpenAnimationKey, sourceElement);
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
