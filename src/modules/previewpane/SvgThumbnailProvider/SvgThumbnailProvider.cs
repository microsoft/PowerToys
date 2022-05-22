// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using Common.ComInterlop;
using Common.Utilities;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace Microsoft.PowerToys.ThumbnailHandler.Svg
{
    /// <summary>
    /// SVG Thumbnail Provider.
    /// </summary>
    [Guid("36B27788-A8BB-4698-A756-DF9F11F64F84")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class SvgThumbnailProvider : IInitializeWithStream, IThumbnailProvider, IDisposable
    {
        /// <summary>
        /// Gets the stream object to access file.
        /// </summary>
        public IStream Stream { get; private set; }

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
        /// <param name="content">The content to render.</param>
        /// <param name="cx">The maximum thumbnail size, in pixels.</param>
        public Bitmap GetThumbnail(string content, uint cx)
        {
            CleanupWebView2UserDataFolder();

            if (cx == 0 || cx > MaxThumbnailSize || string.IsNullOrEmpty(content) || !content.Contains("svg"))
            {
                return null;
            }

            Bitmap thumbnail = null;
            bool thumbnailDone = false;
            string wrappedContent = WrapSVGInHTML(content);

            _browser = new WebView2();
            _browser.Dock = DockStyle.Fill;
            _browser.Visible = true;
            _browser.Width = (int)cx;
            _browser.Height = (int)cx;
            _browser.NavigationCompleted += async (object sender, CoreWebView2NavigationCompletedEventArgs args) =>
            {
                var a = await _browser.ExecuteScriptAsync($"document.getElementsByTagName('svg')[0].viewBox;");
                if (a != null)
                {
                    await _browser.ExecuteScriptAsync($"document.getElementsByTagName('svg')[0].style = 'width:100%;height:100%';");
                }

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

                thumbnailDone = true;
            };

            ConfiguredTaskAwaitable<CoreWebView2Environment>.ConfiguredTaskAwaiter
               webView2EnvironmentAwaiter = CoreWebView2Environment
                   .CreateAsync(userDataFolder: _webView2UserDataFolder)
                   .ConfigureAwait(true).GetAwaiter();
            webView2EnvironmentAwaiter.OnCompleted(async () =>
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
                    if (wrappedContent.Length > 1_500_000)
                    {
                        string filename = _webView2UserDataFolder + "\\" + Guid.NewGuid().ToString() + ".html";
                        File.WriteAllText(filename, wrappedContent);
                        _browser.Source = new Uri(filename);
                    }
                    else
                    {
                        _browser.NavigateToString(wrappedContent);
                    }
                }
                catch (Exception)
                {
                }
            });

            while (thumbnailDone == false)
            {
                Application.DoEvents();
            }

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

        /// <inheritdoc/>
        public void Initialize(IStream pstream, uint grfMode)
        {
            // Ignore the grfMode always use read mode to access the file.
            this.Stream = pstream;
        }

        /// <inheritdoc/>
        public void GetThumbnail(uint cx, out IntPtr phbmp, out WTS_ALPHATYPE pdwAlpha)
        {
            phbmp = IntPtr.Zero;
            pdwAlpha = WTS_ALPHATYPE.WTSAT_UNKNOWN;

            if (cx == 0 || cx > MaxThumbnailSize)
            {
                return;
            }

            string svgData = null;
            using (var stream = new ReadonlyStream(this.Stream as IStream))
            {
                using (var reader = new StreamReader(stream))
                {
                    svgData = reader.ReadToEnd();
                }
            }

            if (svgData != null)
            {
                using (Bitmap thumbnail = GetThumbnail(svgData, cx))
                {
                    if (thumbnail != null && thumbnail.Size.Width > 0 && thumbnail.Size.Height > 0)
                    {
                        phbmp = thumbnail.GetHbitmap();
                        pdwAlpha = WTS_ALPHATYPE.WTSAT_RGB;
                    }
                }
            }
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
