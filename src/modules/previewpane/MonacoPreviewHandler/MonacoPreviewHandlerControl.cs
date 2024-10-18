// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using System.Text;

using Common;
using ManagedCommon;
using Microsoft.PowerToys.FilePreviewCommon;
using Microsoft.PowerToys.PreviewHandler.Monaco.Properties;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using UtfUnknown;
using Windows.System;

namespace Microsoft.PowerToys.PreviewHandler.Monaco
{
    public class MonacoPreviewHandlerControl : FormHandlerControl
    {
        /// <summary>
        /// WebView2 element
        /// </summary>
        private readonly WebView2 _webView = new();

        /// <summary>
        /// Text box to display the information about blocked elements from Svg.
        /// </summary>
        private RichTextBox _textBox;

        /// <summary>
        /// Represent if an text box info bar is added for showing message.
        /// </summary>
        private bool _infoBarAdded;

        /// <summary>
        /// Saves if the user already navigated to the index page
        /// </summary>
        private bool _hasNavigated;

        /// <summary>
        /// WebView2 Environment
        /// </summary>
        private CoreWebView2Environment _webView2Environment;

        /// <summary>
        /// Id for monaco language
        /// </summary>
        private string _vsCodeLangSet;

        private Encoding _encodingToUse;

        private string _fileContent;

        private static readonly string _appDataPath = Environment.GetEnvironmentVariable("USERPROFILE") + "\\AppData\\LocalLow\\Microsoft\\PowerToys\\MonacoPreview-Temp";

        private static readonly bool _doesGpoDisableMonaco = global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredMonacoPreviewEnabledValue() == global::PowerToys.GPOWrapper.GpoRuleConfigured.Disabled;

        private Task _gatherFileInformationTask;

        public MonacoPreviewHandlerControl()
        {
            SetBackground();
        }

        [STAThread]
        public override async void DoPreview<T>(T dataSource)
        {
            Logger.LogTrace();

            // Checks if dataSource is a string
            if (dataSource is not string filePath)
            {
                throw new ArgumentException($"{nameof(dataSource)} for {nameof(MonacoPreviewHandlerControl)} must be a string but was a '{typeof(T)}'");
            }

            if (_doesGpoDisableMonaco)
            {
                // GPO is disabling this utility. Show an error message instead.
                _infoBarAdded = true;
                AddTextBoxControl(Resources.GpoDisabledErrorText);
                Resize += FormResized;
                base.DoPreview(dataSource);
                return;
            }

            _gatherFileInformationTask = Task.Run(() => GatherFileInformation(filePath));
            Task initializeIndexFileAndSelectedFileTask = InitializeIndexFileAndSelectedFile(filePath);

            // New webview2 element
            _webView.DefaultBackgroundColor = Color.Transparent;

            try
            {
                // Check if the file is too big.
                long fileSize = new FileInfo(filePath).Length;

                if (fileSize > Settings.MaxFileSize)
                {
                    Logger.LogInfo("File is too big to display. Showing error message");
                    AddTextBoxControl(Resources.Max_File_Size_Error.Replace("%1", (Settings.MaxFileSize / 1000).ToString(CultureInfo.CurrentCulture), StringComparison.InvariantCulture));
                    return;
                }

                Logger.LogInfo("Create WebView2 environment");
                Task<CoreWebView2Environment> webView2EnvironmentTask = CoreWebView2Environment.CreateAsync(
                    userDataFolder: _appDataPath,
                    options: new CoreWebView2EnvironmentOptions()
                    {
                        AreBrowserExtensionsEnabled = false,
                        EnableTrackingPrevention = false,
                        AdditionalBrowserArguments = "--disable-background-networking --disable-sync --disable-web-security --disable-client-side-phishing-detection --disable-component-extensions-with-backgroud-pages --disable-features=Translate,CalculateNativeWinOcclusion,BackForwardCache,HeavyAdPrivacyMitigations, --disable-renderer-backgrounding",
                    });

                base.DoPreview(dataSource);

                try
                {
                    if (CoreWebView2Environment.GetAvailableBrowserVersionString() == null)
                    {
                        throw new WebView2RuntimeNotFoundException();
                    }

                    _webView2Environment = await webView2EnvironmentTask;

                    // Initialize WebView
                    try
                    {
                        await _webView.EnsureCoreWebView2Async(_webView2Environment);
                        _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(MonacoHelper.VirtualHostName, MonacoHelper.MonacoDirectory, CoreWebView2HostResourceAccessKind.Allow);

                        _webView.CoreWebView2.Navigate("http://" + MonacoHelper.VirtualHostName + "/index.html");
                        _webView.NavigationCompleted += WebView2Init;
                        Controls.Add(_webView);
                        _webView.BringToFront();
                        _webView.Height = Height;
                        _webView.Width = Width;
                        _webView.CoreWebView2.NewWindowRequested += CoreWebView2_NewWindowRequested;
                    }
                    catch (NullReferenceException e)
                    {
                        Logger.LogError("NullReferenceException caught. Skipping exception.", e);
                    }
                }
                catch (WebView2RuntimeNotFoundException e)
                {
                    Logger.LogWarning("WebView2 was not found:");
                    Logger.LogWarning(e.Message);

                    // WebView2 not installed message
                    Label errorMessage = new();
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
                    downloadLink.ForeColor = Settings.TextColor;
                    Controls.Add(downloadLink);
                }
            }
            catch (UnauthorizedAccessException e)
            {
                Logger.LogError(e.Message);
                AddTextBoxControl(Resources.Access_Denied_Exception_Message);
            }
            catch (Exception e)
            {
                Logger.LogError(e.Message);
                string errorMessage = Resources.Exception_Occurred;
                errorMessage += e.Message;
                errorMessage += "\n" + e.Source;
                errorMessage += "\n" + e.StackTrace;
                AddTextBoxControl(errorMessage);
            }

            Resize += FormResize;
            SetFocus();
        }

