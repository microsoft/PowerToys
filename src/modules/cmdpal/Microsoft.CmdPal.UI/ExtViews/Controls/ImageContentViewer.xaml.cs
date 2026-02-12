// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.Core.ViewModels.Messages;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.System;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.ExtViews.Controls;

public sealed partial class ImageContentViewer : UserControl
{
    private const double MaxHeightSafetyPadding = 12 + 20 + 20; // a few pixels to be safe

    public static readonly DependencyProperty UniformFitEnabledProperty = DependencyProperty.Register(
        nameof(UniformFitEnabled), typeof(bool), typeof(ImageContentViewer), new PropertyMetadata(true));

    private DispatcherQueueTimer? _resizeDebounceTimer;
    private Microsoft.UI.Xaml.Controls.Page? _parentPage;

    public bool UniformFitEnabled
    {
        get => (bool)GetValue(UniformFitEnabledProperty);
        set => SetValue(UniformFitEnabledProperty, value);
    }

    public ContentImageViewModel? ViewModel
    {
        get => (ContentImageViewModel?)DataContext;
        set => DataContext = value;
    }

    public ImageContentViewer()
    {
        InitializeComponent();
        Loaded += ImageContentViewer_Loaded;
        Unloaded += ImageContentViewer_Unloaded;
    }

    private void ImageContentViewer_Loaded(object sender, RoutedEventArgs e)
    {
        ApplyImage();
        ApplyMaxDimensions();
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        // Debounce timer for resize
        var dq = DispatcherQueue.GetForCurrentThread();
        _resizeDebounceTimer = dq.CreateTimer();
        _resizeDebounceTimer.Interval = TimeSpan.FromMilliseconds(120);
        _resizeDebounceTimer.IsRepeating = false;
        _resizeDebounceTimer.Tick += ResizeDebounceTimer_Tick;

        // Hook to parent Page size changes to keep MaxHeight in sync
        _parentPage = FindParentPage();
        if (_parentPage is not null)
        {
            _parentPage.SizeChanged += ParentPage_SizeChanged;
            UpdateBorderMaxHeight();
        }
        else
        {
            // Fallback to this control's ActualHeight
            UpdateBorderMaxHeight(useSelf: true);
        }

        // Initial overlay layout
        LayoutOverlayButton();
    }

    private void ImageContentViewer_Unloaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
        {
            ViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        if (_resizeDebounceTimer is not null)
        {
            _resizeDebounceTimer.Tick -= ResizeDebounceTimer_Tick;
        }

        if (_parentPage is not null)
        {
            _parentPage.SizeChanged -= ParentPage_SizeChanged;
            _parentPage = null;
        }
    }

