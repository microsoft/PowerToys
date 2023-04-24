// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Peek.Common.Helpers;
using Windows.System;

namespace Peek.FilePreviewer.Controls
{
    [INotifyPropertyChanged]
    public sealed partial class TextFilePreview : UserControl, IDisposable
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

        public static readonly DependencyProperty SourceProperty = DependencyProperty.Register(
                nameof(Source),
                typeof(Uri),
                typeof(TextFilePreview),
                new PropertyMetadata(null, new PropertyChangedCallback((d, e) => ((TextFilePreview)d).SourcePropertyChanged())));

        public string? TempDataFolder { get; set; }

        public Uri? Source
        {
            get { return (Uri)GetValue(SourceProperty); }
            set { SetValue(SourceProperty, value); }
        }

        public TextFilePreview()
        {
            this.InitializeComponent();
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", TempDataFolder, EnvironmentVariableTarget.Process);
        }

        public void Dispose()
        {
            if (PreviewText.CoreWebView2 != null)
            {
                PreviewText.CoreWebView2.DOMContentLoaded -= CoreWebView2_DOMContentLoaded;
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

            if (Source != null && PreviewText.CoreWebView2 != null)
            {
                /* CoreWebView2.Navigate() will always trigger a navigation even if the content/URI is the same.
                 * Use WebView2.Source to avoid re-navigating to the same content. */
                PreviewText.CoreWebView2.Navigate(Source.ToString());
            }
        }

        private void SourcePropertyChanged()
        {
            Navigate();
        }

        private async void PreviewText_Loading(FrameworkElement sender, object args)
        {
            if (PreviewText.CoreWebView2 == null)
            {
                try
                {
                    await PreviewText.EnsureCoreWebView2Async();
                }
                catch (Exception ex)
                {
                    Logger.LogError("CoreWebView2 initialization failed. " + ex.Message);
                }
            }
        }

        private void CoreWebView2_DOMContentLoaded(CoreWebView2 sender, CoreWebView2DOMContentLoadedEventArgs args)
        {
            DOMContentLoaded?.Invoke(sender, args);
        }

        private async void PreviewText_NavigationStarting(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs args)
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

        private void PreviewText_NavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                _navigatedUri = Source;
            }

            // OperationCanceled status code is used when the app cancels a navigation via NavigationStarting event
            if (args.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
            {
                NavigationCompleted?.Invoke(sender, args);
            }
        }

        private void PreviewText_CoreWebView2Initialized(WebView2 sender, CoreWebView2InitializedEventArgs args)
        {
            try
            {
                sender.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                sender.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                sender.CoreWebView2.Settings.AreDevToolsEnabled = false;
                sender.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                sender.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                sender.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                sender.CoreWebView2.Settings.IsScriptEnabled = false;
                sender.CoreWebView2.Settings.IsWebMessageEnabled = false;

                sender.CoreWebView2.DOMContentLoaded += CoreWebView2_DOMContentLoaded;

                if (Source != null)
                {
                    /* CoreWebView2.Navigate() will always trigger a navigation even if the content/URI is the same.
                     * Use WebView2.Source to avoid re-navigating to the same content. */
                    sender.CoreWebView2.Navigate(Source.ToString());
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("WebView2 loading failed. " + ex.Message);
            }
        }
    }
}
