// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using CommunityToolkit.Mvvm.ComponentModel;
using ManagedCommon;
using Microsoft.PowerToys.Telemetry;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using Peek.Common.Extensions;
using Peek.Common.Helpers;
using Peek.Common.Models;
using Peek.FilePreviewer.Models;
using Peek.FilePreviewer.Previewers;
using Peek.FilePreviewer.Previewers.Interfaces;
using Peek.UI.Telemetry.Events;

namespace Peek.FilePreviewer
{
    [INotifyPropertyChanged]
    public sealed partial class FilePreview : UserControl, IDisposable
    {
        public const int LoadingDelayBeforeProgressSpinnerShownMs = 200;

        private readonly PreviewerFactory previewerFactory = new();

        private readonly DispatcherTimer _loadingProgressTimer = new()
        {
            Interval = TimeSpan.FromMilliseconds(LoadingDelayBeforeProgressSpinnerShownMs),
        };

        public event EventHandler<PreviewSizeChangedArgs>? PreviewSizeChanged;

        public static readonly DependencyProperty ItemProperty =
        DependencyProperty.Register(
            nameof(Item),
            typeof(IFileSystemItem),
            typeof(FilePreview),
            new PropertyMetadata(false, async (d, e) => await ((FilePreview)d).OnItemPropertyChanged()));

        public static readonly DependencyProperty ScalingFactorProperty =
            DependencyProperty.Register(
                nameof(ScalingFactor),
                typeof(double),
                typeof(FilePreview),
                new PropertyMetadata(false, async (d, e) => await ((FilePreview)d).OnScalingFactorPropertyChanged()));

        public static readonly DependencyProperty ShowFilePreviewTooltipProperty =
            DependencyProperty.Register(
                nameof(ShowFilePreviewTooltip),
                typeof(bool),
                typeof(FilePreview),
                new PropertyMetadata(true, (d, e) => ((FilePreview)d).OnShowFilePreviewTooltipChanged()));

