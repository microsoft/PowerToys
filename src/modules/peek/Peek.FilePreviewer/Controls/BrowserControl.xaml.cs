// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Peek.FilePreviewer.Controls
{
    using System;
    using Microsoft.UI.Xaml;
    using Microsoft.UI.Xaml.Controls;
    using Microsoft.Web.WebView2.Core;
    using Windows.System;

    public sealed partial class BrowserControl : UserControl
    {
        public delegate void NavigationCompletedHandler(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args);

        public event NavigationCompletedHandler NavigationCompleted = (sender, args) => { };

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
                "Source",
                typeof(Uri),
                typeof(BrowserControl),
                new PropertyMetadata(null, new PropertyChangedCallback(SourcePropertyChanged)));

        public static readonly DependencyProperty IsNavigationCompletedProperty = DependencyProperty.Register(
                "IsNavigationCompleted",
                typeof(bool),
                typeof(BrowserControl),
                new PropertyMetadata(null));

        public Uri Source
        {
            get { return (Uri)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public bool IsNavigationCompleted
        {
            get { return (bool)GetValue(IsNavigationCompletedProperty); }
            set { SetValue(IsNavigationCompletedProperty, value); }
        }

        public BrowserControl()
        {
            this.InitializeComponent();
        }

        private static void SourcePropertyChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var browserControl = (BrowserControl)d;
            browserControl.Navigate();
        }

        /// <summary>
        /// Navigate to the to the <see cref="Uri"/> set in <see cref="Source"/>.
        /// Calling <see cref="Navigate"/> will always trigger a navigation/refresh
        /// even if web target file is the same.
        /// </summary>
        public void Navigate()
        {
            if (Source != null)
            {
                /* CoreWebView2.Navigate() will always trigger a navigation even if the content/URI is the same.
                 * Use WebView2.Source to avoid re-navigating to the same content. */
                PreviewBrowser.CoreWebView2.Navigate(Source.ToString());
            }
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
                // PreviewBrowser.CoreWebView2.AddWebResourceRequestedFilter("*", Microsoft.Web.WebView2.Core.CoreWebView2WebResourceContext.All);
            }
            catch
            {
                // TODO: exception / log hanlder?
            }
        }

        private async void PreviewBrowser_NavigationStarting(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs args)
        {
            IsNavigationCompleted = false;

            // In case user starts or tries to navigate from within the HTML file we launch default web browser for navigation.
            if (args.Uri != null && args.Uri != Source?.ToString() && args.IsUserInitiated)
            {
                args.Cancel = true;
                await Launcher.LaunchUriAsync(new Uri(args.Uri));
            }
        }

        private void PreviewWV2_NavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            NavigationCompleted?.Invoke(sender, args);

            IsNavigationCompleted = true;
        }
    }
}
