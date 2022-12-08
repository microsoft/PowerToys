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
        [NotifyPropertyChangedFor(nameof(IsImageVisible))]
        [NotifyPropertyChangedFor(nameof(UnsupportedFilePreviewer))]
        [NotifyPropertyChangedFor(nameof(IsUnsupportedPreviewVisible))]
        [NotifyPropertyChangedFor(nameof(BrowserPreviewer))]
        [NotifyPropertyChangedFor(nameof(IsBrowserVisible))]
        [NotifyPropertyChangedFor(nameof(ImageInfoTooltip))]
        private IPreviewer? previewer;
        private string imageTooltip = "No file yet";

        public FilePreview()
        {
            InitializeComponent();
        }

        public IBitmapPreviewer? BitmapPreviewer => Previewer as IBitmapPreviewer;

        public IBrowserPreview? BrowserPreviewer => Previewer as IBrowserPreview;

        public bool IsImageVisible => BitmapPreviewer != null;

        public string ImageInfoTooltip => imageTooltip;

        public IUnsupportedFilePreviewer? UnsupportedFilePreviewer => Previewer as IUnsupportedFilePreviewer;

        public bool IsUnsupportedPreviewVisible => UnsupportedFilePreviewer != null;

        /* TODO: need a better way to switch visibility according to the Preview.
         * Could use Enum + Converter to switch according to the current preview. */
        public bool IsBrowserVisible
        {
            get
            {
                if (BrowserPreviewer != null)
                {
                    return BrowserPreviewer.IsPreviewLoaded;
                }

                return false;
            }
        }

        public File File
        {
            get => (File)GetValue(FilesProperty);
            set => SetValue(FilesProperty, value);
        }

        private async Task OnFilePropertyChanged()
        {
            // TODO: track and cancel existing async preview tasks
            // https://github.com/microsoft/PowerToys/issues/22480
            if (File == null)
            {
                return;
            }

            Previewer = previewerFactory.Create(File);
            if (Previewer != null)
            {
                var size = await Previewer.GetPreviewSizeAsync();
                PreviewSizeChanged?.Invoke(this, new PreviewSizeChangedArgs(size));
                await Previewer.LoadPreviewAsync();
            }

            await UpdateImageTooltipAsync();
        }

        private void PreviewBrowser_NavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            // Once browser has completed navigation it is ready to be visible
            OnPropertyChanged(nameof(IsBrowserVisible));
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
