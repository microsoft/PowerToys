// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Web;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;

namespace RegistryPreviewUILib
{
    [INotifyPropertyChanged]
    public sealed partial class MonacoEditorControl : UserControl
    {
        public string Text { get; private set; }

        [ObservableProperty]
        private bool _isLoading;

        public event EventHandler TextChanged;

        public MonacoEditorControl()
        {
            InitializeComponent();

            ActualThemeChanged += OnActualThemeChanged;
        }

        public async Task SetTextAsync(string text)
        {
            if (IsLoading)
            {
                await Browser.EnsureCoreWebView2Async();
            }

            var encodedText = HttpUtility.JavaScriptStringEncode(text);
            await Browser.CoreWebView2.ExecuteScriptAsync($"editor.setValue('{encodedText}')");
        }

        private async void OnActualThemeChanged(FrameworkElement sender, object args)
        {
            await SetThemeAsync();
        }

        private async void Browser_Loaded(object sender, RoutedEventArgs e)
        {
            IsLoading = true;

            await Browser.EnsureCoreWebView2Async();
            Browser.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
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

            // TODO decouple from FilePreviewCommon
            Browser.CoreWebView2.SetVirtualHostNameToFolderMapping(
                Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.VirtualHostName,
                Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.MonacoDirectory,
                CoreWebView2HostResourceAccessKind.Allow);

            // TODO share Monaco src with other projects
            Browser.CoreWebView2.Navigate(Path.Combine(Microsoft.PowerToys.FilePreviewCommon.MonacoHelper.MonacoDirectory, "registryEditorIndex.html"));
        }

        private async void CoreWebView2_NavigationCompleted(CoreWebView2 sender, CoreWebView2NavigationCompletedEventArgs args)
        {
            await SetThemeAsync();
            await SetTextAsync(Text);
            IsLoading = false;

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
            TextChanged?.Invoke(this, EventArgs.Empty);
        }

        private async Task SetThemeAsync()
        {
            var theme = Application.Current.RequestedTheme == ApplicationTheme.Light ? "vs" : "vs-dark";
            await Browser.CoreWebView2.ExecuteScriptAsync($"monaco.editor.setTheme('{theme}')");
        }
    }
}
