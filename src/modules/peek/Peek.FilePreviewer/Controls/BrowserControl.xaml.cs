// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Controls;

using ManagedCommon;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Peek.Common.Constants;
using Peek.Common.Helpers;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI;

using Control = System.Windows.Controls.Control;

namespace Peek.FilePreviewer.Controls
{
    public sealed partial class BrowserControl : Microsoft.UI.Xaml.Controls.UserControl, IDisposable
    {
        /// <summary>
        /// Helper private Uri where we cache the last navigated page
        /// so we can redirect internal PDF or Webpage links to external
        /// web browser, avoiding WebView internal navigation.
        /// </summary>
        private Uri? _navigatedUri;

        private Color? _originalBackgroundColor;

        /// <summary>
        /// URI of the current source being previewed (for resource filtering)
        /// </summary>
        private Uri? _currentSourceUri;

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

        public static readonly DependencyProperty CustomContextMenuProperty = DependencyProperty.Register(
            nameof(CustomContextMenu),
            typeof(bool),
            typeof(BrowserControl),
            null);

        public bool CustomContextMenu
        {
            get
            {
                return (bool)GetValue(CustomContextMenuProperty);
            }

            set
            {
                SetValue(CustomContextMenuProperty, value);
            }
        }

        public BrowserControl()
        {
            this.InitializeComponent();
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", TempFolderPath.Path, EnvironmentVariableTarget.Process);
            Environment.SetEnvironmentVariable("WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS", "--block-new-web-contents", EnvironmentVariableTarget.Process);
        }

        public void Dispose()
        {
            if (PreviewBrowser.CoreWebView2 != null)
            {
                PreviewBrowser.CoreWebView2.DOMContentLoaded -= CoreWebView2_DOMContentLoaded;
                PreviewBrowser.CoreWebView2.ContextMenuRequested -= CoreWebView2_ContextMenuRequested;
                RemoveResourceFilter();
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
                _currentSourceUri = Source;

                // Only apply resource filter for non-dev files
                if (!IsDevFilePreview)
                {
                    ApplyResourceFilter();
                }

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

                    // Remove resource filter for dev files (Monaco needs to load resources)
                    RemoveResourceFilter();
                }
                else
                {
                    PreviewBrowser.CoreWebView2.ClearVirtualHostNameToFolderMapping(Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.VirtualHostName);
                    ApplyResourceFilter();
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
                    _originalBackgroundColor = Microsoft.UI.Colors.White;
                }

                // Setting the background color to transparent when initially loading the WebView2 component.
                // This ensures that non-HTML files are displayed with a transparent background.
                PreviewBrowser.DefaultBackgroundColor = Color.FromArgb(0, 0, 0, 0);

                PreviewBrowser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                PreviewBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
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
                PreviewBrowser.CoreWebView2.ContextMenuRequested += CoreWebView2_ContextMenuRequested;
            }
            catch (Exception ex)
            {
                Logger.LogError("WebView2 loading failed. " + ex.Message);
            }

            Navigate();
        }

        private List<Control> GetContextMenuItems(CoreWebView2 sender, CoreWebView2ContextMenuRequestedEventArgs args)
        {
            var menuItems = args.MenuItems;

            if (menuItems.IsReadOnly)
            {
                return [];
            }

            if (CustomContextMenu)
            {
                MenuItem CreateCommandMenuItem(string resourceId, string commandName)
                {
                    MenuItem commandMenuItem = new()
                    {
                        Header = ResourceLoaderInstance.ResourceLoader.GetString(resourceId),
                        IsEnabled = true,
                    };

                    commandMenuItem.Click += async (s, ex) =>
                    {
                        await sender.ExecuteScriptAsync($"{commandName}()");
                    };

                    return commandMenuItem;
                }

                // When using Monaco, we show menu items that call the appropriate JS functions -
                // WebView2 isn't able to show a "Copy" menu item of its own.
                return [
                    CreateCommandMenuItem("ContextMenu_Copy", "runCopyCommand"),
                    new Separator(),
                    CreateCommandMenuItem("ContextMenu_ToggleTextWrapping", "runToggleTextWrapCommand"),
                    CreateCommandMenuItem("ContextMenu_ToggleMinimap", "runToggleMinimap")
                ];
            }
            else
            {
                MenuItem CreateMenuItemFromWebViewMenuItem(CoreWebView2ContextMenuItem webViewMenuItem)
                {
                    MenuItem menuItem = new()
                    {
                        Header = webViewMenuItem.Label.Replace('&', '_'),  // replace with '_' so it is underlined in the label
                        IsEnabled = webViewMenuItem.IsEnabled,
                        InputGestureText = webViewMenuItem.ShortcutKeyDescription,
                    };

                    menuItem.Click += (_, _) =>
                    {
                        args.SelectedCommandId = webViewMenuItem.CommandId;
                    };

                    return menuItem;
                }

                // When not using Monaco, we keep the "Copy" menu item from WebView2's default context menu.
                return menuItems.Where(menuItem => menuItem.Name == "copy")
                                .Select(CreateMenuItemFromWebViewMenuItem)
                                .ToList<Control>();
            }
        }

