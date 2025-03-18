// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Xml.Linq;
using Common.Utilities;
using ManagedCommon;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Microsoft.PowerToys.ThumbnailHandler.Svg
{
    /// <summary>
    /// SVG Thumbnail Provider.
    /// </summary>
    public class SvgThumbnailProvider : IDisposable
    {
        public SvgThumbnailProvider(string filePath)
        {
            FilePath = filePath;
            if (FilePath != null && File.Exists(FilePath))
            {
                Stream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            }
        }

        /// <summary>
        /// Gets the file path to the file creating thumbnail for.
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        /// Gets the stream object to access file.
        /// </summary>
        public Stream Stream { get; private set; }

        /// <summary>
        /// Gets or sets signalled when the main thread can use preprocessed svg contents.
        /// </summary>
        public ManualResetEventSlim SvgContentsReady { get; set; } = new ManualResetEventSlim(false);

        public string SvgContents { get; set; } = string.Empty;

        /// <summary>
        ///  The maximum dimension (width or height) thumbnail we will generate.
        /// </summary>
        private const uint MaxThumbnailSize = 10000;

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
        private const string VirtualHostName = "PowerToysLocalSvgThumbnail";

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
        /// Represent WebView2 user data folder path.
        /// </summary>
        private string _webView2UserDataFolder = System.Environment.GetEnvironmentVariable("USERPROFILE") +
                                    "\\AppData\\LocalLow\\Microsoft\\PowerToys\\SvgThumbnailPreview-Temp";

        /// <summary>
        /// Render SVG using WebView2 control, capture the WebView2
        /// preview and create Bitmap out of it.
        /// </summary>
        /// <param name="cx">The maximum thumbnail size, in pixels.</param>
        public Bitmap GetThumbnailImpl(uint cx)
        {
            CleanupWebView2UserDataFolder();

            if (cx == 0 || cx > MaxThumbnailSize)
            {
                return null;
            }

            Bitmap thumbnail = null;

            var thumbnailDone = new ManualResetEventSlim(false);

            _browser = new WebView2();
            _browser.Dock = DockStyle.Fill;
            _browser.Visible = true;
            _browser.Width = (int)cx;
            _browser.Height = (int)cx;
            _browser.NavigationCompleted += async (object sender, CoreWebView2NavigationCompletedEventArgs args) =>
            {
                try
                {
                    // Check if the SVG element is present
                    var a = await _browser.ExecuteScriptAsync($"document.getElementsByTagName('svg')[0].viewBox;");
                    if (a != null)
                    {
                        var svgContent = SvgContents.Substring(SvgContents.IndexOf("<svg", StringComparison.OrdinalIgnoreCase), SvgContents.IndexOf("</svg>", StringComparison.OrdinalIgnoreCase) - SvgContents.IndexOf("<svg", StringComparison.OrdinalIgnoreCase) + "</svg>".Length);

                        Dictionary<string, string> styleDict = new Dictionary<string, string>();

                        // Try to parse the SVG content
                        try
                        {
                            // Attempt to parse the svgContent
                            var svgDocument = XDocument.Parse(svgContent);
                            var svgElement = svgDocument.Root;
                            var currentStyle = svgElement?.Attribute("style")?.Value;

                            // If style attribute exists, preserve existing styles
                            if (!string.IsNullOrEmpty(currentStyle) && currentStyle != "null")
                            {
                                styleDict = currentStyle
                                    .Split(';', StringSplitOptions.RemoveEmptyEntries)
                                    .Select(stylePart => stylePart.Split(':', 2, StringSplitOptions.TrimEntries))
                                    .Where(styleKeyValue => styleKeyValue.Length == 2 && !string.IsNullOrEmpty(styleKeyValue[0]))
                                    .ToDictionary(
                                        styleKeyValue => styleKeyValue[0],
                                        styleKeyValue => styleKeyValue[1]);
                            }
                        }
                        catch (Exception ex)
                        {
                            // Log the error if the SVG content is not valid or parsing fails
                            Logger.LogError($"Failed to parse SVG content: {ex.Message}");
                        }

                        // Add or replace width and height in the existing style
                        styleDict["width"] = "100%";
                        styleDict["height"] = "100%";

                        // Construct a single JavaScript string to set all properties
                        var styleScript = string.Join(";", styleDict.Select(kv => $"document.getElementsByTagName('svg')[0].style.setProperty('{kv.Key}', '{kv.Value}');"));

                        // Apply the new style attributes using the constructed script
                        await _browser.ExecuteScriptAsync(styleScript);
                    }

                    // Hide scrollbar - fixes #18286
                    await _browser.ExecuteScriptAsync("document.querySelector('body').style.overflow='hidden'");

                    MemoryStream ms = new MemoryStream();
                    await _browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png, ms);
                    thumbnail = new Bitmap(ms);

                    if (thumbnail.Width != cx && thumbnail.Height != cx && thumbnail.Width != 0 && thumbnail.Height != 0)
                    {
                        // We are not the appropriate size for caller.  Resize now while
                        // respecting the aspect ratio.
                        float scale = Math.Min((float)cx / thumbnail.Width, (float)cx / thumbnail.Height);
                        int scaleWidth = (int)(thumbnail.Width * scale);
                        int scaleHeight = (int)(thumbnail.Height * scale);
                        thumbnail = ResizeImage(thumbnail, scaleWidth, scaleHeight);
                    }

                    thumbnailDone.Set();
                }
                catch (Exception ex)
                {
                    Logger.LogError("Error during NavigationCompleted: ", ex);
                    thumbnailDone.Set();
                }
            };

            var webView2Options = new CoreWebView2EnvironmentOptions("--block-new-web-contents");
            ConfiguredTaskAwaitable<CoreWebView2Environment>.ConfiguredTaskAwaiter
                webView2EnvironmentAwaiter = CoreWebView2Environment
                    .CreateAsync(userDataFolder: _webView2UserDataFolder, options: webView2Options)
                    .ConfigureAwait(true).GetAwaiter();

            webView2EnvironmentAwaiter.OnCompleted(async () =>
            {
                for (int attempt = 0; attempt < 3; attempt++)
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
                            // Show local file we've saved with the svg contents. Block all else.
                            if (new Uri(e.Request.Uri) != _localFileURI)
                            {
                                e.Response = _browser.CoreWebView2.Environment.CreateWebResourceResponse(null, 403, "Forbidden", null);
                            }
                        };

                        // WebView2.NavigateToString() limitation
                        // See https://learn.microsoft.com/dotnet/api/microsoft.web.webview2.core.corewebview2.navigatetostring?view=webview2-dotnet-1.0.864.35#remarks
                        // While testing the limit, it turned out it is ~1.5MB, so to be on a safe side we go for 1.5m bytes
                        SvgContentsReady.Wait();
                        if (string.IsNullOrEmpty(SvgContents) || !SvgContents.Contains("svg"))
                        {
                            thumbnailDone.Set();
                            return;
                        }

                        if (SvgContents.Length > 1_500_000)
                        {
                            string filename = _webView2UserDataFolder + "\\" + Guid.NewGuid().ToString() + ".html";
                            File.WriteAllText(filename, SvgContents);
                            _localFileURI = new Uri(filename);
                            _browser.Source = _localFileURI;
                        }
                        else
                        {
                            _browser.NavigateToString(SvgContents);
                        }

                        break; // Exit the retry loop if initialization succeeds
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Initialization attempt {attempt} failed: {ex.Message}");
                        if (attempt == 2)
                        {
                            Logger.LogError($"Failed running webView2Environment completed for {FilePath} : ", ex);
                            thumbnailDone.Set();
                            return;
                        }

                        await Task.Delay(1000); // Delay before retrying
                    }
                }
            });

            while (!thumbnailDone.Wait(75))
            {
                Application.DoEvents();
            }

            _browser.Dispose();

            return thumbnail;
        }

        /// <summary>
        /// Wrap the SVG markup in HTML with a meta tag to render it
        /// using WebView2 control.
        /// We also set the padding and margin for the body to zero as
        /// there is a default margin of 8.
        /// </summary>
        /// <param name="svg">The original SVG markup.</param>
        /// <returns>The SVG content wrapped in HTML.</returns>
        public static string WrapSVGInHTML(string svg)
        {
            string html = @"
                <head>
                  <meta http-equiv='X-UA-Compatible' content='IE=Edge'>
                </head>
                <html>
                  <body style='padding:0px;margin:0px;' scroll='no'>
                    {0}
                  </body>
                </html>";

            // Using InvariantCulture since this should be displayed as it is
            return string.Format(CultureInfo.InvariantCulture, html, svg);
        }

        /// <summary>
        /// Resize the image with high quality to the specified width and height.
        /// </summary>
        /// <param name="image">The image to resize.</param>
        /// <param name="width">The width to resize to.</param>
        /// <param name="height">The height to resize to.</param>
        /// <returns>The resized image.</returns>
        public static Bitmap ResizeImage(Image image, int width, int height)
        {
            if (width <= 0 ||
                height <= 0 ||
                width > MaxThumbnailSize ||
                height > MaxThumbnailSize ||
                image == null)
            {
                return null;
            }

            Bitmap destImage = new Bitmap(width, height);

            destImage.SetResolution(image.HorizontalResolution, image.VerticalResolution);

            using (var graphics = Graphics.FromImage(destImage))
            {
                graphics.CompositingMode = CompositingMode.SourceCopy;
                graphics.CompositingQuality = CompositingQuality.HighQuality;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;

                graphics.Clear(Color.White);
                graphics.DrawImage(image, 0, 0, width, height);
            }

            return destImage;
        }

        /// <summary>
        /// Generate thumbnail bitmap for provided Gcode file/stream.
        /// </summary>
        /// <param name="cx">Maximum thumbnail size, in pixels.</param>
        /// <returns>Generated bitmap</returns>
        public Bitmap GetThumbnail(uint cx)
        {
            if (cx == 0 || cx > MaxThumbnailSize)
            {
                return null;
            }

            if (global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredSvgThumbnailsEnabledValue() == global::PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                // GPO is disabling this utility.
                return null;
            }

            if (Stream != null)
            {
                new Thread(() =>
                {
                    string svgData = null;
                    using (var reader = new StreamReader(Stream))
                    {
                        svgData = reader.ReadToEnd();
                        try
                        {
                            // Fixes #17527 - Inkscape v1.1 swapped order of default and svg namespaces in svg file (default first, svg after).
                            // That resulted in parser being unable to parse it correctly and instead of svg, text was previewed.
                            // MS Edge and Firefox also couldn't preview svg files with mentioned order of namespaces definitions.
                            svgData = SvgPreviewHandlerHelper.SwapNamespaces(svgData);
                            svgData = SvgPreviewHandlerHelper.AddStyleSVG(svgData);
                            SvgContents = WrapSVGInHTML(svgData);
                            SvgContentsReady.Set();
                        }
                        catch (Exception)
                        {
                            SvgContentsReady.Set();
                        }
                    }
                }).Start();
            }
            else
            {
                SvgContentsReady.Set();
            }

            using (Bitmap thumbnail = GetThumbnailImpl(cx))
            {
                if (thumbnail != null && thumbnail.Size.Width > 0 && thumbnail.Size.Height > 0)
                {
                    return (Bitmap)thumbnail.Clone();
                }
            }

            return null;
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
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
