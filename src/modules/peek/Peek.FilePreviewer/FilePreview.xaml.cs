// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Peek.Common.Models;
    using Peek.FilePreviewer.Models;
    using Peek.FilePreviewer.Previewers;

    [INotifyPropertyChanged]
    public sealed partial class FilePreview : UserControl
    {
        private readonly PreviewerFactory previewerFactory = new ();

        public event EventHandler<PreviewSizeChangedArgs>? PreviewSizeChanged;

        public static readonly DependencyProperty FilesProperty =
        DependencyProperty.Register(
            nameof(File),
            typeof(File),
            typeof(FilePreview),
            new PropertyMetadata(false, async (d, e) => await ((FilePreview)d).OnFilePropertyChanged()));

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(BitmapPreviewer))]
        [NotifyPropertyChangedFor(nameof(BrowserPreviewer))]
        [NotifyPropertyChangedFor(nameof(UnsupportedFilePreviewer))]
        private IPreviewer? previewer;

        private CancellationTokenSource _cancellationTokenSource = new ();

        public FilePreview()
        {
            InitializeComponent();
        }

        private async void Previewer_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Fallback on DefaultPreviewer if we fail to load the correct Preview
            if (e.PropertyName == nameof(IPreviewer.State))
            {
                if (Previewer?.State == PreviewState.Error)
                {
                    // Cancel previous loading task
                    _cancellationTokenSource.Cancel();
                    _cancellationTokenSource = new ();

                    Previewer = previewerFactory.CreateDefaultPreviewer(File, _cancellationTokenSource.Token);
                    await UpdatePreviewAsync();
                }
            }
        }

        public IBitmapPreviewer? BitmapPreviewer => Previewer as IBitmapPreviewer;

        public IBrowserPreviewer? BrowserPreviewer => Previewer as IBrowserPreviewer;

        public bool IsImageVisible => BitmapPreviewer != null;

        public IUnsupportedFilePreviewer? UnsupportedFilePreviewer => Previewer as IUnsupportedFilePreviewer;

        public bool IsUnsupportedPreviewVisible => UnsupportedFilePreviewer != null;

        public File File
        {
            get => (File)GetValue(FilesProperty);
            set => SetValue(FilesProperty, value);
        }

        public bool MatchPreviewState(PreviewState? value, PreviewState stateToMatch)
        {
            return value == stateToMatch;
        }

        public Visibility IsPreviewVisible(IPreviewer? previewer, PreviewState? state)
        {
            var isValidPreview = previewer != null && MatchPreviewState(state, PreviewState.Loaded);
            return isValidPreview ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task OnFilePropertyChanged()
        {
            // Cancel previous loading task
            _cancellationTokenSource.Cancel();
            _cancellationTokenSource = new ();

            // TODO: track and cancel existing async preview tasks
            // https://github.com/microsoft/PowerToys/issues/22480
            if (File == null)
            {
                Previewer = null;
                ImagePreview.Visibility = Visibility.Collapsed;
                BrowserPreview.Visibility = Visibility.Collapsed;
                UnsupportedFilePreview.Visibility = Visibility.Collapsed;
                return;
            }

            Previewer = previewerFactory.Create(File, _cancellationTokenSource.Token);

            await UpdatePreviewAsync();
        }

        private async Task UpdatePreviewAsync()
        {
            if (Previewer != null)
            {
                try
                {
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    var size = await Previewer.GetPreviewSizeAsync();
                    PreviewSizeChanged?.Invoke(this, new PreviewSizeChangedArgs(size));
                    _cancellationTokenSource.Token.ThrowIfCancellationRequested();
                    await Previewer.LoadPreviewAsync();
                }
                catch (OperationCanceledException)
                {
                    // TODO: Log task cancelled exception?
                }
            }
        }

        partial void OnPreviewerChanging(IPreviewer? value)
        {
            if (Previewer != null)
            {
                Previewer.PropertyChanged -= Previewer_PropertyChanged;
            }

            if (value != null)
            {
                value.PropertyChanged += Previewer_PropertyChanged;
            }
        }

        private void PreviewBrowser_NavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            // Once browser has completed navigation it is ready to be visible
            if (BrowserPreviewer != null)
            {
                BrowserPreviewer.State = PreviewState.Loaded;
            }
        }
    }
}
