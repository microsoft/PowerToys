// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Numerics;
using ManagedCommon;
using Microsoft.Graphics.Canvas.Effects;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace Microsoft.CmdPal.UI.Controls;

internal sealed partial class BlurImageControl : Control
{
    private const string ImageSourceParameterName = "ImageSource";

    private const string BrightnessEffectName = "Brightness";
    private const string BrightnessOverlayEffectName = "BrightnessOverlay";
    private const string BlurEffectName = "Blur";
    private const string TintBlendEffectName = "TintBlend";
    private const string TintEffectName = "Tint";

#pragma warning disable CA1507 // Use nameof to express symbol names ... some of these refer to effect properties that are separate from the class properties
    private static readonly string BrightnessSource1AmountEffectProperty = GetPropertyName(BrightnessEffectName, "Source1Amount");
    private static readonly string BrightnessSource2AmountEffectProperty = GetPropertyName(BrightnessEffectName, "Source2Amount");
    private static readonly string BrightnessOverlayColorEffectProperty = GetPropertyName(BrightnessOverlayEffectName, "Color");
    private static readonly string BlurBlurAmountEffectProperty = GetPropertyName(BlurEffectName, "BlurAmount");
    private static readonly string TintColorEffectProperty = GetPropertyName(TintEffectName, "Color");
#pragma warning restore CA1507

    private static readonly string[] AnimatableProperties = [
        BrightnessSource1AmountEffectProperty,
        BrightnessSource2AmountEffectProperty,
        BrightnessOverlayColorEffectProperty,
        BlurBlurAmountEffectProperty,
        TintColorEffectProperty
    ];

    public static readonly DependencyProperty ImageSourceProperty =
        DependencyProperty.Register(
            nameof(ImageSource),
            typeof(ImageSource),
            typeof(BlurImageControl),
            new PropertyMetadata(null, OnImageChanged));

    public static readonly DependencyProperty ImageStretchProperty =
        DependencyProperty.Register(
            nameof(ImageStretch),
            typeof(Stretch),
            typeof(BlurImageControl),
            new PropertyMetadata(Stretch.UniformToFill, OnImageStretchChanged));

    public static readonly DependencyProperty ImageOpacityProperty =
        DependencyProperty.Register(
            nameof(ImageOpacity),
            typeof(double),
            typeof(BlurImageControl),
            new PropertyMetadata(1.0, OnOpacityChanged));

    public static readonly DependencyProperty ImageBrightnessProperty =
        DependencyProperty.Register(
            nameof(ImageBrightness),
            typeof(double),
            typeof(BlurImageControl),
            new PropertyMetadata(1.0, OnBrightnessChanged));

    public static readonly DependencyProperty BlurAmountProperty =
        DependencyProperty.Register(
            nameof(BlurAmount),
            typeof(double),
            typeof(BlurImageControl),
            new PropertyMetadata(0.0, OnBlurAmountChanged));

    public static readonly DependencyProperty TintColorProperty =
        DependencyProperty.Register(
            nameof(TintColor),
            typeof(Color),
            typeof(BlurImageControl),
            new PropertyMetadata(Colors.Transparent, OnVisualPropertyChanged));

    public static readonly DependencyProperty TintIntensityProperty =
        DependencyProperty.Register(
            nameof(TintIntensity),
            typeof(double),
            typeof(BlurImageControl),
            new PropertyMetadata(0.0, OnVisualPropertyChanged));

    private Compositor? _compositor;
    private SpriteVisual? _effectVisual;
    private CompositionEffectBrush? _effectBrush;
    private CompositionSurfaceBrush? _imageBrush;
    private CompositionBackdropBrush? _backdropBrush;
    private LoadedImageSurface? _lastLoadedSurface;
    private IRandomAccessStream? _lastLoadedStream;
    private Uri? _lastLoadedUri;
    private DateTime? _lastLoadedFileWriteTime;
    private bool _lastLoadedFromUri;
    private Uri? _loadingUri;
    private long _imageLoadVersion;

    public BlurImageControl()
    {
        this.DefaultStyleKey = typeof(BlurImageControl);
        this.Loaded += OnLoaded;
        this.Unloaded += OnUnloaded;
        this.SizeChanged += OnSizeChanged;
    }

    public ImageSource ImageSource
    {
        get => (ImageSource)GetValue(ImageSourceProperty);
        set => SetValue(ImageSourceProperty, value);
    }

    public Stretch ImageStretch
    {
        get => (Stretch)GetValue(ImageStretchProperty);
        set => SetValue(ImageStretchProperty, value);
    }

    public double ImageOpacity
    {
        get => (double)GetValue(ImageOpacityProperty);
        set => SetValue(ImageOpacityProperty, value);
    }

