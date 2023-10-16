// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.IO.Abstractions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Common;
using Microsoft.PowerToys.PreviewHandler.Markdown.Properties;
using Microsoft.PowerToys.PreviewHandler.Markdown.Telemetry.Events;
using Microsoft.PowerToys.Telemetry;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Windows.System;

namespace Microsoft.PowerToys.PreviewHandler.Markdown
{
    /// <summary>
    /// Win Form Implementation for Markdown Preview Handler.
    /// </summary>
    public class MarkdownPreviewHandlerControl : FormHandlerControl
    {
        private static readonly IFileSystem FileSystem = new FileSystem();
        private static readonly IPath Path = FileSystem.Path;
        private static readonly IFile File = FileSystem.File;

        /// <summary>
        /// RichTextBox control to display if external images are blocked.
        /// </summary>
        private RichTextBox _infoBar;

        /// <summary>
        /// Extended Browser Control to display markdown html.
        /// </summary>
        private WebView2 _browser;

        /// <summary>
        /// WebView2 Environment
        /// </summary>
        private CoreWebView2Environment _webView2Environment;

        /// <summary>
        /// Name of the virtual host
        /// </summary>
        public const string VirtualHostName = "PowerToysLocalMarkdown";

        /// <summary>
        /// URI of the local file saved with the contents
        /// </summary>
        private Uri _localFileURI;

        /// <summary>
        /// True if external image is blocked, false otherwise.
        /// </summary>
        private bool _infoBarDisplayed;

        /// <summary>
        /// Gets the path of the current assembly.
        /// </summary>
        /// <remarks>
        /// Source: https://stackoverflow.com/a/283917/14774889
        /// </remarks>
        public static string AssemblyDirectory
        {
            get
            {
                string codeBase = Assembly.GetExecutingAssembly().Location;
                UriBuilder uri = new UriBuilder(codeBase);
                string path = Uri.UnescapeDataString(uri.Path);
                return Path.GetDirectoryName(path);
            }
        }

        /// <summary>
        /// Represent WebView2 user data folder path.
        /// </summary>
        private string _webView2UserDataFolder = System.Environment.GetEnvironmentVariable("USERPROFILE") +
                                "\\AppData\\LocalLow\\Microsoft\\PowerToys\\MarkdownPreview-Temp";