    private void ParentPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        // Debounce updates a bit to avoid frequent layout passes
        _resizeDebounceTimer?.Start();
        LayoutOverlayButton();
    }

    private void ResizeDebounceTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        UpdateBorderMaxHeight();
        LayoutOverlayButton();
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        var name = e.PropertyName;
        if (name == nameof(ContentImageViewModel.Image))
        {
            ApplyImage();
            LayoutOverlayButton();
        }
        else if (name is nameof(ContentImageViewModel.MaxWidth) or nameof(ContentImageViewModel.MaxHeight))
        {
            ApplyMaxDimensions();
            UpdateBorderMaxHeight();
            LayoutOverlayButton();
        }
    }

    private void ApplyImage()
    {
        Image.SourceKey = ViewModel?.Image;
    }

    private void ApplyMaxDimensions()
    {
        // Apply optional max dimensions from the view model to the content container.
        if (ViewModel is null)
        {
            return;
        }

        ImageBorder.MaxWidth = ViewModel.MaxWidth;
    }

    private async void CopyImage_Click(object sender, RoutedEventArgs e)
    {
        if (this.Image.Source is FontIconSource fontIconSource)
        {
            ClipboardHelper.SetText(fontIconSource.Glyph);
            SendCopiedImageToast();
            return;
        }

        try
        {
            var renderTarget = new RenderTargetBitmap();
            await renderTarget.RenderAsync(this.Image);

            var pixelBuffer = await renderTarget.GetPixelsAsync();
            var pixels = pixelBuffer.ToArray();

            var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(
                BitmapPixelFormat.Bgra8,
                BitmapAlphaMode.Premultiplied,
                (uint)renderTarget.PixelWidth,
                (uint)renderTarget.PixelHeight,
                96,
                96,
                pixels);
            await encoder.FlushAsync();

            var dataPackage = new DataPackage();
            dataPackage.SetBitmap(RandomAccessStreamReference.CreateFromStream(stream));
            Clipboard.SetContent(dataPackage);
            SendCopiedImageToast();
        }
        catch
        {
            CopyImageUri_Click(sender, e);
        }
    }

    private void CopyImageUri_Click(object sender, RoutedEventArgs e)
    {
        var iconVm = ViewModel?.Image;
        var lightTheme = ActualTheme == ElementTheme.Light;
        var data = lightTheme ? iconVm?.Light : iconVm?.Dark;
        var srcKey = data?.Icon ?? string.Empty;
        if (Uri.TryCreate(srcKey, UriKind.Absolute, out var uri) &&
            uri.Scheme is "http" or "https")
        {
            ClipboardHelper.SetText(srcKey);
            WeakReferenceMessenger.Default.Send(new ShowToastMessage(RS_.GetString("ImageContentViewer_Toast_CopiedLink")));
        }
    }

    private static void SendCopiedImageToast()
    {
        WeakReferenceMessenger.Default.Send(new ShowToastMessage(RS_.GetString("ImageContentViewer_Toast_CopiedImage")));
    }

    private void OpenZoomOverlay_Click(object sender, RoutedEventArgs e)
    {
        // Full-window overlay using a Popup attached to this control's XamlRoot
        if (XamlRoot is null)
        {
            return;
        }

        var popup = new Popup
        {
            IsLightDismissEnabled = false,
            XamlRoot = XamlRoot,
        };

        var overlay = new Grid
        {
            Width = XamlRoot.Size.Width,
            Height = XamlRoot.Size.Height,
            Background = new AcrylicBrush
            {
                TintColor = Microsoft.UI.Colors.Black,
                TintOpacity = 0.7,
                TintLuminosityOpacity = 0.5,
                FallbackColor = Microsoft.UI.Colors.Gray,
                AlwaysUseFallback = false,
            },
            TabFocusNavigation = KeyboardNavigationMode.Local,
        };

        // Close popup on Esc pressed at overlay level
        overlay.KeyDown += (s, args) =>
        {
            if (args.Key == VirtualKey.Escape)
            {
                popup.IsOpen = false;
                args.Handled = true;
            }
        };

        var closeBtn = new Button
        {
            Style = (Style)Application.Current.Resources["SubtleButtonStyle"],
            Content = new SymbolIcon { Symbol = Symbol.Cancel },
            HorizontalAlignment = HorizontalAlignment.Right,
            VerticalAlignment = VerticalAlignment.Top,
            Margin = new Thickness(12),
        };

        closeBtn.Click += (_, __) => popup.IsOpen = false;

        // Zoom/pan viewer using current icon
        var viewer = new ImageViewer();
        viewer.Initialize(Image.SourceKey);
        viewer.HorizontalAlignment = HorizontalAlignment.Stretch;
        viewer.VerticalAlignment = VerticalAlignment.Stretch;

        // Also close when viewer requests cancellation (e.g., Escape from viewer)
        viewer.CancelRequested += (_, __) => popup.IsOpen = false;

        overlay.Children.Add(viewer);
        overlay.Children.Add(closeBtn);

        popup.Child = overlay;

        TypedEventHandler<XamlRoot, XamlRootChangedEventArgs>? onRootChanged = (_, _) =>
        {
            overlay.Width = popup.XamlRoot.Size.Width;
            overlay.Height = popup.XamlRoot.Size.Height;
        };

        popup.XamlRoot.Changed += onRootChanged;

        popup.Closed += (_, __) =>
        {
            popup.XamlRoot.Changed -= onRootChanged;
            popup.Child = null;
        };

        popup.IsOpen = true;
        overlay.Focus(FocusState.Programmatic);
    }

    private void OpenZoomOverlayButton_Loaded(object sender, RoutedEventArgs e)
    {
        LayoutOverlayButton();
    }

    private void OpenZoomOverlayButton_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        LayoutOverlayButton();
    }

    private void FitViewbox_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        LayoutOverlayButton();
    }

    private Microsoft.UI.Xaml.Controls.Page? FindParentPage()
    {
        DependencyObject? current = this;
        while (current is not null)
        {
            current = VisualTreeHelper.GetParent(current);
            if (current is Microsoft.UI.Xaml.Controls.Page page)
            {
                return page;
            }
        }

        return null;
    }

    private void UpdateBorderMaxHeight(bool useSelf = false)
    {
        var height = useSelf ? ActualHeight : (_parentPage?.ActualHeight ?? ActualHeight);
        if (height > 0)
        {
            var pageLimit = Math.Max(0, height - MaxHeightSafetyPadding);
            if (ViewModel?.MaxHeight is double vmh and > 0)
            {
                ImageBorder.MaxHeight = Math.Min(pageLimit, vmh);
            }
            else
            {
                ImageBorder.MaxHeight = pageLimit;
            }
        }
        else if (ViewModel?.MaxHeight is double vmh2 and > 0)
        {
            ImageBorder.MaxHeight = vmh2; // fallback if page height not ready
        }
    }

    private void LayoutOverlayButton()
    {
        if (OpenZoomOverlayButton is null || FitViewbox is null || OverlayCanvas is null || Image is null)
        {
            return;
        }

        // If layout isn't ready, skip
        if (FitViewbox.ActualWidth <= 0 || FitViewbox.ActualHeight <= 0 || OverlayCanvas.ActualWidth <= 0 || OverlayCanvas.ActualHeight <= 0)
        {
            return;
        }

        // Compute the transformed bounds of the image content relative to the overlay canvas.
        // This accounts for Viewbox scaling/clipping due to MaxHeight constraints and custom max dimensions.
        try
        {
            var gt = Image.TransformToVisual(OverlayCanvas);
            var rect = gt.TransformBounds(new Rect(0, 0, Image.ActualWidth, Image.ActualHeight));

            const double margin = 8.0;
            var buttonWidth = OpenZoomOverlayButton.ActualWidth;

            var x = rect.Right - buttonWidth - margin;
            var y = rect.Top + margin;

            // Clamp inside overlay bounds just in case
            if (x < margin)
            {
                x = margin;
            }

            if (y < margin)
            {
                y = margin;
            }

            if (x > OverlayCanvas.ActualWidth - buttonWidth - margin)
            {
                x = OverlayCanvas.ActualWidth - buttonWidth - margin;
            }

            if (y > OverlayCanvas.ActualHeight - OpenZoomOverlayButton.ActualHeight - margin)
            {
                y = OverlayCanvas.ActualHeight - OpenZoomOverlayButton.ActualHeight - margin;
            }

            Canvas.SetLeft(OpenZoomOverlayButton, x);
            Canvas.SetTop(OpenZoomOverlayButton, y);
        }
        catch
        {
            // Fallback: keep it at top-right of the viewbox area
            const double margin = 8.0;
            var buttonWidth = OpenZoomOverlayButton.ActualWidth;
            var x = FitViewbox.ActualWidth - buttonWidth - margin;
            var y = margin;
            Canvas.SetLeft(OpenZoomOverlayButton, x);
            Canvas.SetTop(OpenZoomOverlayButton, y);
        }
    }
}
