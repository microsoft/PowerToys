// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using Common;
using Common.Utilities;
using Microsoft.PowerToys.PreviewHandler.Svg.Telemetry.Events;
using Microsoft.PowerToys.PreviewHandler.Svg.Utilities;
using Microsoft.PowerToys.Telemetry;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Microsoft.PowerToys.PreviewHandler.Svg
{
    /// <summary>
    /// Implementation of Control for Svg Preview Handler.
    /// </summary>
    public class SvgPreviewControl : FormHandlerControl
    {
        /// <summary>
        /// WebView2 Control to display Svg.
        /// </summary>
        private WebView2 _browser;

        /// <summary>
        /// WebView2 Environment
        /// </summary>
        private CoreWebView2Environment _webView2Environment;

        /// <summary>
        /// Name of the virtual host
        /// </summary>
        private const string VirtualHostName = "PowerToysLocalSvg";

        /// <summary>
        /// Gets the path of the current assembly.
        /// </summary>
        /// <remarks>
        /// Source: https://stackoverflow.com/a/283917/14774889
        /// </remarks>
        private static string AssemblyDirectory
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
        /// Text box to display the information about blocked elements from Svg.
        /// </summary>
        private RichTextBox _textBox;

        /// <summary>
        /// Represent if an text box info bar is added for showing message.
        /// </summary>
        private bool _infoBarAdded;

        /// <summary>
        /// Represent WebView2 user data folder path.
        /// </summary>
        private string _webView2UserDataFolder = System.Environment.GetEnvironmentVariable("USERPROFILE") +
                                "\\AppData\\LocalLow\\Microsoft\\PowerToys\\SvgPreview-Temp";

        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="dataSource">Stream reference to access source file.</param>
        public override void DoPreview<T>(T dataSource)
        {
            CleanupWebView2UserDataFolder();

            string svgData = null;
            bool blocked = false;

            try
            {
                using (var stream = new ReadonlyStream(dataSource as IStream))
                {
                    using (var reader = new StreamReader(stream))
                    {
                        svgData = reader.ReadToEnd();
                    }
                }

                blocked = SvgPreviewHandlerHelper.CheckBlockedElements(svgData);
            }
            catch (Exception ex)
            {
                PreviewError(ex, dataSource);
                return;
            }

            try
            {
                svgData = SvgPreviewHandlerHelper.AddStyleSVG(svgData);
            }
            catch (Exception ex)
            {
                PowerToysTelemetry.Log.WriteEvent(new SvgFilePreviewError { Message = ex.Message });
            }

            InvokeOnControlThread(() =>
            {
                try
                {
                    _infoBarAdded = false;

                    // Add a info bar on top of the Preview if any blocked element is present.
                    if (blocked)
                    {
                        _infoBarAdded = true;
                        AddTextBoxControl(Properties.Resource.BlockedElementInfoText);
                    }

                    AddWebViewControl(svgData);
                    Resize += FormResized;
                    base.DoPreview(dataSource);
                    PowerToysTelemetry.Log.WriteEvent(new SvgFilePreviewed());
                }
                catch (Exception ex)
                {
                    PreviewError(ex, dataSource);
                }
            });
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
        /// Adds a WebView2 Control to Control Collection.
        /// </summary>
        /// <param name="svgData">Svg to display on Browser Control.</param>
        private void AddWebViewControl(string svgData)
        {
            _browser = new WebView2();
            _browser.Dock = DockStyle.Fill;

            ConfiguredTaskAwaitable<CoreWebView2Environment>.ConfiguredTaskAwaiter
               webView2EnvironmentAwaiter = CoreWebView2Environment
                   .CreateAsync(userDataFolder: _webView2UserDataFolder)
                   .ConfigureAwait(true).GetAwaiter();
            webView2EnvironmentAwaiter.OnCompleted(() =>
            {
                InvokeOnControlThread(async () =>
                {
                    try
                    {
                        _webView2Environment = webView2EnvironmentAwaiter.GetResult();
                        await _browser.EnsureCoreWebView2Async(_webView2Environment).ConfigureAwait(true);
                        await _browser.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync("window.addEventListener('contextmenu', window => {window.preventDefault();});");
                        _browser.CoreWebView2.SetVirtualHostNameToFolderMapping(VirtualHostName, AssemblyDirectory, CoreWebView2HostResourceAccessKind.Allow);
                        _browser.CoreWebView2.Settings.AreDefaultScriptDialogsEnabled = false;

                        // WebView2.NavigateToString() limitation
                        // See https://docs.microsoft.com/en-us/dotnet/api/microsoft.web.webview2.core.corewebview2.navigatetostring?view=webview2-dotnet-1.0.864.35#remarks
                        // While testing the limit, it turned out it is ~1.5MB, so to be on a safe side we go for 1.5m bytes
                        if (svgData.Length > 1_500_000)
                        {
                            string filename = _webView2UserDataFolder + "\\" + Guid.NewGuid().ToString() + ".html";
                            File.WriteAllText(filename, svgData);
                            _browser.Source = new Uri(filename);
                        }
                        else
                        {
                            _browser.NavigateToString(svgData);
                        }

                        Controls.Add(_browser);
                    }
                    catch (Exception)
                    {
                    }
                });
            });
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

        /// <summary>
        /// Called when an error occurs during preview.
        /// </summary>
        /// <param name="exception">The exception which occurred.</param>
        /// <param name="dataSource">Stream reference to access source file.</param>
        private void PreviewError<T>(Exception exception, T dataSource)
        {
            PowerToysTelemetry.Log.WriteEvent(new SvgFilePreviewError { Message = exception.Message });
            Controls.Clear();
            _infoBarAdded = true;
            AddTextBoxControl(Properties.Resource.SvgNotPreviewedError);
            base.DoPreview(dataSource);
        }

        /// <summary>
        /// Cleanup the previously created tmp html files from svg files bigger than 2MB.
        /// </summary>
        private void CleanupWebView2UserDataFolder()
        {
            try
            {
                // Cleanup temp dir
                var dir = new DirectoryInfo(_webView2UserDataFolder);

                foreach (var file in dir.EnumerateFiles("*.html"))
                {
                    file.Delete();
                }
            }
            catch (Exception)
            {
            }
        }
    }
}
