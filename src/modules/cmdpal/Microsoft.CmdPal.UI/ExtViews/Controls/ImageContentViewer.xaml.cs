// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.CmdPal.UI.Controls;
using Microsoft.CmdPal.UI.Helpers;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Animation;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.ViewManagement;
using DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue;
using DispatcherQueueTimer = Microsoft.UI.Dispatching.DispatcherQueueTimer;
using RS_ = Microsoft.CmdPal.UI.Helpers.ResourceLoaderInstance;

namespace Microsoft.CmdPal.UI.ExtViews.Controls;

public sealed partial class ImageContentViewer : UserControl
{
    private const double MaxHeightSafetyPadding = 12 + 20 + 20; // a few pixels to be safe

    public static readonly DependencyProperty UniformFitEnabledProperty = DependencyProperty.Register(
        nameof(UniformFitEnabled), typeof(bool), typeof(ImageContentViewer), new PropertyMetadata(true));

    public static readonly DependencyProperty FitStreamDecodeToBoundsProperty = DependencyProperty.Register(
        nameof(FitStreamDecodeToBounds), typeof(bool), typeof(ImageContentViewer), new PropertyMetadata(false, OnFitStreamDecodeToBoundsChanged));

    private readonly object? _zoomButtonToolTip;
    private DispatcherQueueTimer? _resizeDebounceTimer;
    private Microsoft.UI.Xaml.Controls.Page? _parentPage;
    private ContentImageViewModel? _observedViewModel;
    private UISettings? _uiSettings;
    private Storyboard? _zoomButtonAnimation;
    private double? _expandedZoomButtonWidth;
    private double _targetZoomButtonWidth;
    private bool _zoomButtonHovered;
    private Size _availableSize;
    private ImagePreviewDecode? _previewDecode;

    public bool UniformFitEnabled
    {
        get => (bool)GetValue(UniformFitEnabledProperty);
        set => SetValue(UniformFitEnabledProperty, value);
    }

    public bool FitStreamDecodeToBounds
    {
        get => (bool)GetValue(FitStreamDecodeToBoundsProperty);
        set => SetValue(FitStreamDecodeToBoundsProperty, value);
    }

    public ContentImageViewModel? ViewModel
    {
        get => DataContext as ContentImageViewModel;
        set => DataContext = value;
    }

    public ImageContentViewer()
    {
        InitializeComponent();
        _targetZoomButtonWidth = OpenZoomOverlayButton.Width;
        _zoomButtonToolTip = ToolTipService.GetToolTip(OpenZoomOverlayButton);
        Image.SourceRequestCompleted += Image_SourceRequestCompleted;
        Loaded += ImageContentViewer_Loaded;
        Unloaded += ImageContentViewer_Unloaded;
        DataContextChanged += ImageContentViewer_DataContextChanged;
    }

    private static void OnFitStreamDecodeToBoundsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is ImageContentViewer viewer &&
            viewer.Image is { } image &&
            GetImageStream(image.SourceKey as IconInfoViewModel, image.ActualTheme) is not null)
        {
            image.RefreshSourceForSizeChange();
        }
    }

    private void ImageContentViewer_Loaded(object sender, RoutedEventArgs e)
    {
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
        }

        ApplyViewModel();
    }

    private void ImageContentViewer_Unloaded(object sender, RoutedEventArgs e)
    {
        ObserveViewModel(null);
        PreviewLayout.MinHeight = 0;
        _zoomButtonHovered = false;
        _expandedZoomButtonWidth = null;
        UpdateZoomButtonWidth(animate: false);
        _zoomButtonAnimation?.Stop();
        _zoomButtonAnimation = null;

        if (_resizeDebounceTimer is not null)
        {
            _resizeDebounceTimer.Stop();
            _resizeDebounceTimer.Tick -= ResizeDebounceTimer_Tick;
            _resizeDebounceTimer = null;
        }

        if (_parentPage is not null)
        {
            _parentPage.SizeChanged -= ParentPage_SizeChanged;
            _parentPage = null;
        }
    }

    private void ImageContentViewer_DataContextChanged(FrameworkElement sender, DataContextChangedEventArgs e)
    {
        if (IsLoaded)
        {
            ApplyViewModel();
        }
    }

    private void ApplyViewModel()
    {
        ObserveViewModel(ViewModel);
        ApplyMaxDimensions();
        UpdateBorderMaxHeight();
        ApplyImage();
        if (_previewDecode is not null)
        {
            ScheduleResizeUpdate();
        }

        LayoutOverlayButton();
    }

    private void ObserveViewModel(ContentImageViewModel? viewModel)
    {
        if (ReferenceEquals(_observedViewModel, viewModel))
        {
            return;
        }

        if (_observedViewModel is not null)
        {
            _observedViewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _observedViewModel = viewModel;
        if (_observedViewModel is not null)
        {
            _observedViewModel.PropertyChanged += ViewModel_PropertyChanged;
        }
    }

    private void ParentPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ScheduleResizeUpdate();
        LayoutOverlayButton();
    }

    private void ScheduleResizeUpdate()
    {
        if (_resizeDebounceTimer is { } timer)
        {
            timer.Stop();
            timer.Start();
        }
    }

    private void ResizeDebounceTimer_Tick(DispatcherQueueTimer sender, object args)
    {
        UpdateBorderMaxHeight();
        LayoutOverlayButton();

        if (_previewDecode is { } decode && XamlRoot is { } root)
        {
            var bounds = GetPreviewDecodeBounds(root.Size);
            if (decode.NeedsRefresh(bounds, root.RasterizationScale))
            {
                Image.RefreshSourceForSizeChange();
            }
        }
    }

    private void ViewModel_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (!ReferenceEquals(sender, _observedViewModel))
        {
            return;
        }

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
            ScheduleResizeUpdate();
            LayoutOverlayButton();
        }
    }

    private void ApplyImage()
    {
        var sourceKey = ViewModel?.Image;
        if (ReferenceEquals(sourceKey, Image.SourceKey))
        {
            return;
        }

        if (sourceKey is null)
        {
            PreviewLayout.MinHeight = 0;
        }
        else if (Image.Source is not null)
        {
            // Keep the preview height while the replacement image loads.
            PreviewLayout.MinHeight = PreviewLayout.ActualHeight;
        }

        _previewDecode = null;
        Image.SourceKey = sourceKey;
    }

    private void Image_SourceRequestCompleted(object? sender, EventArgs e)
    {
        PreviewLayout.MinHeight = 0;
        if (_previewDecode is { } decode)
        {
            decode.Complete(Image.Source is ImageIconSource { ImageSource: BitmapImage });
            ScheduleResizeUpdate();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (!_availableSize.Equals(availableSize))
        {
            _availableSize = availableSize;
            if (_previewDecode is not null)
            {
                ScheduleResizeUpdate();
            }
        }

        return base.MeasureOverride(availableSize);
    }

    private void Image_SourceRequested(IconBox sender, SourceRequestedEventArgs args)
    {
        if (!FitStreamDecodeToBounds || GetImageStream(args.Key as IconInfoViewModel, args.Theme) is null)
        {
            _previewDecode = null;
            IconProvider.SourceRequestedOriginal(sender, args);
            return;
        }

        var bounds = GetPreviewDecodeBounds(XamlRoot?.Size ?? Size.Empty);
        var decode = new ImagePreviewDecode(bounds);
        _previewDecode = decode;
        if (!bounds.IsEmpty)
        {
            IconProvider.SourceRequestedPreview(args, decode);
        }
    }

    private Size GetPreviewDecodeBounds(Size viewportSize) => ImagePreviewDecode.GetBounds(
        _availableSize,
        new Size(FitViewbox.MaxWidth, ImageBorder.MaxHeight),
        viewportSize);

    private static IRandomAccessStreamReference? GetImageStream(IconInfoViewModel? image, ElementTheme theme)
    {
        var data = image?.IconForTheme(theme == ElementTheme.Light);
        return string.IsNullOrEmpty(data?.Icon) ? data?.Data?.Unsafe : null;
    }

    private void ApplyMaxDimensions()
    {
        FitViewbox.MaxWidth = ViewModel?.MaxWidth ?? double.PositiveInfinity;
        FitViewbox.MaxHeight = ViewModel?.MaxHeight ?? double.PositiveInfinity;
        ImageBorder.MaxWidth = FitViewbox.MaxWidth;
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
            if (FitStreamDecodeToBounds && GetImageStream(ViewModel?.Image, ActualTheme) is { } original)
            {
                var originalBitmap = original as RandomAccessStreamReference ??
                    RandomAccessStreamReference.CreateFromStream(await original.OpenReadAsync());
                var originalPackage = new DataPackage();
                originalPackage.SetBitmap(originalBitmap);
                Clipboard.SetContent(originalPackage);
                SendCopiedImageToast();
                return;
            }

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
        viewer.CancelRequested += (_, __) =>
        {
            popup.IsOpen = false;

            // Move focus back; otherwise it might go to place where sun doesn't shine in after closing the popup
            this.Focus(FocusState.Programmatic);
        };

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

    private void ZoomButton_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Touch)
        {
            _zoomButtonHovered = true;
            UpdateZoomButtonWidth(animate: true);
        }
    }

    private void ZoomButton_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (e.Pointer.PointerDeviceType != Microsoft.UI.Input.PointerDeviceType.Touch)
        {
            _zoomButtonHovered = false;
            UpdateZoomButtonWidth(animate: true);
        }
    }

    private void ZoomButton_FocusChanged(object sender, RoutedEventArgs e) => UpdateZoomButtonWidth(animate: true);

    private void ZoomButtonLabelSlot_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var size = e.NewSize;
        ZoomButtonLabelClip.Rect = new Rect(0, 0, size.Width, size.Height);
        CenterZoomButtonLabel();
    }

    private void ZoomButtonLabel_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        CenterZoomButtonLabel();
        LayoutOverlayButton();
    }

    private void CenterZoomButtonLabel()
    {
        Canvas.SetTop(ZoomButtonLabel, Math.Max(0, (ZoomButtonLabelSlot.ActualHeight - ZoomButtonLabel.ActualHeight) / 2));
    }

    private void Preview_SizeChanged(object sender, SizeChangedEventArgs e) => LayoutOverlayButton();

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

    private void UpdateBorderMaxHeight()
    {
        var height = _parentPage?.ActualHeight ?? XamlRoot?.Size.Height ?? 0;
        var pageLimit = height > 0 ? Math.Max(0, height - MaxHeightSafetyPadding) : double.PositiveInfinity;
        var imageLimit = ViewModel?.MaxHeight ?? double.PositiveInfinity;
        ImageBorder.MaxHeight = Math.Min(pageLimit, imageLimit);
    }

    private void LayoutOverlayButton()
    {
        if (!IsLoaded)
        {
            return;
        }

        const double preferredInset = 8;
        var width = FitViewbox.ActualWidth;
        var height = FitViewbox.ActualHeight;
        var buttonSize = OpenZoomOverlayButton.Height;
        var showButton = width >= buttonSize && height >= buttonSize;
        ZoomButtonOverlay.Width = width;
        ZoomButtonOverlay.Height = height;
        OpenZoomOverlayButton.Visibility = showButton ? Visibility.Visible : Visibility.Collapsed;
        if (!showButton)
        {
            _zoomButtonHovered = false;
        }

        var insetX = Math.Clamp((width - buttonSize) / 2, 0, preferredInset);
        var insetY = Math.Clamp((height - buttonSize) / 2, 0, preferredInset);
        OpenZoomOverlayButton.Margin = new Thickness(insetX, insetY, insetX, 0);

        var labelWidth = ZoomButtonLabel.ActualWidth;
        var expandedWidth = buttonSize + Canvas.GetLeft(ZoomButtonLabel) + labelWidth;
        var border = OpenZoomOverlayButton.BorderThickness;
        var labelFits = showButton && labelWidth > 0
            && expandedWidth + (2 * insetX) <= width
            && ZoomButtonLabel.ActualHeight <= buttonSize - border.Top - border.Bottom;
        _expandedZoomButtonWidth = labelFits ? expandedWidth : null;

        var toolTip = labelFits ? null : _zoomButtonToolTip;
        if (!ReferenceEquals(ToolTipService.GetToolTip(OpenZoomOverlayButton), toolTip))
        {
            ToolTipService.SetToolTip(OpenZoomOverlayButton, toolTip);
        }

        UpdateZoomButtonWidth(animate: false);
    }

    private void UpdateZoomButtonWidth(bool animate)
    {
        var button = OpenZoomOverlayButton;
        var expandedWidth = _zoomButtonHovered || button.FocusState == FocusState.Keyboard
            ? _expandedZoomButtonWidth
            : null;
        var width = expandedWidth ?? button.Height;
        if (width == _targetZoomButtonWidth)
        {
            return;
        }

        _targetZoomButtonWidth = width;
        var previousWidth = button.ActualWidth;
        _zoomButtonAnimation?.Stop();
        _zoomButtonAnimation = null;
        button.Width = width;

        if (!animate || previousWidth <= 0 || !(_uiSettings ??= new UISettings()).AnimationsEnabled)
        {
            return;
        }

        var motion = new DoubleAnimation
        {
            From = previousWidth,
            To = width,
            Duration = TimeSpan.FromMilliseconds(expandedWidth.HasValue ? 140 : 100),
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
            EnableDependentAnimation = true,
        };
        Storyboard.SetTarget(motion, button);
        Storyboard.SetTargetProperty(motion, nameof(Width));

        // The canvas keeps these width changes out of the preview's layout.
        var transition = new Storyboard { FillBehavior = FillBehavior.Stop };
        transition.Children.Add(motion);
        _zoomButtonAnimation = transition;
        transition.Begin();
    }
}
