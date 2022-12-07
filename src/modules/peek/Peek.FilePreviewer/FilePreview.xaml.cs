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
        [NotifyPropertyChangedFor(nameof(BrowserPreviewer))]
        [NotifyPropertyChangedFor(nameof(IsBrowserVisible))]
        private IPreviewer? previewer;

        public FilePreview()
        {
            InitializeComponent();
        }

        public IBitmapPreviewer? BitmapPreviewer => Previewer as IBitmapPreviewer;

        public IBrowserPreview? BrowserPreviewer => Previewer as IBrowserPreview;

        public bool IsImageVisible => BitmapPreviewer != null;

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
            else
            {
                // TODO: figure out optimal window size for unsupported control
                PreviewSizeChanged?.Invoke(this, new PreviewSizeChangedArgs(new Size(1280, 720)));
            }
        }

        private void PreviewBrowser_NavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            // Once browser has completed navigation it is ready to be visible
            OnPropertyChanged(nameof(IsBrowserVisible));
        }
    }
}
