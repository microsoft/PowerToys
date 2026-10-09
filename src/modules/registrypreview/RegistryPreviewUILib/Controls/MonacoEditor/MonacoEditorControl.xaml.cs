// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Reflection;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Timers;
using System.Web;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
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

        public string Text { get; private set; }

        [ObservableProperty]
        private bool _isLoading;

        public event EventHandler TextChanged;

        public MonacoEditorControl()
        {
            InitializeComponent();
            Environment.SetEnvironmentVariable("WEBVIEW2_USER_DATA_FOLDER", MonacoHelper.TempFolderPath, EnvironmentVariableTarget.Process);

            _textChangedThrottle = new Timer(250);
            _textChangedThrottle.Elapsed += OnTextChangedThrottleElapsed;
            _textChangedThrottle.AutoReset = false;

            ActualThemeChanged += OnActualThemeChanged;
        }

        public async Task SetTextAsync(string text)
        {
            Text = text;

            if (!IsLoading)
            {
                var encodedText = HttpUtility.JavaScriptStringEncode(text);
                await Browser.CoreWebView2.ExecuteScriptAsync($"editor.setValue('{encodedText}')");
            }
        }

        private async void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            await SetThemeAsync();
        }

        private async void Browser_Loaded(object sender, RoutedEventArgs e)
        {
            IsLoading = true;

            await Browser.EnsureCoreWebView2Async();
            Browser.DefaultBackgroundColor = Color.FromArgb(0, 0, 0, 0);
            Browser.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
            Browser.CoreWebView2.PermissionRequested += CoreWebView2_PermissionRequested;
            Browser.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
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

        private async void CoreWebView2_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            await SetThemeAsync();
            IsLoading = false;
            await SetTextAsync(Text);

            Browser.CoreWebView2.WebMessageReceived += CoreWebView2_WebMessageReceived;
            Browser.Focus(FocusState.Programmatic);
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

        private async Task SetThemeAsync()
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