        [ObservableProperty]
        private int numberOfFiles;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(ImagePreviewer))]
        [NotifyPropertyChangedFor(nameof(VideoPreviewer))]
        [NotifyPropertyChangedFor(nameof(AudioPreviewer))]
        [NotifyPropertyChangedFor(nameof(BrowserPreviewer))]
        [NotifyPropertyChangedFor(nameof(ArchivePreviewer))]
        [NotifyPropertyChangedFor(nameof(ShellPreviewHandlerPreviewer))]
        [NotifyPropertyChangedFor(nameof(DrivePreviewer))]
        [NotifyPropertyChangedFor(nameof(SpecialFolderPreviewer))]
        [NotifyPropertyChangedFor(nameof(UnsupportedFilePreviewer))]
        private IPreviewer? previewer;

        [ObservableProperty]
        private string? infoTooltip = ResourceLoaderInstance.ResourceLoader.GetString("PreviewTooltip_Blank");

        [ObservableProperty]
        private string noMoreFilesText = ResourceLoaderInstance.ResourceLoader.GetString("NoMoreFiles");

        [ObservableProperty]
        private bool isLoadingIndicatorVisible;

        private CancellationTokenSource _cancellationTokenSource = new();

        public FilePreview()
        {
            InitializeComponent();
            _loadingProgressTimer.Tick += LoadingProgressTimer_Tick;
        }

        public void Dispose()
        {
            _loadingProgressTimer.Tick -= LoadingProgressTimer_Tick;
            _loadingProgressTimer.Stop();
            _cancellationTokenSource.Dispose();
        }

        private void LoadingProgressTimer_Tick(object? sender, object e)
        {
            _loadingProgressTimer.Stop();

            if (Previewer?.State == PreviewState.Loading)
            {
                IsLoadingIndicatorVisible = true;
            }
        }

        private void StartLoadingProgressTimer()
        {
            // Do not reset the timer if it's already running or if the indicator is already visible.
            // This prevents rapid key repeats from indefinitely delaying the spinner.
            if (!_loadingProgressTimer.IsEnabled && !IsLoadingIndicatorVisible)
            {
                _loadingProgressTimer.Start();
            }
        }

        private void StopLoadingProgressTimer()
        {
            _loadingProgressTimer.Stop();
            IsLoadingIndicatorVisible = false;
        }

        private async void Previewer_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(IPreviewer.State))
            {
                if (Previewer?.State == PreviewState.Loading)
                {
                    StartLoadingProgressTimer();
                }
                else
                {
                    StopLoadingProgressTimer();
                }

                // Fallback on DefaultPreviewer if we fail to load the correct Preview
                if (Previewer?.State == PreviewState.Error)
                {
                    // Cancel previous loading task
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource = new();

                    if (Previewer is not IUnsupportedFilePreviewer)
                    {
                        Previewer = PreviewerFactory.CreateDefaultPreviewer(Item);
                        await UpdatePreviewAsync(_cancellationTokenSource.Token);
                    }
                }
            }
        }

        public IImagePreviewer? ImagePreviewer => Previewer as IImagePreviewer;

        public IVideoPreviewer? VideoPreviewer => Previewer as IVideoPreviewer;

        public IAudioPreviewer? AudioPreviewer => Previewer as IAudioPreviewer;

        public IBrowserPreviewer? BrowserPreviewer => Previewer as IBrowserPreviewer;

        public IArchivePreviewer? ArchivePreviewer => Previewer as IArchivePreviewer;

        public IShellPreviewHandlerPreviewer? ShellPreviewHandlerPreviewer => Previewer as IShellPreviewHandlerPreviewer;

        public IDrivePreviewer? DrivePreviewer => Previewer as IDrivePreviewer;

        public ISpecialFolderPreviewer? SpecialFolderPreviewer => Previewer as ISpecialFolderPreviewer;

        public IUnsupportedFilePreviewer? UnsupportedFilePreviewer => Previewer as IUnsupportedFilePreviewer;

        public IFileSystemItem Item
        {
            get => (IFileSystemItem)GetValue(ItemProperty);
            set => SetValue(ItemProperty, value);
        }

        public double ScalingFactor
        {
            get => (double)GetValue(ScalingFactorProperty);
            set
            {
                SetValue(ScalingFactorProperty, value);

                if (Previewer is IImagePreviewer imagePreviewer)
                {
                    imagePreviewer.ScalingFactor = ScalingFactor;
                }
            }
        }

        public bool ShowFilePreviewTooltip
        {
            get => (bool)GetValue(ShowFilePreviewTooltipProperty);
            set => SetValue(ShowFilePreviewTooltipProperty, value);
        }

        private void OnShowFilePreviewTooltipChanged()
        {
            if (!ShowFilePreviewTooltip)
            {
                InfoTooltip = null;
            }
            else if (Item != null)
            {
                _ = SafeUpdateTooltipAsync();
            }
        }

        private async Task SafeUpdateTooltipAsync()
        {
            try
            {
                await UpdateTooltipAsync(_cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected during navigation
            }
            catch (Exception ex)
            {
                Logger.LogError("Tooltip update failed: " + ex.Message);
            }
        }

        public bool MatchPreviewState(PreviewState? value, PreviewState stateToMatch)
        {
            return value == stateToMatch;
        }

        public string GetPreviewStateText(PreviewState? state)
        {
            return (state ?? PreviewState.Uninitialized).ToString();
        }

        public Visibility IsPreviewVisible(IPreviewer? previewer, PreviewState? state)
        {
            if (previewer is null)
            {
                return Visibility.Collapsed;
            }

            if (MatchPreviewState(state, PreviewState.Loaded))
            {
                return Visibility.Visible;
            }

            // Keep image preview visible while the next image is loading so the previous frame
            // remains on screen until we can swap.
            if (previewer is IImagePreviewer imagePreviewer && MatchPreviewState(state, PreviewState.Loading) && imagePreviewer.Preview is not null)
            {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public double GetImagePreviewOpacity(bool isLoadingIndicatorVisible)
        {
            // Dim front-buffer image when the loading progress indicator is shown
            return isLoadingIndicatorVisible ? 0.4 : 1.0;
        }

        public Visibility IsWarningMessageVisible(IPreviewer? previewer, PreviewState? state)
        {
            var shouldShow = previewer is IVideoPreviewer videoPreviewer && MatchPreviewState(state, PreviewState.Loaded) && !string.IsNullOrEmpty(videoPreviewer.MissingCodecName);

            return shouldShow ? Visibility.Visible : Visibility.Collapsed;
        }

        public string GetWarningMessage(string missingCodecName)
        {
            return ReadableStringHelper.FormatResourceString("VideoMissingCodec_WarningMessage", missingCodecName);
        }

        private async void CodecSearchHyperlink_Click(Hyperlink sender, HyperlinkClickEventArgs args)
        {
            string codecName = VideoPreviewer?.MissingCodecName ?? string.Empty;

            string searchQuery = Uri.EscapeDataString(codecName);
            Uri storeSearchUri = new Uri($"ms-windows-store://search/?query=codec {codecName}");

            await Windows.System.Launcher.LaunchUriAsync(storeSearchUri);
        }

        public Visibility IsUnsupportedPreviewVisible(IUnsupportedFilePreviewer? previewer, PreviewState state)
        {
            var isValidPreview = previewer != null && (MatchPreviewState(state, PreviewState.Loaded) || MatchPreviewState(state, PreviewState.Error));
            return isValidPreview ? Visibility.Visible : Visibility.Collapsed;
        }

        private void ClearAllPreviews()
        {
            (Previewer as IDisposable)?.Dispose();
            Previewer = null;
            HideAllPreviewControls();
        }

        private void HideAllPreviewControls()
        {
            ImagePreview.Visibility = Visibility.Collapsed;
            VideoPreview.Visibility = Visibility.Collapsed;
            AudioPreview.Visibility = Visibility.Collapsed;
            BrowserPreview.Visibility = Visibility.Collapsed;
            ArchivePreview.Visibility = Visibility.Collapsed;
            DrivePreview.Visibility = Visibility.Collapsed;
            UnsupportedFilePreview.Visibility = Visibility.Collapsed;
        }

        private void UpdatePreviewerItem()
        {
            if (Previewer is IReusablePreviewer reusablePreviewer)
            {
                reusablePreviewer.Rebind(Item, ScalingFactor);
            }
        }

        private async Task OnItemPropertyChanged()
        {
            // Cancel previous loading task
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new();

            // Ensure the loading timer is running so rapid skimming eventually shows the spinner.
            StartLoadingProgressTimer();

            NoMoreFiles.Visibility = NumberOfFiles == 0 ? Visibility.Visible : Visibility.Collapsed;

            if (Item is null)
            {
                ClearAllPreviews();
                return;
            }

            var neededType = previewerFactory.GetCompatiblePreviewerType(Item);

            // Reuse the existing previewer when the type matches and supports in-place item updates,
            // avoiding control teardown which would cause a visible white flash between images.
            bool canReuse = Previewer is IReusablePreviewer && Previewer.GetType() == neededType;

            if (!canReuse)
            {
                // Clear up any unmanaged resources before creating a new previewer instance.
                (Previewer as IDisposable)?.Dispose();
                Previewer = previewerFactory.Create(Item);
            }

            UpdatePreviewerItem();
            await UpdatePreviewAsync(_cancellationTokenSource.Token);
        }

        private async Task OnScalingFactorPropertyChanged()
        {
            await UpdatePreviewSizeAsync(_cancellationTokenSource.Token);
        }

        private async Task UpdatePreviewSizeAsync(CancellationToken cancellationToken)
        {
            if (Previewer != null)
            {
                var previewSize = await Previewer.GetPreviewSizeAsync(cancellationToken);
                PreviewSizeChanged?.Invoke(this, new PreviewSizeChangedArgs(previewSize));
            }
        }

        private async Task UpdateImagePreviewAsync(CancellationToken cancellationToken)
        {
            if (Previewer is IImagePreviewer imagePreviewer)
            {
                var previewSize = await Previewer.GetPreviewSizeAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                await Previewer.LoadPreviewAsync(cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();

                // Apply resize and image swap atomically on the UI thread once the image is ready.
                PreviewSizeChanged?.Invoke(this, new PreviewSizeChangedArgs(previewSize));
                ImagePreview.InstantSwap();
            }
        }

        private async Task UpdatePreviewAsync(CancellationToken cancellationToken)
        {
            if (Previewer is null)
            {
                return;
            }

            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (Previewer is IImagePreviewer)
                {
                    await UpdateImagePreviewAsync(cancellationToken);
                }
                else
                {
                    await UpdatePreviewSizeAsync(cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    await Previewer.LoadPreviewAsync(cancellationToken);
                }

                cancellationToken.ThrowIfCancellationRequested();
                await UpdateTooltipAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected during navigation.
            }
            catch (Exception ex)
            {
                // Fall back to Default previewer.
                PowerToysTelemetry.Log.WriteEvent(new ErrorEvent() { HResult = (Common.Models.HResult)ex.HResult, Message = ex.Message, Failure = ErrorEvent.FailureType.PreviewFail });
                Logger.LogError("Error in UpdatePreviewAsync, falling back to default previewer: " + ex.Message);
                Previewer.State = PreviewState.Error;
            }
        }

        partial void OnPreviewerChanging(IPreviewer? value)
        {
            VideoPreview.MediaPlayer.Pause();
            VideoPreview.MediaPlayer.Source = null;
            VideoPreview.Source = null;
            AudioPreview.Source = null;
            ImagePreview.Source = null;
            ArchivePreview.Source = null;
            BrowserPreview.Source = null;
            DrivePreview.Source = null;

            ShellPreviewHandlerPreviewer?.Clear();
            ShellPreviewHandlerPreview.Source = null;

            if (Previewer != null)
            {
                Previewer.PropertyChanged -= Previewer_PropertyChanged;
            }

            if (value != null)
            {
                value.PropertyChanged += Previewer_PropertyChanged;
            }
        }

        partial void OnPreviewerChanged(IPreviewer? value)
        {
            // Ensure the media transport controls are only present when viewing video media.
            VideoPreview.MediaPlayer.CommandManager.IsEnabled = value is IVideoPreviewer;
        }

        private void BrowserPreview_DOMContentLoaded(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2DOMContentLoadedEventArgs args)
        {
            /*
             * There is an odd behavior where the WebView2 would not raise the NavigationCompleted event
             * for certain HTML files, even though it has already been loaded. Probably related to certain
             * extra module that require more time to load. One example is saving and opening google.com locally.
             *
             * So to address this, we will make the Browser visible and display it as "Loaded" as soon the HTML document
             * has been parsed and loaded with the DOMContentLoaded event.
             *
             * Similar issue: https://github.com/MicrosoftEdge/WebView2Feedback/issues/998
             */
            if (BrowserPreviewer != null)
            {
                BrowserPreviewer.State = PreviewState.Loaded;
            }
        }

        private void PreviewBrowser_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            /*
             * In theory most of navigation should work after DOM is loaded.
             * But in case something fails we check NavigationCompleted event
             * for failure and switch visibility accordingly.
             *
             * As an alternative, in the future, the preview Browser control
             * could also display error content.
             */
            if (!args.IsSuccess)
            {
                if (BrowserPreviewer != null)
                {
                    BrowserPreviewer.State = PreviewState.Error;
                }
            }
        }

        private void ShellPreviewHandlerPreview_HandlerLoaded(object sender, EventArgs e)
        {
            if (ShellPreviewHandlerPreviewer != null)
            {
                ShellPreviewHandlerPreviewer.State = PreviewState.Loaded;
            }
        }

        private void ShellPreviewHandlerPreview_HandlerError(object sender, EventArgs e)
        {
            if (ShellPreviewHandlerPreviewer != null)
            {
                ShellPreviewHandlerPreviewer.State = PreviewState.Error;
            }
        }

        private async void KeyboardAccelerator_CtrlC_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            if (Previewer != null)
            {
                await Previewer.CopyAsync();
            }
        }

        private void KeyboardAccelerator_Space_Invoked(KeyboardAccelerator sender, KeyboardAcceleratorInvokedEventArgs args)
        {
            var mediaPlayer = VideoPreview.MediaPlayer;

            if (mediaPlayer.Source == null || !mediaPlayer.CanPause)
            {
                return;
            }

            if (mediaPlayer.CurrentState == Windows.Media.Playback.MediaPlayerState.Playing)
            {
                mediaPlayer.Pause();
            }
            else
            {
                mediaPlayer.Play();
            }

            // Prevent the keyboard accelerator to be called twice
            args.Handled = true;
        }

        private async Task UpdateTooltipAsync(CancellationToken cancellationToken)
        {
            if (!ShowFilePreviewTooltip)
            {
                InfoTooltip = null;
                return;
            }

            if (Item == null)
            {
                return;
            }

            // Fetch and format available file properties
            string fileNameFormatted = ReadableStringHelper.FormatResourceString("PreviewTooltip_FileName", Item.Name);
            var sb = new StringBuilder(fileNameFormatted, 256);

            cancellationToken.ThrowIfCancellationRequested();
            string fileType = await Task.Run(Item.GetContentTypeAsync);
            string fileTypeFormatted = string.IsNullOrEmpty(fileType) ? string.Empty : "\n" + ReadableStringHelper.FormatResourceString("PreviewTooltip_FileType", fileType);
            sb.Append(fileTypeFormatted);

            string dateModified = Item.DateModified?.ToString(CultureInfo.CurrentCulture) ?? string.Empty;
            string dateModifiedFormatted = string.IsNullOrEmpty(dateModified) ? string.Empty : "\n" + ReadableStringHelper.FormatResourceString("PreviewTooltip_DateModified", dateModified);
            sb.Append(dateModifiedFormatted);

            string fileSize = ReadableStringHelper.BytesToReadableString(Item.FileSizeBytes);
            string fileSizeFormatted = string.IsNullOrEmpty(fileSize) ? string.Empty : "\n" + ReadableStringHelper.FormatResourceString("PreviewTooltip_FileSize", fileSize);
            sb.Append(fileSizeFormatted);

            if (!ShowFilePreviewTooltip)
            {
                return;
            }

            InfoTooltip = sb.ToString();
        }

        /// <summary>
        /// Set the placement of the tooltip for those previewers supporting the feature, ensuring it does not obscure the Main Window's title bar.
        /// </summary>
        private void ToolTipParentControl_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            var previewControl = sender as FrameworkElement;
            if (previewControl != null)
            {
                var toolTip = ToolTipService.GetToolTip(previewControl) as ToolTip;
                if (toolTip != null)
                {
                    if (string.IsNullOrEmpty(toolTip.Content as string))
                    {
                        toolTip.IsOpen = false;
                        return;
                    }

                    var pos = e.GetCurrentPoint(previewControl).Position;
                    toolTip.Placement = pos.Y < previewControl.ActualHeight / 2 ?
                        PlacementMode.Bottom : PlacementMode.Top;
                }
            }
        }
    }
}
