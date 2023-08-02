// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Peek.Common.Constants;
using Peek.Common.Helpers;
using Windows.System;
using Windows.UI;

namespace Peek.FilePreviewer.Controls
{
    public sealed partial class BrowserControl : UserControl, IDisposable
    {
        /// <summary>
        /// Helper private Uri where we cache the last navigated page
        /// so we can redirect internal PDF or Webpage links to external
        /// web browser, avoiding WebView internal navigation.
        /// </summary>
        private Uri? _navigatedUri;

        public delegate void NavigationCompletedHandler(WebView2? sender, CoreWebView2NavigationCompletedEventArgs? args);

        public delegate void DOMContentLoadedHandler(CoreWebView2? sender, CoreWebView2DOMContentLoadedEventArgs? args);

        public event NavigationCompletedHandler? NavigationCompleted;

        public event DOMContentLoadedHandler? DOMContentLoaded;

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

        public static readonly DependencyProperty IsDevFilePreviewProperty = DependencyProperty.Register(
            nameof(IsDevFilePreview),
            typeof(bool),
            typeof(BrowserControl),
            new PropertyMetadata(null, new PropertyChangedCallback((d, e) => ((BrowserControl)d).OnIsDevFilePreviewChanged())));

        public bool IsDevFilePreview
        {
            get
            {
                return (bool)GetValue(IsDevFilePreviewProperty);
            }

            set
            {
                SetValue(IsDevFilePreviewProperty, value);
            }
        }

        public BrowserControl()
        {
            this.InitializeComponent();
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", TempFolderPath.Path, EnvironmentVariableTarget.Process);
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

            if (Source != null && PreviewBrowser.CoreWebView2 != null)
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

        private void OnIsDevFilePreviewChanged()
        {
            if (PreviewBrowser.CoreWebView2 != null)
            {
                PreviewBrowser.CoreWebView2.Settings.IsScriptEnabled = IsDevFilePreview;
                if (IsDevFilePreview)
                {
                    PreviewBrowser.CoreWebView2.SetVirtualHostNameToFolderMapping(Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.VirtualHostName, Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.MonacoDirectory, CoreWebView2HostResourceAccessKind.Allow);
                }
            }
        }

        private async void PreviewWV2_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await PreviewBrowser.EnsureCoreWebView2Async();

                // transparent background when loading the page
                PreviewBrowser.DefaultBackgroundColor = Color.FromArgb(0, 0, 0, 0);

                PreviewBrowser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                PreviewBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                PreviewBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
                PreviewBrowser.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                PreviewBrowser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                PreviewBrowser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                PreviewBrowser.CoreWebView2.Settings.IsScriptEnabled = IsDevFilePreview;
                PreviewBrowser.CoreWebView2.Settings.IsWebMessageEnabled = false;

                if (IsDevFilePreview)
                {
                    PreviewBrowser.CoreWebView2.SetVirtualHostNameToFolderMapping(Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.VirtualHostName, Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.MonacoDirectory, CoreWebView2HostResourceAccessKind.Allow);
                }

                PreviewBrowser.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;
                PreviewBrowser.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            }
            catch (Exception ex)
            {
                Logger.LogError("WebView2 loading failed. " + ex.Message);
            }

            Navigate();
        }

        private void CoreWebView2_DOMContentLoaded(CoreWebView2 sender, CoreWebView2DOMContentLoadedEventArgs args)
        {
            DOMContentLoaded?.Invoke(sender, args);
        }

        private async void CoreWebView2_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            // Monaco opens URI in a new window. We open the URI in the default web browser.
            if (args.Uri != null && args.IsUserInitiated)
            {
                args.Handled = true;
                await Launcher.LaunchUriAsync(new Uri(args.Uri));
            }
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