        private void CoreWebView2_ContextMenuRequested(CoreWebView2 sender, CoreWebView2ContextMenuRequestedEventArgs args)
        {
            var deferral = args.GetDeferral();
            args.Handled = true;

            var menuItems = GetContextMenuItems(sender, args);

            if (menuItems.Count != 0)
            {
                var contextMenu = new ContextMenu();
                contextMenu.Closed += (_, _) => deferral.Complete();
                contextMenu.IsOpen = true;

                foreach (var menuItem in menuItems)
                {
                    contextMenu.Items.Add(menuItem);
                }
            }
        }

        /// <summary>
        /// Applies strict resource filtering for non-dev files to block external resources.
        /// This prevents XSS attacks and unwanted external content loading.
        /// </summary>
        private void ApplyResourceFilter()
        {
            // Remove existing handler to prevent duplicate subscriptions
            RemoveResourceFilter();

            // Add filter and subscribe to resource requests
            PreviewBrowser.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
            PreviewBrowser.CoreWebView2.WebResourceRequested += CoreWebView2_WebResourceRequested;
        }

        private void RemoveResourceFilter()
        {
            PreviewBrowser.CoreWebView2.WebResourceRequested -= CoreWebView2_WebResourceRequested;
        }

        private void CoreWebView2_WebResourceRequested(CoreWebView2 sender, CoreWebView2WebResourceRequestedEventArgs args)
        {
            if (_currentSourceUri == null)
            {
                return;
            }

            var requestUri = new Uri(args.Request.Uri);

            // Allow loading the source file itself
            if (requestUri == _currentSourceUri)
            {
                return;
            }

            // For local file:// resources, allow same directory and subdirectories
            if (requestUri.Scheme == "file" && _currentSourceUri.Scheme == "file")
            {
                try
                {
                    var sourceDirectory = System.IO.Path.GetDirectoryName(_currentSourceUri.LocalPath);
                    var requestPath = requestUri.LocalPath;

                    // Allow resources in the same directory or subdirectories
                    if (!string.IsNullOrEmpty(sourceDirectory) &&
                        requestPath.StartsWith(sourceDirectory, StringComparison.OrdinalIgnoreCase))
                    {
                        return;
                    }
                }
                catch
                {
                    // If path processing fails, block for security
                }
            }

            // Block all other resources including http(s) requests to prevent external tracking,
            // data exfiltration, and XSS attacks
            args.Response = PreviewBrowser.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Forbidden", null);
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

        private async void PreviewBrowser_NavigationStarting(WebView2 sender, CoreWebView2NavigationStartingEventArgs args)
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

        private void PreviewWV2_NavigationCompleted(WebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
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
            OpenUriDialogContent.Text = uri.ToString();
            var result = await OpenUriDialog.ShowAsync();

            if (result == ContentDialogResult.Primary)
            {
                await Launcher.LaunchUriAsync(uri);
            }
        }

        private void OpenUriDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(OpenUriDialogContent.Text);
            Clipboard.SetContent(dataPackage);
        }
    }
}
