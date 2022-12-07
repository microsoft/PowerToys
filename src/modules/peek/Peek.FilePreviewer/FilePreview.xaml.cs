// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer
{
    using System;
    using System.Diagnostics;
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
        private IPreviewer? previewer;

        public FilePreview()
        {
            InitializeComponent();
        }

        public IBitmapPreviewer? BitmapPreviewer => Previewer as IBitmapPreviewer;

        public bool IsImageVisible => BitmapPreviewer != null;

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
            else
            {
                // TODO: figure out optimal window size for unsupported control
                PreviewSizeChanged?.Invoke(this, new PreviewSizeChangedArgs(new Size(1280, 720)));
            }
        }
    }
}
