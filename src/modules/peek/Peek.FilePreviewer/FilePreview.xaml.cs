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
    using Windows.System;

    [INotifyPropertyChanged]
    public sealed partial class FilePreview : UserControl
    {
        private bool _isWebView2CoreInit = false;

        /// <summary>
        /// Cached the current URI used to navigate so we can
        /// evaluate internal vs external user navigation within
        /// the local HTML.
        /// </summary>
        private Uri _localFileURI = default!;

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

            // TODO: Implement plugin pattern to support any file types.
            if (File.Extension.ToLower() == ".html" && _isWebView2CoreInit)
            {
                NavigateWithWV2(File);
            }
            else if (IsSupportedImage(File.Extension))
            {
                PreviewBrowser.Visibility = Visibility.Collapsed;
                PreviewImage.Visibility = Visibility.Visible;

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

        private void NavigateWithWV2(File file)
        {
            _localFileURI = new Uri(file.Path);
            PreviewBrowser.CoreWebView2.Navigate(_localFileURI.ToString());

            PreviewSizeChanged?.Invoke(this, new PreviewSizeChangedArgs(new Size(1280, 720)));
        }

        private async void PreviewWV2_Loaded(object sender, RoutedEventArgs e)
        {
            // This call might take ~300ms to complete
            await PreviewBrowser.EnsureCoreWebView2Async();

            PreviewBrowser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            PreviewBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            PreviewBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
            PreviewBrowser.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            PreviewBrowser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            PreviewBrowser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            PreviewBrowser.CoreWebView2.Settings.IsScriptEnabled = false;
            PreviewBrowser.CoreWebView2.Settings.IsWebMessageEnabled = false;

            // Don't load any resources.
            PreviewBrowser.CoreWebView2.AddWebResourceRequestedFilter("*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
        }

        private void PreviewWV2_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            _isWebView2CoreInit = true;
        }

        private void PreviewWV2_NavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            PreviewImage.Visibility = Visibility.Collapsed;
            PreviewBrowser.Visibility = Visibility.Visible;
        }

        private async void PreviewBrowser_NavigationStarting(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs args)
        {
            // In case user starts or tries to navigate from within the HTML file we launch default web browser for navigation.
            if (args.Uri != null && args.Uri != _localFileURI?.ToString() && args.IsUserInitiated)
            {
                args.Cancel = true;
                await Launcher.LaunchUriAsync(new Uri(args.Uri));
            }
        }
    }
}
