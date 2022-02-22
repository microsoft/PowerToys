// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.CompilerServices;
using System.Windows.Forms;
using Common;
using Microsoft.PowerToys.PreviewHandler.Monaco.Properties;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Windows.System;

namespace Microsoft.PowerToys.PreviewHandler.Monaco
{
    public class MonacoPreviewHandlerControl : FormHandlerControl
    {
        /// <summary>
        /// Settings class
        /// </summary>
        private readonly Settings _settings = new Settings();

        /// <summary>
        /// Saves if the user already navigated to the index page
        /// </summary>
        private bool _hasNavigated;

        /// <summary>
        /// WebView2 element
        /// </summary>
        private WebView2 _webView;

        /// <summary>
        /// WebView2 Environment
        /// </summary>
        private CoreWebView2Environment _webView2Environment;

        /// <summary>
        /// Loading label
        /// </summary>
        private Label _loading;

        /// <summary>
        /// Name of the virtual host
        /// </summary>
        public const string VirtualHostName = "PowerToysLocalMonaco";

        [STAThread]
        public override void DoPreview<T>(T dataSource)
        {
            base.DoPreview(dataSource);

            // Starts loading screen
            InitializeLoadingScreen();

            // New webview2 element
            _webView = new WebView2();

            // Checks if dataSource is a string
            if (!(dataSource is string filePath))
            {
                throw new ArgumentException($"{nameof(dataSource)} for {nameof(MonacoPreviewHandler)} must be a string but was a '{typeof(T)}'");
            }

            // Check if the file is too big.
            long fileSize = new FileInfo(filePath).Length;

            if (fileSize < _settings.MaxFileSize)
            {
                try
                {
                    InvokeOnControlThread(() =>
                    {
                        ConfiguredTaskAwaitable<CoreWebView2Environment>.ConfiguredTaskAwaiter
                            webView2EnvironmentAwaiter = CoreWebView2Environment
                                .CreateAsync(userDataFolder: System.Environment.GetEnvironmentVariable("USERPROFILE") +
                                                             "\\AppData\\LocalLow\\Microsoft\\PowerToys\\MonacoPreview-Temp")
                                .ConfigureAwait(true).GetAwaiter();
                        webView2EnvironmentAwaiter.OnCompleted(() =>
                        {
                            InvokeOnControlThread(async () =>
                            {
                                try
                                {
                                    if (CoreWebView2Environment.GetAvailableBrowserVersionString() == null)
                                    {
                                        throw new WebView2RuntimeNotFoundException();
                                    }

                                    _webView2Environment = webView2EnvironmentAwaiter.GetResult();
                                    var vsCodeLangSet = FileHandler.GetLanguage(Path.GetExtension(filePath));
                                    var fileContent = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)).ReadToEnd();
                                    var base64FileCode = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fileContent));

                                    // prepping index html to load in
                                    var html = new StreamReader(new FileStream(Settings.AssemblyDirectory + "\\index.html", FileMode.Open, FileAccess.Read, FileShare.ReadWrite)).ReadToEnd();

                                    html = html.Replace("[[PT_LANG]]", vsCodeLangSet, StringComparison.InvariantCulture);
                                    html = html.Replace("[[PT_WRAP]]", _settings.Wrap ? "1" : "0", StringComparison.InvariantCulture);
                                    html = html.Replace("[[PT_THEME]]", Settings.GetTheme(), StringComparison.InvariantCulture);
                                    html = html.Replace("[[PT_CODE]]", base64FileCode, StringComparison.InvariantCulture);
                                    html = html.Replace("[[PT_URL]]", VirtualHostName, StringComparison.InvariantCulture);

                                    // Initialize WebView
                                    try
                                    {
                                        await _webView.EnsureCoreWebView2Async(_webView2Environment).ConfigureAwait(true);
                                        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(VirtualHostName, Settings.AssemblyDirectory, CoreWebView2HostResourceAccessKind.Allow);
                                        _webView.NavigateToString(html);
                                        _webView.NavigationCompleted += WebView2Init;
                                        _webView.Height = this.Height;
                                        _webView.Width = this.Width;
                                        Controls.Add(_webView);
                                    }
                                    catch (NullReferenceException)
                                    {
                                    }
                                }
                                catch (WebView2RuntimeNotFoundException)
                                {
                                    Controls.Remove(_loading);

                                    // WebView2 not installed message
                                    Label errorMessage = new Label();
                                    errorMessage.Text = Resources.WebView2_Not_Installed_Message;
                                    errorMessage.Width = TextRenderer.MeasureText(Resources.WebView2_Not_Installed_Message, errorMessage.Font).Width + 10;
                                    errorMessage.Height = TextRenderer.MeasureText(Resources.WebView2_Not_Installed_Message, errorMessage.Font).Height;
                                    Controls.Add(errorMessage);

                                    // Download Link
                                    Label downloadLink = new LinkLabel();
                                    downloadLink.Text = Resources.Download_WebView2;
                                    downloadLink.Click += DownloadLink_Click;
                                    downloadLink.Top = TextRenderer.MeasureText(Resources.WebView2_Not_Installed_Message, errorMessage.Font).Height + 10;
                                    downloadLink.Width = TextRenderer.MeasureText(Resources.Download_WebView2, errorMessage.Font).Width + 10;
                                    downloadLink.Height = TextRenderer.MeasureText(Resources.Download_WebView2, errorMessage.Font).Height;
                                    Controls.Add(downloadLink);
                                }
                            });
                        });
                    });
                }
                catch (Exception e)
                {
                    InvokeOnControlThread(() =>
                    {
                        Controls.Remove(_loading);
                        Label text = new Label();
                        text.Text = Resources.Exception_Occurred;
                        text.Text += e.Message;
                        text.Text += "\n" + e.Source;
                        text.Text += "\n" + e.StackTrace;
                        text.Width = 500;
                        text.Height = 10000;
                        Controls.Add(text);
                    });
                }

                this.Resize += FormResize;
            }
            else
            {
                InvokeOnControlThread(() =>
                {
                    Controls.Remove(_loading);
                    Label errorMessage = new Label();
                    errorMessage.Text = Resources.Max_File_Size_Error;
                    errorMessage.Width = 500;
                    errorMessage.Height = 50;
                    Controls.Add(errorMessage);
                });
            }
        }

        /// <summary>
        /// This event sets the height and width of the webview to the size of the form
        /// </summary>
        public void FormResize(object sender, EventArgs e)
        {
            _webView.Height = this.Height;
            _webView.Width = this.Width;
        }

        /// <summary>
        /// This event initializes the webview and sets various settings
        /// </summary>
        [STAThread]
        private void WebView2Init(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            // Checks if already navigated
            if (!_hasNavigated)
            {
                CoreWebView2Settings settings = (sender as WebView2).CoreWebView2.Settings;

#if DEBUG
                // Enable developer tools and context menu for debugging
                settings.AreDefaultContextMenusEnabled = true;
                settings.AreDevToolsEnabled = true;
#else
                // Disable context menu
                settings.AreDefaultContextMenusEnabled = false;

                // Disable developer tools
                settings.AreDevToolsEnabled = false;
#endif

                // Disable script dialogs (like alert())
                settings.AreDefaultScriptDialogsEnabled = false;

                // Enables JavaScript
                settings.IsScriptEnabled = true;

                // Disable zoom with ctrl and scroll
                settings.IsZoomControlEnabled = false;

                // Disable developer menu
                settings.IsBuiltInErrorPageEnabled = false;

                // Disable status bar
                settings.IsStatusBarEnabled = false;

                Controls.Remove(_loading);
#if DEBUG
                _webView.CoreWebView2.OpenDevToolsWindow();
#endif
            }
        }

        /// <summary>
        /// This event cancels every navigation inside the webview
        /// </summary>
        [STAThread]
        private void NavigationStarted(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            // Prevents navigation if already one done to index.html
            if (_hasNavigated)
            {
                e.Cancel = false;
            }

            // If it has navigated to index.html it stops further navigations
            if (e.Uri == "about:blank")
            {
                _hasNavigated = true;
            }
        }

        private void InitializeLoadingScreen()
        {
            InvokeOnControlThread(() =>
            {
                _loading = new Label();
                _loading.Text = Resources.Loading_Screen_Message;
                _loading.Width = this.Width;
                _loading.Height = this.Height;
                _loading.Font = new Font("MS Sans Serif", 16, FontStyle.Bold);
                _loading.ForeColor = Settings.TextColor;
                _loading.BackColor = Settings.BackgroundColor;
                Controls.Add(_loading);
            });
        }

        private async void DownloadLink_Click(object sender, EventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section"));
        }
    }
}