    public double ImageBrightness
    {
        get => (double)GetValue(ImageBrightnessProperty);
        set => SetValue(ImageBrightnessProperty, Math.Clamp(value, -1, 1));
    }

    public double BlurAmount
    {
        get => (double)GetValue(BlurAmountProperty);
        set => SetValue(BlurAmountProperty, value);
    }

    public Color TintColor
    {
        get => (Color)GetValue(TintColorProperty);
        set => SetValue(TintColorProperty, value);
    }

    public double TintIntensity
    {
        get => (double)GetValue(TintIntensityProperty);
        set => SetValue(TintIntensityProperty, value);
    }

    private static void OnImageStretchChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BlurImageControl control && control._imageBrush != null)
        {
            control._imageBrush.Stretch = ConvertStretch((Stretch)e.NewValue);
        }
    }

    private static void OnVisualPropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BlurImageControl control && control._compositor != null)
        {
            control.UpdateEffect();
        }
    }

    private static void OnOpacityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BlurImageControl control && control._effectVisual != null)
        {
            control._effectVisual.Opacity = (float)(double)e.NewValue;
        }
    }

    private static void OnBlurAmountChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BlurImageControl control && control._effectBrush != null)
        {
            control.UpdateEffect();
        }
    }

    private static void OnBrightnessChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is BlurImageControl control && control._effectBrush != null)
        {
            control.UpdateEffect();
        }
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (_compositor is null)
        {
            InitializeComposition();
        }
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (IsLoaded)
        {
            return;
        }

        ClearImageSurface();
        var effectVisual = _effectVisual;
        var effectBrush = _effectBrush;
        var imageBrush = _imageBrush;
        var backdropBrush = _backdropBrush;
        _compositor = null;
        _effectVisual = null;
        _effectBrush = null;
        _imageBrush = null;
        _backdropBrush = null;

        try
        {
            ElementCompositionPreview.SetElementChildVisual(this, null);
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to detach visual for BlurImageControl", ex);
        }
        finally
        {
            DisposeResource(effectVisual);
            DisposeResource(effectBrush);
            DisposeResource(imageBrush);
            DisposeResource(backdropBrush);
        }
    }

    private void OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (_effectVisual != null)
        {
            _effectVisual.Size = new Vector2(
                (float)Math.Max(1, e.NewSize.Width),
                (float)Math.Max(1, e.NewSize.Height));
        }
    }

    private static void OnImageChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not BlurImageControl control)
        {
            return;
        }

        control.UpdateImageSource();
    }

    private void InitializeComposition()
    {
        var visual = ElementCompositionPreview.GetElementVisual(this);
        _compositor = visual.Compositor;

        _effectVisual = _compositor.CreateSpriteVisual();
        _effectVisual.Size = new Vector2(
            (float)Math.Max(1, ActualWidth),
            (float)Math.Max(1, ActualHeight));
        _effectVisual.Opacity = (float)ImageOpacity;

        ElementCompositionPreview.SetElementChildVisual(this, _effectVisual);

        UpdateEffect();
    }

    private void EnsureEffect()
    {
        if (_compositor is null)
        {
            return;
        }

        if (_effectBrush is not null)
        {
            return;
        }

        var imageSource = new CompositionEffectSourceParameter(ImageSourceParameterName);

        // 1) Brightness via ArithmeticCompositeEffect
        // We blend between the original image and either black or white,
        // depending on whether we want to darken or brighten. BrightnessEffect isn't supported
        // in the composition graph.
        var brightnessEffect = new ArithmeticCompositeEffect
        {
            Name = BrightnessEffectName,
            Source1 = imageSource, // original image
            Source2 = new ColorSourceEffect
            {
                Name = BrightnessOverlayEffectName,
                Color = Colors.Black, // we'll swap black/white via properties
            },

            MultiplyAmount = 0.0f,
            Source1Amount = 1.0f, // original
            Source2Amount = 0.0f, // overlay
            Offset = 0.0f,
        };

        // 2) Blur
        var blurEffect = new GaussianBlurEffect
        {
            Name = BlurEffectName,
            BlurAmount = 0.0f,
            BorderMode = EffectBorderMode.Hard,
            Optimization = EffectOptimization.Balanced,
            Source = brightnessEffect,
        };

        // 3) Tint (always in the chain; intensity via alpha)
        var tintEffect = new BlendEffect
        {
            Name = TintBlendEffectName,
            Background = blurEffect,
            Foreground = new ColorSourceEffect
            {
                Name = TintEffectName,
                Color = Colors.Transparent,
            },
            Mode = BlendEffectMode.Multiply,
        };

        var effectFactory = _compositor.CreateEffectFactory(tintEffect, AnimatableProperties);

        _effectBrush = effectFactory.CreateBrush();
        UpdateImageSource();

        if (_effectVisual is not null)
        {
            _effectVisual.Brush = _effectBrush;
        }
    }

    private void UpdateImageSource()
    {
        if (_compositor is null || _effectBrush is null)
        {
            return;
        }

        if (ImageSource is BitmapImage { UriSource: not null } bitmapImage)
        {
            _imageBrush ??= _compositor.CreateSurfaceBrush();
            _effectBrush.SetSourceParameter(ImageSourceParameterName, _imageBrush);
            _ = LoadImageAsync(bitmapImage);
        }
        else
        {
            ClearImageSurface();
            _backdropBrush ??= _compositor.CreateBackdropBrush();
            _effectBrush.SetSourceParameter(ImageSourceParameterName, _backdropBrush);
        }
    }

    private void UpdateEffect()
    {
        if (_compositor is null)
        {
            return;
        }

        EnsureEffect();
        if (_effectBrush is null)
        {
            return;
        }

        var props = _effectBrush.Properties;

        // Brightness
        var b = (float)Math.Clamp(ImageBrightness, -1.0, 1.0);

        float source1Amount;
        float source2Amount;
        Color overlayColor;

        if (b >= 0)
        {
            // Brighten: blend towards white
            overlayColor = Colors.White;
            source1Amount = 1.0f - b; // original image contribution
            source2Amount = b;        // white overlay contribution
        }
        else
        {
            // Darken: blend towards black
            overlayColor = Colors.Black;
            var t = -b;               // 0..1
            source1Amount = 1.0f - t; // original image
            source2Amount = t;        // black overlay
        }

        props.InsertScalar(BrightnessSource1AmountEffectProperty, source1Amount);
        props.InsertScalar(BrightnessSource2AmountEffectProperty, source2Amount);
        props.InsertColor(BrightnessOverlayColorEffectProperty, overlayColor);

        // Blur
        props.InsertScalar(BlurBlurAmountEffectProperty, (float)BlurAmount);

        // Tint
        var tintColor = TintColor;
        var clampedIntensity = (float)Math.Clamp(TintIntensity, 0.0, 1.0);

        var adjustedColor = Color.FromArgb(
            (byte)(clampedIntensity * 255),
            tintColor.R,
            tintColor.G,
            tintColor.B);

        props.InsertColor(TintColorEffectProperty, adjustedColor);
    }

    private async Task LoadImageAsync(BitmapImage bitmapImage)
    {
        var uri = bitmapImage.UriSource;
        if (uri.Equals(_loadingUri))
        {
            return;
        }

        var version = ++_imageLoadVersion;
        _loadingUri = uri;
        LoadedImageSurface? loadedSurface = null;
        IRandomAccessStream? loadedStream = null;
        try
        {
            var lastWriteTime = uri.IsAbsoluteUri && uri.IsFile
                ? await Task.Run(() => GetImageLastWriteTime(uri))
                : null;
            if (version != _imageLoadVersion || _imageBrush is null || CanReuseImage(uri, lastWriteTime))
            {
                return;
            }

            Logger.LogDebug($"Starting load of BlurImageControl from '{uri}'");
            if (lastWriteTime is not null)
            {
                try
                {
                    // Snapshot the file to bypass URI caching and release the file before decoding.
                    loadedStream = await Task.Run(() => ReadImageFileAsync(uri));
                }
                catch (Exception ex)
                {
                    if (version == _imageLoadVersion)
                    {
                        if (_lastLoadedFromUri && CanReuseImage(uri, lastWriteTime, allowUriFallback: true))
                        {
                            return;
                        }

                        Logger.LogWarning($"Failed to read image file for BlurImageControl; trying URI loading: {ex.Message}");
                    }
                }
            }

            if (version != _imageLoadVersion || _imageBrush is null)
            {
                return;
            }

            var previousSurface = _lastLoadedSurface;
            var previousStream = _lastLoadedStream;

            var loadedFromUri = loadedStream is null;
            loadedSurface = loadedStream is not null
                ? LoadedImageSurface.StartLoadFromStream(loadedStream)
                : LoadedImageSurface.StartLoadFromUri(uri);
            loadedSurface.LoadCompleted += OnLoadedSurfaceOnLoadCompleted;
            SetLoadedSurfaceToBrush(loadedSurface);
            _lastLoadedSurface = loadedSurface;
            _lastLoadedStream = loadedStream;
            _lastLoadedUri = uri;
            _lastLoadedFromUri = loadedFromUri;

            // Track the attempted file version; URI fallback still needs to retry the file read.
            _lastLoadedFileWriteTime = lastWriteTime;
            loadedSurface = null;
            loadedStream = null;

            // Release the previous native resources after binding the replacement surface.
            DisposeLoadedSurface(previousSurface);
            DisposeResource(previousStream);
        }
        catch (Exception ex)
        {
            if (version == _imageLoadVersion)
            {
                ClearImageSurface();
                Logger.LogError("Failed to load image for BlurImageControl", ex);
            }
        }
        finally
        {
            DisposeLoadedSurface(loadedSurface);
            DisposeResource(loadedStream);
            if (version == _imageLoadVersion)
            {
                _loadingUri = null;
            }
        }
    }

    private void OnLoadedSurfaceOnLoadCompleted(LoadedImageSurface loadedSurface, LoadedImageSourceLoadCompletedEventArgs e)
    {
        if (!ReferenceEquals(loadedSurface, _lastLoadedSurface))
        {
            return;
        }

        // The surface is already bound to the brush and updates in place as it loads.
        switch (e.Status)
        {
            case LoadedImageSourceLoadStatus.Success:
                Logger.LogDebug("BlurImageControl loaded successfully");
                break;
            case LoadedImageSourceLoadStatus.NetworkError:
            case LoadedImageSourceLoadStatus.InvalidFormat:
            case LoadedImageSourceLoadStatus.Other:
            default:
                _lastLoadedUri = null;
                _lastLoadedFileWriteTime = null;
                Logger.LogError($"Failed to load image for BlurImageControl: Load status {e.Status}");
                break;
        }
    }

    private void SetLoadedSurfaceToBrush(LoadedImageSurface loadedSurface)
    {
        var surfaceBrush = _imageBrush;
        if (surfaceBrush is null)
        {
            return;
        }

        surfaceBrush.Stretch = ConvertStretch(ImageStretch);
        surfaceBrush.BitmapInterpolationMode = CompositionBitmapInterpolationMode.Linear;
        surfaceBrush.Surface = loadedSurface;
    }

    private void ClearImageSurface()
    {
        var loadedSurface = _lastLoadedSurface;
        var loadedStream = _lastLoadedStream;
        _lastLoadedSurface = null;
        _lastLoadedStream = null;
        _lastLoadedUri = null;
        _lastLoadedFileWriteTime = null;
        _lastLoadedFromUri = false;
        _loadingUri = null;
        _imageLoadVersion++;

        try
        {
            if (_imageBrush is not null)
            {
                _imageBrush.Surface = null;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to clear image for BlurImageControl", ex);
        }
        finally
        {
            DisposeLoadedSurface(loadedSurface);
            DisposeResource(loadedStream);
        }
    }

    private void DisposeLoadedSurface(LoadedImageSurface? loadedSurface)
    {
        try
        {
            if (loadedSurface is not null)
            {
                loadedSurface.LoadCompleted -= OnLoadedSurfaceOnLoadCompleted;
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to detach image load handler for BlurImageControl", ex);
        }
        finally
        {
            DisposeResource(loadedSurface);
        }
    }

    private static void DisposeResource(IDisposable? resource)
    {
        try
        {
            resource?.Dispose();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to dispose resource for BlurImageControl", ex);
        }
    }

    private static DateTime? GetImageLastWriteTime(Uri uri)
    {
        try
        {
            var file = new FileInfo(uri.LocalPath);
            return file.Exists ? file.LastWriteTimeUtc : null;
        }
        catch (Exception)
        {
            // File metadata is only a cache hint; loading reports image errors.
            return null;
        }
    }

    private bool CanReuseImage(Uri uri, DateTime? lastWriteTime, bool allowUriFallback = false)
    {
        return _lastLoadedSurface is not null
            && uri.Equals(_lastLoadedUri)
            && (lastWriteTime is null
                || (lastWriteTime == _lastLoadedFileWriteTime && (allowUriFallback || !_lastLoadedFromUri)));
    }

    private static async Task<IRandomAccessStream> ReadImageFileAsync(Uri uri)
    {
        var file = await StorageFile.GetFileFromPathAsync(uri.LocalPath);
        using var fileStream = await file.OpenAsync(FileAccessMode.Read, StorageOpenOptions.AllowReadersAndWriters);
        var memory = new InMemoryRandomAccessStream();
        try
        {
            await RandomAccessStream.CopyAsync(fileStream, memory);
            memory.Seek(0);
            return memory;
        }
        catch
        {
            DisposeResource(memory);
            throw;
        }
    }

    private static CompositionStretch ConvertStretch(Stretch stretch)
    {
        return stretch switch
        {
            Stretch.None => CompositionStretch.None,
            Stretch.Fill => CompositionStretch.Fill,
            Stretch.Uniform => CompositionStretch.Uniform,
            Stretch.UniformToFill => CompositionStretch.UniformToFill,
            _ => CompositionStretch.UniformToFill,
        };
    }

    private static string GetPropertyName(string effectName, string propertyName) => $"{effectName}.{propertyName}";
}
