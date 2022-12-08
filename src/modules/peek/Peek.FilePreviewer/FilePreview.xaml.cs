// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer
{
    using System;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.UI.Xaml.Media.Imaging;
    using Peek.Common.Helpers;
    using Peek.Common.Models;
    using Peek.FilePreviewer.Models;
    using Peek.FilePreviewer.Previewers;
    using Windows.Foundation;

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
        [NotifyPropertyChangedFor(nameof(ImageInfoTooltip))]
        private IPreviewer? previewer;
        private string imageTooltip = "No file yet";

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
                    Previewer = previewerFactory.CreateDefaultPreviewer(File);
                    await UpdatePreviewAsync();
                }
            }
        }

        public IBitmapPreviewer? BitmapPreviewer => Previewer as IBitmapPreviewer;

        public IBrowserPreviewer? BrowserPreviewer => Previewer as IBrowserPreviewer;

        public bool IsImageVisible => BitmapPreviewer != null;

        public string ImageInfoTooltip => imageTooltip;

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

            Previewer = previewerFactory.Create(File);
            await UpdatePreviewAsync();
        }

        private async Task UpdatePreviewAsync()
        {
            if (Previewer != null)
            {
                var size = await Previewer.GetPreviewSizeAsync();
                PreviewSizeChanged?.Invoke(this, new PreviewSizeChangedArgs(size));
                await Previewer.LoadPreviewAsync();
            }

            await UpdateImageTooltipAsync();
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

        private async Task UpdateImageTooltipAsync()
        {
            if (File == null)
            {
                return;
            }

            imageTooltip = string.Empty;

            // Fetch and format available file properties
            imageTooltip += ReadableStringHelper.FormatResourceString("PreviewTooltip_FileName", File.FileName);

            string fileType = await PropertyHelper.GetFileType(File.Path);
            imageTooltip += string.IsNullOrEmpty(fileType) ? string.Empty : "\n" + ReadableStringHelper.FormatResourceString("PreviewTooltip_FileType", fileType);

            string dateModified = File.DateModified.ToString();
            imageTooltip += string.IsNullOrEmpty(dateModified) ? string.Empty : "\n" + ReadableStringHelper.FormatResourceString("PreviewTooltip_DateModified", dateModified);

            Size dimensions = await PropertyHelper.GetImageSize(File.Path);
            imageTooltip += dimensions.IsEmpty ? string.Empty : "\n" + ReadableStringHelper.FormatResourceString("PreviewTooltip_Dimensions", dimensions.Width, dimensions.Height);

            ulong bytes = await PropertyHelper.GetFileSizeInBytes(File.Path);
            string fileSize = ReadableStringHelper.BytesToReadableString(bytes);
            imageTooltip += string.IsNullOrEmpty(fileSize) ? string.Empty : "\n" + ReadableStringHelper.FormatResourceString("PreviewTooltip_FileSize", fileSize);

            OnPropertyChanged(nameof(ImageInfoTooltip));
        }
    }
}