        private async void CoreWebView2_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            // Monaco opens URI in a new window. We open the URI in the default web browser.
            if (e.Uri != null && e.IsUserInitiated)
            {
                e.Handled = true;
                await Launcher.LaunchUriAsync(new Uri(e.Uri));
            }
        }

        /// <summary>
        /// This event sets the height and width of the webview to the size of the form
        /// </summary>
        public void FormResize(object sender, EventArgs e)
        {
            _webView.Height = Height;
            _webView.Width = Width;
            Update();
        }

        /// <summary>
        /// This event initializes the webview and sets various settings
        /// </summary>
        [STAThread]
        private async void WebView2Init(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            // Checks if already navigated
            if (!_hasNavigated)
            {
                await _gatherFileInformationTask.ConfigureAwait(true);
                await _webView.CoreWebView2.ExecuteScriptAsync(MonacoHelper.GetSetContentCommand(_fileContent)).ConfigureAwait(true);
                await _webView.CoreWebView2.ExecuteScriptAsync(MonacoHelper.GetSetLanguageCommand(_vsCodeLangSet)).ConfigureAwait(true);
                await _webView.CoreWebView2.ExecuteScriptAsync(MonacoHelper.GetSetWordWrapCommand(Settings.Wrap)).ConfigureAwait(true);
                await _webView.CoreWebView2.ExecuteScriptAsync(MonacoHelper.GetSetThemeCommand(Settings.GetTheme() == "dark" ? MonacoHelper.DefaultDarkTheme : MonacoHelper.DefaultLightTheme)).ConfigureAwait(true);

                Logger.LogInfo("Setting WebView2 settings");
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

                Logger.LogInfo("Remove loading elements");
#if DEBUG
                _webView.CoreWebView2.OpenDevToolsWindow();
                Logger.LogInfo("Opened Dev Tools window, because solution was built in debug mode");
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
                Logger.LogInfo("Stopped navigation from user");
            }

            // If it has navigated to index.html it stops further navigations
            if (e.Uri.Contains(MonacoHelper.VirtualHostName))
            {
                _hasNavigated = true;
            }
        }

        private void SetBackground()
        {
            Logger.LogTrace();
            BackColor = Settings.BackgroundColor;
        }

        private async Task InitializeIndexFileAndSelectedFile(string filePath)
        {
            DetectionResult result = CharsetDetector.DetectFromFile(filePath);
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            // Check if the detected encoding is not null, otherwise default to UTF-8
            _encodingToUse = result.Detected?.Encoding ?? Encoding.UTF8;
            using StreamReader fileReader = new(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite), _encodingToUse);
            Logger.LogInfo("Starting reading requested file");
            _fileContent = await fileReader.ReadToEndAsync().ConfigureAwait(false);

            if (Settings.TryFormat)
            {
                var formatter = MonacoHelper.Formatters.SingleOrDefault(f => f.LangSet == _vsCodeLangSet);
                if (formatter != null)
                {
                    try
                    {
                        _fileContent = formatter.Format(_fileContent);
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to apply formatting to {filePath}", ex);
                    }
                }
            }

            fileReader.Close();
            Logger.LogInfo("Reading requested file ended");
        }

        private void GatherFileInformation(string filePath)
        {
            Logger.LogInfo("Starting getting monaco language id out of filetype");
            _vsCodeLangSet = MonacoHelper.GetLanguage(Path.GetExtension(filePath));
        }

        private async void DownloadLink_Click(object sender, EventArgs e)
        {
            await Launcher.LaunchUriAsync(new Uri("https://developer.microsoft.com/en-us/microsoft-edge/webview2/#download-section"));
            Logger.LogTrace();
        }

        /// <summary>
        /// Occurs when RichtextBox is resized.
        /// </summary>
        /// <param name="sender">Reference to resized control.</param>
        /// <param name="e">Provides data for the ContentsResized event.</param>
        private void RTBContentsResized(object sender, ContentsResizedEventArgs e)
        {
            var richTextBox = sender as RichTextBox;
            richTextBox.Height = e.NewRectangle.Height + 5;
        }

        /// <summary>
        /// Occurs when form is resized.
        /// </summary>
        /// <param name="sender">Reference to resized control.</param>
        /// <param name="e">Provides data for the resize event.</param>
        private void FormResized(object sender, EventArgs e)
        {
            if (_infoBarAdded)
            {
                _textBox.Width = Width;
            }
        }

        /// <summary>
        /// Adds a Text Box in Controls for showing information about blocked elements.
        /// </summary>
        /// <param name="message">Message to be displayed in textbox.</param>
        private void AddTextBoxControl(string message)
        {
            _textBox = new RichTextBox();
            _textBox.Text = message;
            _textBox.BackColor = Color.LightYellow;
            _textBox.Multiline = true;
            _textBox.Dock = DockStyle.Top;
            _textBox.ReadOnly = true;
            _textBox.ContentsResized += RTBContentsResized;
            _textBox.ScrollBars = RichTextBoxScrollBars.None;
            _textBox.BorderStyle = BorderStyle.None;
            Controls.Add(_textBox);
        }
    }
}
