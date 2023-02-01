// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.System;

namespace Peek.FilePreviewer.Controls
{
    public sealed partial class BrowserControl : UserControl, IDisposable
    {
        /// <summary>
        /// Helper private Uri where we cache the last navigated page
        /// so we can redirect internal PDF or Webpage links to external
        /// webbrowser, avoiding WebView internal navigation.
        /// </summary>
        private Uri? _navigatedUri;

        public delegate void NavigationCompletedHandler(WebView2? sender, CoreWebView2NavigationCompletedEventArgs? args);

        public delegate void DOMContentLoadedHandler(CoreWebView2? sender, CoreWebView2DOMContentLoadedEventArgs? args);

        public event NavigationCompletedHandler? NavigationCompleted;

        public event DOMContentLoadedHandler? DOMContentLoaded;

        private string previewBrowserUserDataFolder = System.Environment.GetEnvironmentVariable("USERPROFILE") +
                        "\\AppData\\LocalLow\\Microsoft\\PowerToys\\Peek-Temp";

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
                nameof(Source),
                typeof(Uri),
                typeof(BrowserControl),
                new PropertyMetadata(null, new PropertyChangedCallback((d, e) => ((BrowserControl)d).SourcePropertyChanged())));

        public Uri? Source
        {
            get { return (Uri)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public BrowserControl()
        {
            this.InitializeComponent();
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", previewBrowserUserDataFolder, EnvironmentVariableTarget.Process);
        }

        public void Dispose()
        {
            if (PreviewBrowser.CoreWebView2 != null)
            {
                PreviewBrowser.CoreWebView2.DOMContentLoaded -= CoreWebView2_DOMContentLoaded;
            }
        }

        /// <summary>
        /// Navigate to the to the <see cref="Uri"/> set in <see cref="Source"/>.
        /// Calling <see cref="Navigate"/> will always trigger a navigation/refresh
        /// even if web target file is the same.
        /// </summary>
        public void Navigate()
        {
            var value = Environment.GetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS");

            _navigatedUri = null;

            if (Source != null)
            {
                /* CoreWebView2.Navigate() will always trigger a navigation even if the content/URI is the same.
                 * Use WebView2.Source to avoid re-navigating to the same content. */
                PreviewBrowser.CoreWebView2.Navigate(Source.ToString());
            }
        }

        private void SourcePropertyChanged()
        {
            Navigate();
        }

        private async void PreviewWV2_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await PreviewBrowser.EnsureCoreWebView2Async();

                PreviewBrowser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                PreviewBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                PreviewBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
                PreviewBrowser.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                PreviewBrowser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                PreviewBrowser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                PreviewBrowser.CoreWebView2.Settings.IsScriptEnabled = false;
                PreviewBrowser.CoreWebView2.Settings.IsWebMessageEnabled = false;

                PreviewBrowser.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
            }
            catch
            {
                // TODO: exception / telemetry log?
            }
        }

        private void CoreWebView2_DOMContentLoaded(CoreWebView2 sender, CoreWebView2DOMContentLoadedEventArgs args)
        {
            DOMContentLoaded?.Invoke(sender, args);
        }

        private async void PreviewBrowser_NavigationStarting(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs args)
        {
            if (_navigatedUri == null)
            {
                return;
            }

            // In case user starts or tries to navigate from within the HTML file we launch default web browser for navigation.
            if (args.Uri != null && args.Uri != _navigatedUri?.ToString() && args.IsUserInitiated)
            {
                args.Cancel = true;
                await Launcher.LaunchUriAsync(new Uri(args.Uri));
            }
        }

        private void PreviewWV2_NavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                _navigatedUri = Source;
            }

            NavigationCompleted?.Invoke(sender, args);
        }
    }
}