        /// <summary>
        /// Initializes a new instance of the <see cref="MarkdownPreviewHandlerControl"/> class.
        /// </summary>
        public MarkdownPreviewHandlerControl()
        {
            this.SetBackgroundColor(Settings.BackgroundColor);
        }

        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="dataSource">Path to the file.</param>
        public override void DoPreview<T>(T dataSource)
        {
            if (global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredMarkdownPreviewEnabledValue() == global::PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                // GPO is disabling this utility. Show an error message instead.
                _infoBarDisplayed = true;
                _infoBar = GetTextBoxControl(Resources.GpoDisabledErrorText);
                Resize += FormResized;
                Controls.Add(_infoBar);
                base.DoPreview(dataSource);

                return;
            }

            FilePreviewCommon.Helper.CleanupTempDir(_webView2UserDataFolder);

            _infoBarDisplayed = false;

            try
            {
                if (!(dataSource is string filePath))
                {
                    throw new ArgumentException($"{nameof(dataSource)} for {nameof(MarkdownPreviewHandlerControl)} must be a string but was a '{typeof(T)}'");
                }

                string fileText = File.ReadAllText(filePath);
                Regex imageTagRegex = new Regex(@"<[ ]*img.*>");
                if (imageTagRegex.IsMatch(fileText))
                {
                    _infoBarDisplayed = true;
                }

                string markdownHTML = FilePreviewCommon.MarkdownHelper.MarkdownHtml(fileText, Settings.GetTheme(), filePath, ImagesBlockedCallBack);

                _browser = new WebView2()
                {
                    Dock = DockStyle.Fill,
                    DefaultBackgroundColor = Color.Transparent,
                };

                var webView2Options = new CoreWebView2EnvironmentOptions("--block-new-web-contents");
                ConfiguredTaskAwaitable<CoreWebView2Environment>.ConfiguredTaskAwaiter
                        webView2EnvironmentAwaiter = CoreWebView2Environment
                            .CreateAsync(userDataFolder: _webView2UserDataFolder, options: webView2Options)
                            .ConfigureAwait(true).GetAwaiter();
                webView2EnvironmentAwaiter.OnCompleted(async () =>
                {
                    try
                    {
                        _webView2Environment = webView2EnvironmentAwaiter.GetResult();
                        await _browser.EnsureCoreWebView2Async(_webView2Environment).ConfigureAwait(true);
                        _browser.CoreWebView2.SetVirtualHostNameToFolderMapping(VirtualHostName, AssemblyDirectory, CoreWebView2HostResourceAccessKind.Deny);
                        _browser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;
                        _browser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
                        _browser.CoreWebView2.Settings.AreDevToolsEnabled = false;
                        _browser.CoreWebView2.Settings.AreHostObjectsAllowed = false;
                        _browser.CoreWebView2.Settings.IsGeneralAutofillEnabled = false;
                        _browser.CoreWebView2.Settings.IsPasswordAutosaveEnabled = false;
                        _browser.CoreWebView2.Settings.IsScriptEnabled = false;
                        _browser.CoreWebView2.Settings.IsWebMessageEnabled = false;

                        // Don't load any resources.
                        _browser.CoreWebView2.AddWebResourceRequestedFilter("*", CoreWebView2WebResourceContext.All);
                        _browser.CoreWebView2.WebResourceRequested += (object sender, CoreWebView2WebResourceRequestedEventArgs e) =>
                        {
                            // Show local file we've saved with the markdown contents. Block all else.
                            if (new Uri(e.Request.Uri) != _localFileURI)
                            {
                                e.Response = _browser.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Forbidden", null);
                            }
                        };

                        // WebView2.NavigateToString() limitation
                        // See https://learn.microsoft.com/dotnet/api/microsoft.web.webview2.core.corewebview2.navigatetostring?view=webview2-dotnet-1.0.864.35#remarks
                        // While testing the limit, it turned out it is ~1.5MB, so to be on a safe side we go for 1.5m bytes
                        if (markdownHTML.Length > 1_500_000)
                        {
                            string filename = _webView2UserDataFolder + "\\" + Guid.NewGuid().ToString() + ".html";
                            File.WriteAllText(filename, markdownHTML);
                            _localFileURI = new Uri(filename);
                            _browser.Source = _localFileURI;
                        }
                        else
                        {
                            _browser.NavigateToString(markdownHTML);
                        }

                        Controls.Add(_browser);

                        _browser.NavigationStarting += async (object sender, CoreWebView2NavigationStartingEventArgs args) =>
                        {
                            if (args.Uri != null && args.Uri != _localFileURI?.ToString() && args.IsUserInitiated)
                            {
                                args.Cancel = true;
                                await Launcher.LaunchUriAsync(new Uri(args.Uri));
                            }
                        };

                        if (_infoBarDisplayed)
                        {
                            _infoBar = GetTextBoxControl(Resources.BlockedImageInfoText);
                            Resize += FormResized;
                            Controls.Add(_infoBar);
                        }
                    }
                    catch (NullReferenceException)
                    {
                    }
                });

                try
                {
                    PowerToysTelemetry.Log.WriteEvent(new MarkdownFilePreviewed());
                }
                catch
                { // Should not crash if sending telemetry is failing. Ignore the exception.
                }
            }
            catch (Exception ex)
            {
                try
                {
                    PowerToysTelemetry.Log.WriteEvent(new MarkdownFilePreviewError { Message = ex.Message });
                }
                catch
                { // Should not crash if sending telemetry is failing. Ignore the exception.
                }

                Controls.Clear();
                _infoBarDisplayed = true;
                _infoBar = GetTextBoxControl(Resources.MarkdownNotPreviewedError);
                Resize += FormResized;
                Controls.Add(_infoBar);
            }
            finally
            {
                base.DoPreview(dataSource);
            }
        }

        /// <summary>
        /// Gets a textbox control.
        /// </summary>
        /// <param name="message">Message to be displayed in textbox.</param>
        /// <returns>An object of type <see cref="RichTextBox"/>.</returns>
        private RichTextBox GetTextBoxControl(string message)
        {
            RichTextBox richTextBox = new RichTextBox
            {
                Text = message,
                BackColor = Color.LightYellow,
                Multiline = true,
                Dock = DockStyle.Top,
                ReadOnly = true,
            };
            richTextBox.ContentsResized += RTBContentsResized;
            richTextBox.ScrollBars = RichTextBoxScrollBars.None;
            richTextBox.BorderStyle = BorderStyle.None;

            return richTextBox;
        }

        /// <summary>
        /// Callback when RichTextBox is resized.
        /// </summary>
        /// <param name="sender">Reference to resized control.</param>
        /// <param name="e">Provides data for the resize event.</param>
        private void RTBContentsResized(object sender, ContentsResizedEventArgs e)
        {
            RichTextBox richTextBox = (RichTextBox)sender;
            richTextBox.Height = e.NewRectangle.Height + 5;
        }

        /// <summary>
        /// Callback when form is resized.
        /// </summary>
        /// <param name="sender">Reference to resized control.</param>
        /// <param name="e">Provides data for the event.</param>
        private void FormResized(object sender, EventArgs e)
        {
            if (_infoBarDisplayed)
            {
                _infoBar.Width = Width;
            }
        }

        /// <summary>
        /// Callback when image is blocked by extension.
        /// </summary>
        private void ImagesBlockedCallBack()
        {
            _infoBarDisplayed = true;
        }
    }
}
