// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;

using ManagedCommon;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.ViewManagement;

namespace Microsoft.PowerToys.Settings.UI.Controls
{
    public sealed partial class OOBEPageControl : UserControl, IDisposable
    {
        private const string AppxUriPrefix = "ms-appx:///";

        private readonly DispatcherQueue _dispatcherQueue;
        private MediaPlayer _mediaPlayer;
        private MediaSource _mediaSource;
        private string _heroVideoPath;
        private double _videoWidth;
        private double _videoHeight;

        public OOBEPageControl()
        {
            this.InitializeComponent();
            _dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        public string Title
        {
            get { return (string)GetValue(TitleProperty); }
            set { SetValue(TitleProperty, value); }
        }

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public ImageSource HeroImage
        {
            get => (ImageSource)GetValue(HeroImageProperty);
            set => SetValue(HeroImageProperty, value);
        }

        /// <summary>
        /// Gets or sets a looping, muted hero video (for example "ms-appx:///Assets/Settings/Modules/OOBE/Run.mp4").
        /// When set, it is shown instead of <see cref="HeroImage"/>. If the video can't be played (e.g. Windows N/KN
        /// editions without the Media Feature Pack), the PowerToys logo is shown instead.
        /// </summary>
        public string HeroVideo
        {
            get => (string)GetValue(HeroVideoProperty);
            set => SetValue(HeroVideoProperty, value);
        }

        public double HeroImageHeight
        {
            get { return (double)GetValue(HeroImageHeightProperty); }
            set { SetValue(HeroImageHeightProperty, value); }
        }

        public object PageContent
        {
            get { return (object)GetValue(PageContentProperty); }
            set { SetValue(PageContentProperty, value); }
        }

        public static readonly DependencyProperty TitleProperty = DependencyProperty.Register("Title", typeof(string), typeof(OOBEPageControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register("Description", typeof(string), typeof(OOBEPageControl), new PropertyMetadata(default(string)));
        public static readonly DependencyProperty HeroImageProperty = DependencyProperty.Register("HeroImage", typeof(ImageSource), typeof(OOBEPageControl), new PropertyMetadata(default(ImageSource)));
        public static readonly DependencyProperty HeroVideoProperty = DependencyProperty.Register("HeroVideo", typeof(string), typeof(OOBEPageControl), new PropertyMetadata(default(string), OnHeroVideoChanged));
        public static readonly DependencyProperty PageContentProperty = DependencyProperty.Register("PageContent", typeof(object), typeof(OOBEPageControl), new PropertyMetadata(new Grid()));
        public static readonly DependencyProperty HeroImageHeightProperty = DependencyProperty.Register("HeroImageHeight", typeof(double), typeof(OOBEPageControl), new PropertyMetadata(280.0));

        public void Dispose()
        {
            StopHeroVideo();
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Resolves a hero video reference to an absolute file path. Settings is unpackaged, so Media Foundation
        /// can't resolve ms-appx:/// URIs; map them onto the application directory instead.
        /// </summary>
        internal static string ResolveHeroVideoPath(string source, string baseDirectory)
        {
            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrEmpty(baseDirectory))
            {
                return null;
            }

            string relativePath = source.StartsWith(AppxUriPrefix, StringComparison.OrdinalIgnoreCase)
                ? Uri.UnescapeDataString(source.AsSpan(AppxUriPrefix.Length))
                : source;

            if (Path.IsPathRooted(relativePath))
            {
                return Path.GetFullPath(relativePath);
            }

            return Path.GetFullPath(Path.Combine(baseDirectory, relativePath.Replace('/', Path.DirectorySeparatorChar)));
        }

        /// <summary>
        /// Computes where to place the video so it fills the host and is cropped evenly on both sides, matching
        /// Image Stretch="UniformToFill". MediaPlayerElement's own UniformToFill anchors the crop to the top-left.
        /// </summary>
        internal static Rect ComputeCenteredFillBounds(double hostWidth, double hostHeight, double videoWidth, double videoHeight)
        {
            if (videoWidth <= 0 || videoHeight <= 0)
            {
                return new Rect(0, 0, hostWidth, hostHeight);
            }

            double scale = Math.Max(hostWidth / videoWidth, hostHeight / videoHeight);
            double width = videoWidth * scale;
            double height = videoHeight * scale;
            return new Rect((hostWidth - width) / 2, (hostHeight - height) / 2, width, height);
        }

        private static void OnHeroVideoChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is OOBEPageControl control && control.IsLoaded)
            {
                control.StartHeroVideo();
            }
        }

        private static bool AreAnimationsEnabled()
        {
            try
            {
                return new UISettings().AnimationsEnabled;
            }
            catch (Exception)
            {
                return true;
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e) => StartHeroVideo();

        private void OnUnloaded(object sender, RoutedEventArgs e) => StopHeroVideo();

        private void StartHeroVideo()
        {
            StopHeroVideo();

            if (string.IsNullOrEmpty(HeroVideo))
            {
                HeaderVideoHost.Visibility = Visibility.Collapsed;
                HeaderFallback.Visibility = Visibility.Collapsed;
                HeaderImage.Visibility = Visibility.Visible;
                return;
            }

            HeaderImage.Visibility = Visibility.Collapsed;

            string path = ResolveHeroVideoPath(HeroVideo, AppContext.BaseDirectory);
            if (path == null || !File.Exists(path))
            {
                Logger.LogWarning($"OOBE hero video not found: {HeroVideo}");
                ShowFallback();
                return;
            }

            _heroVideoPath = path;

            try
            {
                bool animationsEnabled = AreAnimationsEnabled();
                _mediaSource = MediaSource.CreateFromUri(new Uri(path));
                _mediaPlayer = new MediaPlayer
                {
                    IsLoopingEnabled = true,
                    IsMuted = true,
                    AutoPlay = animationsEnabled,
                };

                // Keep the hero video out of the system media controls (volume flyout / lock screen).
                _mediaPlayer.CommandManager.IsEnabled = false;
                _mediaPlayer.MediaFailed += OnMediaFailed;
                _mediaPlayer.MediaOpened += OnMediaOpened;
                _mediaPlayer.Source = _mediaSource;

                HeaderVideo.SetMediaPlayer(_mediaPlayer);
                HeaderFallback.Visibility = Visibility.Collapsed;
                HeaderVideoHost.Visibility = Visibility.Visible;
                LayoutHeroVideo();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to start OOBE hero video: {HeroVideo}", ex);
                StopHeroVideo();
                ShowFallback();
            }
        }

        private void StopHeroVideo()
        {
            if (_mediaPlayer != null)
            {
                _mediaPlayer.MediaFailed -= OnMediaFailed;
                _mediaPlayer.MediaOpened -= OnMediaOpened;
                HeaderVideo.SetMediaPlayer(null);
                _mediaPlayer.Dispose();
                _mediaPlayer = null;
            }

            _mediaSource?.Dispose();
            _mediaSource = null;
            _videoWidth = 0;
            _videoHeight = 0;
        }

        private void HeaderVideoHost_SizeChanged(object sender, SizeChangedEventArgs e) => LayoutHeroVideo();

        private void LayoutHeroVideo()
        {
            double hostWidth = HeaderVideoHost.ActualWidth;
            double hostHeight = HeaderVideoHost.ActualHeight;
            if (hostWidth <= 0 || hostHeight <= 0)
            {
                return;
            }

            HeaderVideoHost.Clip = new RectangleGeometry { Rect = new Rect(0, 0, hostWidth, hostHeight) };

            Rect bounds = ComputeCenteredFillBounds(hostWidth, hostHeight, _videoWidth, _videoHeight);
            HeaderVideo.Width = bounds.Width;
            HeaderVideo.Height = bounds.Height;
            Canvas.SetLeft(HeaderVideo, bounds.X);
            Canvas.SetTop(HeaderVideo, bounds.Y);
        }

        private void ShowFallback()
        {
            HeaderImage.Visibility = Visibility.Collapsed;
            HeaderVideoHost.Visibility = Visibility.Collapsed;
            HeaderFallback.Visibility = Visibility.Visible;
        }

        // MediaOpened and MediaFailed are raised on a background thread.
        private void OnMediaOpened(MediaPlayer sender, object args)
        {
            uint width = sender.PlaybackSession.NaturalVideoWidth;
            uint height = sender.PlaybackSession.NaturalVideoHeight;

            _dispatcherQueue?.TryEnqueue(() =>
            {
                if (ReferenceEquals(sender, _mediaPlayer))
                {
                    _videoWidth = width;
                    _videoHeight = height;
                    LayoutHeroVideo();
                }
            });
        }

        private void OnMediaFailed(MediaPlayer sender, MediaPlayerFailedEventArgs args)
        {
            Logger.LogWarning($"OOBE hero video failed to play ({args.Error}, 0x{args.ExtendedErrorCode?.HResult:X8}): {_heroVideoPath} {args.ErrorMessage}");

            _dispatcherQueue?.TryEnqueue(() =>
            {
                if (ReferenceEquals(sender, _mediaPlayer))
                {
                    StopHeroVideo();
                    ShowFallback();
                }
            });
        }
    }
}
