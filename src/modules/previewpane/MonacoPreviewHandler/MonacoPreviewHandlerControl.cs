// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Common;
using Microsoft.PowerToys.PreviewHandler.Monaco.Formatters;
using Microsoft.PowerToys.PreviewHandler.Monaco.Helpers;
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
        /// Formatters applied before rendering the preview
        /// </summary>
        private readonly IReadOnlyCollection<IFormatter> _formatters = new List<IFormatter>
        {
            new JsonFormatter(),
            new XmlFormatter(),
        }.AsReadOnly();

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
        /// Loading progress bar
        /// </summary>
        private ProgressBar _loadingBar;

        /// <summary>
        /// Grey background
        /// </summary>
        private Label _loadingBackground;

        /// <summary>
        /// Name of the virtual host
        /// </summary>
        public const string VirtualHostName = "PowerToysLocalMonaco";

        /// <summary>
        /// HTML code passed to the file
        /// </summary>
#nullable enable
        private string? _html;
#nullable disable

        /// <summary>
        /// Id for monaco language
        /// </summary>
        private string _vsCodeLangSet;

        /// <summary>
        /// The content of the previewing file in base64
        /// </summary>
        private string _base64FileCode;

        [STAThread]
        public override void DoPreview<T>(T dataSource)
        {
            Logger.LogTrace();

            if (global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredMonacoPreviewEnabledValue() == global::PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                // GPO is disabling this utility. Show an error message instead.
                _infoBarAdded = true;
                AddTextBoxControl(Properties.Resources.GpoDisabledErrorText);
                Resize += FormResized;
                base.DoPreview(dataSource);

                return;
            }

            base.DoPreview(dataSource);

            // Sets background color
            SetBackground();

            // Starts loading screen
            InitializeLoadingScreen();

            // New webview2 element
            _webView = new WebView2();

            // Checks if dataSource is a string
            if (!(dataSource is string filePath))
            {
                throw new ArgumentException($"{nameof(dataSource)} for {nameof(MonacoPreviewHandlerControl)} must be a string but was a '{typeof(T)}'");
            }

            // Check if the file is too big.
            long fileSize = new FileInfo(filePath).Length;

            if (fileSize < _settings.MaxFileSize)
            {
                Task initializeIndexFileAndSelectedFileTask = new Task(() => { InitializeIndexFileAndSelectedFile(filePath); });
                initializeIndexFileAndSelectedFileTask.Start();

                try
                {
                    Logger.LogInfo("Create WebView2 environment");
                    ConfiguredTaskAwaitable<CoreWebView2Environment>.ConfiguredTaskAwaiter
                        webView2EnvironmentAwaiter = CoreWebView2Environment
                            .CreateAsync(userDataFolder: System.Environment.GetEnvironmentVariable("USERPROFILE") +
                                                            "\\AppData\\LocalLow\\Microsoft\\PowerToys\\MonacoPreview-Temp")
                            .ConfigureAwait(true).GetAwaiter();
                    webView2EnvironmentAwaiter.OnCompleted(async () =>
                    {
                        _loadingBar.Value = 60;
                        this.Update();
                        try
                        {
                            if (CoreWebView2Environment.GetAvailableBrowserVersionString() == null)
                            {
                                throw new WebView2RuntimeNotFoundException();
                            }

                            _webView2Environment = webView2EnvironmentAwaiter.GetResult();

                            _loadingBar.Value = 70;
                            this.Update();

                            // Initialize WebView
                            try
                            {
                                await _webView.EnsureCoreWebView2Async(_webView2Environment).ConfigureAwait(true);

                                // Wait until html is loaded
                                initializeIndexFileAndSelectedFileTask.Wait();

                                _webView.CoreWebView2.SetVirtualHostNameToFolderMapping(VirtualHostName, Settings.AssemblyDirectory, CoreWebView2HostResourceAccessKind.Allow);

                                Logger.LogInfo("Navigates to string of HTML file");

                                _webView.NavigateToString(_html);
                                _webView.NavigationCompleted += WebView2Init;
                                _webView.Height = this.Height;
                                _webView.Width = this.Width;
                                Controls.Add(_webView);
                                _webView.SendToBack();
                                _loadingBar.Value = 100;
                                this.Update();
                            }
                            catch (NullReferenceException e)
                            {
                                Logger.LogError("NullReferenceException catched. Skipping exception.", e);
                            }
                        }
                        catch (WebView2RuntimeNotFoundException e)
                        {
                            Logger.LogWarning("WebView2 was not found:");
                            Logger.LogWarning(e.Message);
                            Controls.Remove(_loading);
                            Controls.Remove(_loadingBar);
                            Controls.Remove(_loadingBackground);

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
                }
                catch (Exception e)
                {
                    Controls.Remove(_loading);
                    Controls.Remove(_loadingBar);
                    Controls.Remove(_loadingBackground);
                    Label text = new Label();
                    text.Text = Resources.Exception_Occurred;
                    text.Text += e.Message;
                    text.Text += "\n" + e.Source;
                    text.Text += "\n" + e.StackTrace;
                    text.Width = 500;
                    text.Height = 10000;
                    Controls.Add(text);
                    Logger.LogError(e.Message);
                }

                this.Resize += FormResize;
            }
            else
            {
                Logger.LogInfo("File is too big to display. Showing error message");

                Controls.Remove(_loading);
                _loadingBar.Dispose();
                Controls.Remove(_loadingBar);
                Controls.Remove(_loadingBackground);
                Label errorMessage = new Label();
                errorMessage.Text = Resources.Max_File_Size_Error.Replace("%1", (_settings.MaxFileSize / 1000).ToString(CultureInfo.CurrentCulture), StringComparison.InvariantCulture);
                errorMessage.Width = 500;
                errorMessage.Height = 50;
                Controls.Add(errorMessage);
            }
        }

        /// <summary>
        /// This event sets the height and width of the webview to the size of the form
        /// </summary>
        public void FormResize(object sender, EventArgs e)
        {
            _webView.Height = this.Height;
            _webView.Width = this.Width;
            this.Update();
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
                Controls.Remove(_loading);
                Controls.Remove(_loadingBar);
                Controls.Remove(_loadingBackground);
#if DEBUG
                _webView.CoreWebView2.OpenDevToolsWindow();
                Logger.LogInfo("Opened Dev Tools window, because solution was built in debug mode");
#endif

                _loadingBar.Value = 80;
                this.Update();
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
            if (e.Uri == "about:blank")
            {
                _hasNavigated = true;
            }
        }

        private void SetBackground()
        {
            Logger.LogTrace();
            this.BackColor = Settings.BackgroundColor;
        }

        private void InitializeLoadingScreen()
        {
            Logger.LogTrace();
            _loadingBackground = new Label();
            _loadingBackground.BackColor = Settings.BackgroundColor;
            _loadingBackground.Width = this.Width;
            _loadingBackground.Height = this.Height;
            Controls.Add(_loadingBackground);
            _loadingBackground.BringToFront();

            _loadingBar = new ProgressBar();
            _loadingBar.Width = this.Width - 10;
            _loadingBar.Location = new Point(5, this.Height / 2);
            _loadingBar.Maximum = 100;
            _loadingBar.Value = 10;
            Controls.Add(_loadingBar);

            _loading = new Label();
            _loading.Text = Resources.Loading_Screen_Message;
            _loading.Width = this.Width;
            _loading.Height = 45;
            _loading.Location = new Point(0, _loadingBar.Location.Y - _loading.Height);
            _loading.TextAlign = ContentAlignment.TopCenter;
            _loading.Font = new Font("MS Sans Serif", 16, FontStyle.Bold);
            _loading.ForeColor = Settings.TextColor;
            Controls.Add(_loading);

            _loading.BringToFront();
            _loadingBar.BringToFront();

            this.Update();

            Logger.LogInfo("Loading screen initialized");
        }

        private void InitializeIndexFileAndSelectedFile(string filePath)
        {
            Logger.LogInfo("Starting getting monaco language id out of filetype");
            _vsCodeLangSet = FileHandler.GetLanguage(Path.GetExtension(filePath));

            using (StreamReader fileReader = new StreamReader(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                Logger.LogInfo("Starting reading requested file");
                var fileContent = fileReader.ReadToEnd();

                if (_settings.TryFormat)
                {
                    var formatter = _formatters.SingleOrDefault(f => f.LangSet == _vsCodeLangSet);
                    if (formatter != null)
                    {
                        try
                        {
                            fileContent = formatter.Format(fileContent);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError($"Failed to apply formatting to {filePath}", ex);
                        }
                    }
                }

                fileReader.Close();
                _base64FileCode = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(fileContent));
                Logger.LogInfo("Reading requested file ended");
            }

            // prepping index html to load in
            using (StreamReader htmlFileReader = new StreamReader(new FileStream(Settings.AssemblyDirectory + "\\index.html", FileMode.Open, FileAccess.Read, FileShare.ReadWrite)))
            {
                Logger.LogInfo("Starting reading HTML source file");
                _html = htmlFileReader.ReadToEnd();
                htmlFileReader.Close();
                Logger.LogInfo("Reading HTML source file ended");
            }

            _html = _html.Replace("[[PT_LANG]]", _vsCodeLangSet, StringComparison.InvariantCulture);
            _html = _html.Replace("[[PT_WRAP]]", _settings.Wrap ? "1" : "0", StringComparison.InvariantCulture);
            _html = _html.Replace("[[PT_THEME]]", Settings.GetTheme(), StringComparison.InvariantCulture);
            _html = _html.Replace("[[PT_CODE]]", _base64FileCode, StringComparison.InvariantCulture);
            _html = _html.Replace("[[PT_URL]]", VirtualHostName, StringComparison.InvariantCulture);
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
