// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows.Forms;
using Common.ComInterlop;
using Common.Utilities;
using Microsoft.PowerToys.Telemetry;
using PreviewHandlerCommon;

namespace Microsoft.PowerToys.ThumbnailHandler.Svg
{
    /// <summary>
    /// SVG Thumbnail Provider.
    /// </summary>
    [Guid("36B27788-A8BB-4698-A756-DF9F11F64F84")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class SvgThumbnailProvider : IInitializeWithStream, IThumbnailProvider
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
        /// Captures an image representation of browser contents.
        /// </summary>
        /// <param name="browser">The WebBrowser instance rendering the SVG.</param>
        /// <param name="rectangle">The client rectangle to capture from.</param>
        /// <param name="backgroundColor">The default background color to apply.</param>
        /// <returns>A Bitmap representing the browser contents.</returns>
        public static Bitmap GetBrowserContentImage(WebBrowser browser, Rectangle rectangle, Color backgroundColor)
        {
            Bitmap image = new Bitmap(rectangle.Width, rectangle.Height);
            using (Graphics graphics = Graphics.FromImage(image))
            {
                IntPtr deviceContextHandle = IntPtr.Zero;
                RECT rect = new RECT
                {
                    Left = rectangle.Left,
                    Top = rectangle.Top,
                    Right = rectangle.Right,
                    Bottom = rectangle.Bottom,
                };

                graphics.Clear(backgroundColor);

                try
                {
                    deviceContextHandle = graphics.GetHdc();

                    IViewObject viewObject = browser?.ActiveXInstance as IViewObject;
                    viewObject.Draw(1, -1, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero, deviceContextHandle, ref rect, IntPtr.Zero, IntPtr.Zero, 0);
                }
                finally
                {
                    if (deviceContextHandle != IntPtr.Zero)
                    {
                        graphics.ReleaseHdc(deviceContextHandle);
                    }
                }
            }

            return image;
        }

        /// <summary>
        /// Wrap the SVG markup in HTML with a meta tag to ensure the
        /// WebBrowser control is in Edge mode to enable SVG rendering.
        /// We also set the padding and margin for the body to zero as
        /// there is a default margin of 8.
        /// </summary>
        /// <param name="content">The content to render.</param>
        /// <param name="cx">The maximum thumbnail size, in pixels.</param>
        /// <returns>A thumbnail of the rendered content.</returns>
        public static Bitmap GetThumbnail(string content, uint cx)
        {
            if (cx > MaxThumbnailSize)
            {
                return null;
            }

            Bitmap thumbnail = null;

            // Wrap the SVG content in HTML in IE Edge mode so we can ensure
            // we render properly.
            string wrappedContent = WrapSVGInHTML(content);
            using (WebBrowserExt browser = new WebBrowserExt())
            {
                browser.Dock = DockStyle.Fill;
                browser.IsWebBrowserContextMenuEnabled = false;
                browser.ScriptErrorsSuppressed = true;
                browser.ScrollBarsEnabled = false;
                browser.AllowNavigation = false;
                browser.Width = (int)cx;
                browser.Height = (int)cx;
                browser.DocumentText = wrappedContent;

                // Wait for the browser to render the content.
                while (browser.IsBusy || browser.ReadyState != WebBrowserReadyState.Complete)
                {
                    Application.DoEvents();
                }

                // Check size of the rendered SVG.
                var svg = browser.Document.GetElementsByTagName("svg").Cast<HtmlElement>().FirstOrDefault();
                if (svg != null)
                {
                    var viewBox = svg.GetAttribute("viewbox");
                    if (viewBox != null)
                    {
                        // Update the svg style to override any width or height explicit settings
                        // Setting to 100% width and height will allow to scale to our intended size
                        // Otherwise, we would end up with a scaled up blurry image.
                        svg.Style = "width:100%;height:100%";

                        // Wait for the browser to render the content.
                        while (browser.IsBusy || browser.ReadyState != WebBrowserReadyState.Complete)
                        {
                            Application.DoEvents();
                        }
                    }

                    // Update the size of the browser control to fit the SVG
                    // in the visible viewport.
                    browser.Width = svg.OffsetRectangle.Width;
                    browser.Height = svg.OffsetRectangle.Height;

                    // Wait for the browser to render the content.
                    while (browser.IsBusy || browser.ReadyState != WebBrowserReadyState.Complete)
                    {
                        Application.DoEvents();
                    }

                    // Capture the image of the SVG from the browser.
                    thumbnail = GetBrowserContentImage(browser, svg.OffsetRectangle, Color.White);
                    if (thumbnail.Width != cx && thumbnail.Height != cx)
                    {
                        // We are not the appropriate size for caller.  Resize now while
                        // respecting the aspect ratio.
                        float scale = Math.Min((float)cx / thumbnail.Width, (float)cx / thumbnail.Height);
                        int scaleWidth = (int)(thumbnail.Width * scale);
                        int scaleHeight = (int)(thumbnail.Height * scale);
                        thumbnail = ResizeImage(thumbnail, scaleWidth, scaleHeight);
                    }
                }
            }

            return thumbnail;
        }

        /// <summary>
        /// Wrap the SVG markup in HTML with a meta tag to ensure the
        /// WebBrowser control is in Edge mode to enable SVG rendering.
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
    }
}
