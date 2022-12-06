// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Controls
{
    using System;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Peek.Common.Models;
    using Windows.System;

    public sealed partial class BrowserControl : UserControl
    {
        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register("Source", typeof(Uri), typeof(BrowserControl), new PropertyMetadata(null, new PropertyChangedCallback(SourcePropertyChanged)));

        /// <summary>
        /// Cached the current URI used to navigate so we can
        /// evaluate internal vs external user navigation within
        /// the local HTML.
        /// </summary>
        private Uri _localFileURI = default!;

        public Uri Source
        {
            get { return (Uri)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public BrowserControl()
        {
            this.InitializeComponent();
        }

        private static void SourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // TODO:
        }

        public void Navigate(File file)
        {
            NavigateWithWV2(file);
        }

        private async void PreviewWV2_Loaded(object sender, RoutedEventArgs e)
        {
            try
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
            catch
            {
                // TODO: exception / log hanlder?
            }
        }

        private void NavigateWithWV2(File file)
        {
            _localFileURI = new Uri(file.Path);

            /* CoreWebView2.Navigate() will always trigger a navigation even if the content/URI is the same.
             * Use WebView2.Source to avoid re-navigating to the same content. */
            PreviewBrowser.CoreWebView2.Navigate(_localFileURI.ToString());
        }

        private void PreviewWV2_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            // TODO:
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

        private void PreviewWV2_NavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            // TODO: replace with proper control visibility change
            // PreviewImage.Visibility = Visibility.Collapsed;
            // PreviewBrowser.Visibility = Visibility.Visible;
        }
    }
}
