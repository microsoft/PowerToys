// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using System;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using Common.ComInterlop;
using Common.Utilities;
using Windows.Data.Pdf;
using Windows.Storage.Streams;

namespace Microsoft.PowerToys.ThumbnailHandler.Pdf
{
    /// <summary>
    /// PDF Thumbnail Provider.
    /// </summary>
    [Guid("BCC13D15-9720-4CC4-8371-EA74A274741E")]
    [ClassInterface(ClassInterfaceType.None)]
    [ComVisible(true)]
    public class PdfThumbnailProvider : IInitializeWithStream, IThumbnailProvider
    {
        /// <summary>
        /// Gets the stream object to access file.
        /// </summary>
        public IStream Stream { get; private set; }

        /// <summary>
        ///  The maximum dimension (width or height) thumbnail we will generate.
        /// </summary>
        private const uint MaxThumbnailSize = 10000;

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

            using var dataStream = new ReadonlyStream(this.Stream as IStream);
            using var memStream = new MemoryStream();

            dataStream.CopyTo(memStream);
            memStream.Position = 0;

            // AsRandomAccessStream() extension method from System.Runtime.WindowsRuntime
            var pdf = PdfDocument.LoadFromStreamAsync(memStream.AsRandomAccessStream()).GetAwaiter().GetResult();

            if (pdf.PageCount > 0)
            {
                using var page = pdf.GetPage(0);

                var image = PageToImage(page, cx);

                using Bitmap thumbnail = new Bitmap(image);

                phbmp = thumbnail.GetHbitmap();
                pdwAlpha = WTS_ALPHATYPE.WTSAT_RGB;
            }
        }

        /// <summary>
        /// Transform the PdfPage to an Image.
        /// </summary>
        /// <param name="page">The page to transform to an Image.</param>
        /// <param name="height">The height of the page.</param>
        /// <returns>An object of type <see cref="Image"/></returns>
        private static Image PageToImage(PdfPage page, uint height)
        {
            Image imageOfPage;

            using var stream = new InMemoryRandomAccessStream();

            page.RenderToStreamAsync(stream, new PdfPageRenderOptions()
            {
                DestinationHeight = height,
            }).GetAwaiter().GetResult();

            imageOfPage = Image.FromStream(stream.AsStream());

            return imageOfPage;
        }
    }
}
