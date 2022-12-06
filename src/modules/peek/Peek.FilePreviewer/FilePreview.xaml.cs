// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer
{
    using System;
    using System.Threading.Tasks;
    using CommunityToolkit.Mvvm.ComponentModel;
    using CommunityToolkit.WinUI.UI.Media.Pipelines;
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
        public event EventHandler<PreviewSizeChangedArgs>? PreviewSizeChanged;

        public static readonly DependencyProperty FilesProperty =
        DependencyProperty.Register(
            nameof(File),
            typeof(File),
            typeof(FilePreview),
            new PropertyMetadata(false, async (d, e) => await ((FilePreview)d).OnFilePropertyChanged()));

        [ObservableProperty]
        private ImagePreviewer? previewer;

        public FilePreview()
        {
            InitializeComponent();
        }

        public File File
        {
            get => (File)GetValue(FilesProperty);
            set => SetValue(FilesProperty, value);
        }

        public bool IsPreviewLoading(BitmapSource? bitmapSource)
        {
            return bitmapSource == null;
        }

        private async Task OnFilePropertyChanged()
        {
            if (File == null)
            {
                return;
            }

            // TODO: track and cancel existing async preview tasks
            // https://github.com/microsoft/PowerToys/issues/22480

            // TODO: Implement plugin pattern to support any file types.
            if (IsSupportedImage(File.Extension))
            {
                Previewer = new ImagePreviewer(File);
                var size = await Previewer.GetPreviewSizeAsync();
                PreviewSizeChanged?.Invoke(this, new PreviewSizeChangedArgs(size));
                await Previewer.LoadPreviewAsync();
            }
            else
            {
                Previewer = null;
                PreviewSizeChanged?.Invoke(this, new PreviewSizeChangedArgs(new Size(1280, 720)));
            }
        }

        // TODO: Find all supported file types for the image previewer
        private static bool IsSupportedImage(string extension) => extension switch
        {
            ".bmp" => true,
            ".gif" => true,
            ".jpg" => true,
            ".jfif" => true,
            ".jfi" => true,
            ".jif" => true,
            ".jpeg" => true,
            ".jpe" => true,
            ".png" => true,
            ".tif" => true,
            ".tiff" => true,
            _ => false,
        };
    }
}
