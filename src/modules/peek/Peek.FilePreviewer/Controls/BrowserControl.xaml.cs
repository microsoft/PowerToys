// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using ManagedCommon;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Peek.Common.Constants;
using Windows.ApplicationModel.DataTransfer;
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

        private Color? _originalBackgroundColor;

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

        // Will actually be true for Markdown files as well.
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
            OpenUriDialog.Hide();

            // Setting the background color to transparent.
            // This ensures that non-HTML files are displayed with a transparent background.
            PreviewBrowser.DefaultBackgroundColor = Color.FromArgb(0, 0, 0, 0);

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
                else
                {
                    PreviewBrowser.CoreWebView2.ClearVirtualHostNameToFolderMapping(Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.VirtualHostName);
                }
            }
        }

        private async void PreviewWV2_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                await PreviewBrowser.EnsureCoreWebView2Async();

                // Storing the original background color so it can be reset later for specific file types like HTML.
                if (!_originalBackgroundColor.HasValue)
                {
                    // HACK: We used to store PreviewBrowser.DefaultBackgroundColor here, but WebView started returning transparent when running without a debugger attached. We want html files to be seen as in the browser, which has white as a default background color.
                    _originalBackgroundColor = Colors.White;
                }

                // Setting the background color to transparent when initially loading the WebView2 component.
                // This ensures that non-HTML files are displayed with a transparent background.
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
                else
                {
                    PreviewBrowser.CoreWebView2.ClearVirtualHostNameToFolderMapping(Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.VirtualHostName);
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
            // If the file being previewed is HTML or HTM, reset the background color to its original state.
            // This is done to ensure that HTML and HTM files are displayed as intended, with their own background settings.
            // This shouldn't be done for dev file previewer.
            if (!IsDevFilePreview &&
                (Source?.ToString().EndsWith(".html", StringComparison.OrdinalIgnoreCase) == true ||
                Source?.ToString().EndsWith(".htm", StringComparison.OrdinalIgnoreCase) == true))
            {
                // Reset to default behavior for HTML files
                if (_originalBackgroundColor.HasValue)
                {
                    PreviewBrowser.DefaultBackgroundColor = _originalBackgroundColor.Value;
                }
            }

            DOMContentLoaded?.Invoke(sender, args);
        }

        private async void CoreWebView2_NewWindowRequested(CoreWebView2 sender, CoreWebView2NewWindowRequestedEventArgs args)
        {
            // Monaco opens URI in a new window. We open the URI in the default web browser.
            if (args.Uri != null && args.IsUserInitiated)
            {
                args.Handled = true;
                await ShowOpenUriDialogAsync(new Uri(args.Uri));
            }
        }

        private async void PreviewBrowser_NavigationStarting(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationStartingEventArgs args)
        {
            if (_navigatedUri == null)
            {
                return;
            }

            // In case user starts or tries to navigate from within the HTML file we launch default web browser for navigation.
            // TODO: && args.IsUserInitiated - always false for PDF files, revert the workaround when fixed in WebView2: https://github.com/microsoft/PowerToys/issues/27403
            if (args.Uri != null && args.Uri != _navigatedUri?.ToString())
            {
                args.Cancel = true;
                await ShowOpenUriDialogAsync(new Uri(args.Uri));
            }
        }

        private void PreviewWV2_NavigationCompleted(WebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs args)
        {
            if (args.IsSuccess)
            {
                _navigatedUri = Source;
            }

            // Don't raise NavigationCompleted event if NavigationStarting has been cancelled
            if (args.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
            {
                NavigationCompleted?.Invoke(sender, args);
            }
        }

        private async Task ShowOpenUriDialogAsync(Uri uri)
        {
            OpenUriDialog.Content = uri.ToString();
            var result = await OpenUriDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }

        private void OpenUriDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(sender.Content.ToString());
            Clipboard.SetContent(dataPackage);
        }
    }
}
