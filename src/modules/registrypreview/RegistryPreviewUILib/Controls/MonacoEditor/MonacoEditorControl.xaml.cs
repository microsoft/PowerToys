// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;
using Windows.UI;

namespace RegistryPreviewUILib
{
    [INotifyPropertyChanged]
    public sealed partial class MonacoEditorControl : UserControl, IDisposable
    {
        private readonly Timer _textChangedThrottle;
        private bool _textChangedThrottled;
        private bool _editorReady;

        public string Text { get; private set; }

        [ObservableProperty]
        private bool _isLoading;

        public event EventHandler TextChanged;

        public MonacoEditorControl()
        {
            InitializeComponent();

            _textChangedThrottle = new Timer(250);
            _textChangedThrottle.Elapsed += OnTextChangedThrottleElapsed;
            _textChangedThrottle.AutoReset = false;

            ActualThemeChanged += OnActualThemeChanged;
        }

        public async Task SetTextAsync(string text)
        {
            Text = text;

            if (_editorReady)
            {
                await SetEditorTextAsync(text);
            }
        }

        private async void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            await SetThemeAsync();
        }

        private async void Browser_Loaded(object sender, RoutedEventArgs e)
        {
            IsLoading = true;

            try
            {
                await InitializeBrowserAsync();
            }
            catch (Exception ex)
            {
                // This is an async void event handler, so an unhandled exception would terminate the process.
                // Keep the app alive when the WebView2 runtime is missing or fails to initialize.
                Trace.TraceError($"RegistryPreview: failed to initialize the Monaco editor: {ex}");
            }
        }

        private async Task InitializeBrowserAsync()
        {
            var environment = await CoreWebView2Environment.CreateWithOptionsAsync(null, MonacoHelper.TempFolderPath, null);
            await Browser.EnsureCoreWebView2Async(environment);
            if (Browser.CoreWebView2 == null)
            {
                throw new InvalidOperationException("The WebView2 runtime could not be initialized.");
            }

            Browser.DefaultBackgroundColor = Color.FromArgb(0, 0, 0, 0);
            Browser.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            Browser.CoreWebView2.PermissionRequested += CoreWebView2_PermissionRequested;
            Browser.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
            Browser.CoreWebView2.WebMessageReceived += CoreWebView2_InitializationMessageReceived;
            Browser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
            Browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            Browser.CoreWebView2.Settings.AreHostObjectsAllowed = false;
            Browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
            Browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
            Browser.CoreWebView2.Settings.IsScriptEnabled = true;
            Browser.CoreWebView2.Settings.IsWebMessageEnabled = true;
#if DEBUG
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = true;
#else
            Browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
#endif

            Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                MonacoHelper.VirtualHostName,
                MonacoHelper.MonacoDirectory,
                CoreWebView2HostResourceAccessKind.Allow);

            var assemblyDir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location) ?? string.Empty;
            var index = Path.GetFullPath(Path.Combine(assemblyDir, "Assets", "RegistryPreview", "index.html"));
            Browser.CoreWebView2.Navigate(index);
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

        private void CoreWebView2_PermissionRequested(CoreWebView2 sender, CoreWebView2PermissionRequestedEventArgs args)
        {
            if (args.PermissionKind == CoreWebView2PermissionKind.ClipboardRead)
            {
                // Hide the permission request dialog
                args.State = CoreWebView2PermissionState.Allow;
                args.Handled = true;
            }
        }

        private void CoreWebView2_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            if (!args.IsSuccess)
            {
                Trace.TraceError($"RegistryPreview: Monaco navigation failed with status {args.WebErrorStatus}.");
            }
        }

        private async void CoreWebView2_InitializationMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            var json = JsonNode.Parse(args.WebMessageAsJson);
            if (json?["id"]?.ToString().Equals("editorReady", StringComparison.OrdinalIgnoreCase) == true)
            {
                await InitializeEditorAsync();
            }
        }

        private void CoreWebView2_WebMessageReceived(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
        {
            var json = JsonNode.Parse(args.WebMessageAsJson);
            if (json == null)
            {
                return;
            }

            var id = json["id"];
            if (id == null || !id.ToString().Equals("contentChanged", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            var content = json["content"].ToString();
            if (content == null)
            {
                return;
            }

            Text = content;
            ThrottleTextChanged();
        }

        private async Task InitializeEditorAsync()
        {
            if (_editorReady)
            {
                return;
            }

            try
            {
                await ApplyThemeAsync();
                await SetEditorTextAsync(Text);
                await Browser.CoreWebView2.ExecuteScriptAsync("startEditorChangeTracking()");
                Browser.CoreWebView2.WebMessageReceived -= CoreWebView2_InitializationMessageReceived;
                _editorReady = true;
                Browser.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
                IsLoading = false;
                AutomationProperties.SetAutomationId(Browser, "Browser");
                Browser.Focus(FocusState.Programmatic);
            }
            catch (Exception ex)
            {
                _editorReady = false;
                Trace.TraceError($"RegistryPreview: failed to initialize the Monaco editor after loading: {ex}");
            }
        }

        private async Task SetEditorTextAsync(string text)
        {
            var encodedText = HttpUtility.JavaScriptStringEncode(text);
            await Browser.CoreWebView2.ExecuteScriptAsync($"editor.setValue('{encodedText}')");
        }

        private async Task SetThemeAsync()
        {
            if (!_editorReady || Browser.CoreWebView2 == null)
            {
                return;
            }

            await ApplyThemeAsync();
        }

        private async Task ApplyThemeAsync()
        {
            var theme = Application.Current.RequestedTheme == ApplicationTheme.Light ? "vs" : "vs-dark";
            await Browser.CoreWebView2.ExecuteScriptAsync($"monaco.editor.setTheme('{theme}')");
        }

        private void OnTextChangedThrottleElapsed(object sender, ElapsedEventArgs e)
        {
            if (_textChangedThrottled)
            {
                _textChangedThrottled = false;
                TextChanged?.Invoke(this, EventArgs.Empty);
                _textChangedThrottle.Start();
            }
        }

        private void ThrottleTextChanged()
        {
            if (_textChangedThrottle.Enabled)
            {
                _textChangedThrottled = true;
                return;
            }

            TextChanged?.Invoke(this, EventArgs.Empty);
            _textChangedThrottle.Start();
        }

        public void Dispose()
        {
            _textChangedThrottle?.Dispose();
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
