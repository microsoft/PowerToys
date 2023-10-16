// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using Common;
using Common.Utilities;
using Microsoft.PowerToys.PreviewHandler.Svg.Telemetry.Events;
using Microsoft.PowerToys.Telemetry;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using SvgPreviewHandler;

namespace Microsoft.PowerToys.PreviewHandler.Svg
{
    /// <summary>
    /// Implementation of Control for Svg Preview Handler.
    /// </summary>
    public class SvgPreviewControl : FormHandlerControl
    {
        /// <summary>
        /// Settings class
        /// </summary>
        private readonly SvgPreviewHandler.Settings _settings = new();

        /// <summary>
        /// Generator for the actual preview file
        /// </summary>
        private readonly SvgHTMLPreviewGenerator _previewGenerator = new();

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
        /// URI of the local file saved with the contents
        /// </summary>
        private Uri _localFileURI;

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

        public SvgPreviewControl()
        {
            this.SetBackgroundColor(_settings.ThemeColor);
        }

        /// <summary>
        /// Start the preview on the Control.
        /// </summary>
        /// <param name="dataSource">Stream reference to access source file.</param>
        public override void DoPreview<T>(T dataSource)
        {
            if (global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredSvgPreviewEnabledValue() == global::PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                // GPO is disabling this utility. Show an error message instead.
                _infoBarAdded = true;
                AddTextBoxControl(Properties.Resource.GpoDisabledErrorText);
                Resize += FormResized;
                base.DoPreview(dataSource);

                return;
            }

            CleanupWebView2UserDataFolder();

            string svgData = null;
            bool blocked = false;

            try
            {
                if (!(dataSource is string filePath))
                {
                    throw new ArgumentException($"{nameof(dataSource)} for {nameof(SvgPreviewControl)} must be a string but was a '{typeof(T)}'");
                }

                using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
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
                // Fixes #17527 - Inkscape v1.1 swapped order of default and svg namespaces in svg file (default first, svg after).
                // That resulted in parser being unable to parse it correctly and instead of svg, text was previewed.
                // MS Edge and Firefox also couldn't preview svg files with mentioned order of namespaces definitions.
                svgData = SvgPreviewHandlerHelper.SwapNamespaces(svgData);
                svgData = SvgPreviewHandlerHelper.AddStyleSVG(svgData);
            }
            catch (Exception ex)
            {
                try
                {
                    PowerToysTelemetry.Log.WriteEvent(new SvgFilePreviewError { Message = ex.Message });
                }
                catch
                { // Should not crash if sending telemetry is failing. Ignore the exception.
                }
            }

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
                try
                {
                    PowerToysTelemetry.Log.WriteEvent(new SvgFilePreviewed());
                }
                catch
                { // Should not crash if sending telemetry is failing. Ignore the exception.
                }
            }
            catch (Exception ex)
            {
                PreviewError(ex, dataSource);
            }
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

        // Disable loading resources.
        private void CoreWebView2_BlockExternalResources(object sender, CoreWebView2WebResourceRequestedEventArgs e)
        {
            // Show local file we've saved with the svg contents. Block all else.
            if (new Uri(e.Request.Uri) != _localFileURI)
            {
                e.Response = _browser.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Forbidden", null);
            }
        }

        /// <summary>
        /// Adds a WebView2 Control to Control Collection.
        /// </summary>
        /// <param name="svgData">Svg to display on Browser Control.</param>
        private void AddWebViewControl(string svgData)
        {
            _browser = new WebView2();
            _browser.DefaultBackgroundColor = Color.Transparent;
            _browser.Dock = DockStyle.Fill;

            // Prevent new windows from being opened.
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
                    _browser.CoreWebView2.WebResourceRequested += CoreWebView2_BlockExternalResources;

                    string generatedPreview = _previewGenerator.GeneratePreview(svgData);

                    // WebView2.NavigateToString() limitation
                    // See https://learn.microsoft.com/dotnet/api/microsoft.web.webview2.core.corewebview2.navigatetostring?view=webview2-dotnet-1.0.864.35#remarks
                    // While testing the limit, it turned out it is ~1.5MB, so to be on a safe side we go for 1.5m bytes
                    if (generatedPreview.Length > 1_500_000)
                    {
                        string filename = _webView2UserDataFolder + "\\" + Guid.NewGuid().ToString() + ".html";
                        File.WriteAllText(filename, generatedPreview);
                        _localFileURI = new Uri(filename);
                        _browser.Source = _localFileURI;
                    }
                    else
                    {
                        _browser.NavigateToString(generatedPreview);
                    }

                    Controls.Add(_browser);
                }
                catch (Exception)
                {
                }
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
            try
            {
                PowerToysTelemetry.Log.WriteEvent(new SvgFilePreviewError { Message = exception.Message });
            }
            catch
            { // Should not crash if sending telemetry is failing. Ignore the exception.
            }

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
