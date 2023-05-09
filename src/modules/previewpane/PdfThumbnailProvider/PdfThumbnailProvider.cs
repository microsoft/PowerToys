// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.
using Windows.Data.Pdf;
using Windows.Storage;
using Windows.Storage.Streams;

namespace Microsoft.PowerToys.ThumbnailHandler.Pdf
{
    /// <summary>
    /// PDF Thumbnail Provider.
    /// </summary>
    public class PdfThumbnailProvider
    {
        public PdfThumbnailProvider(string filePath)
        {
            FilePath = filePath;
        }

        /// <summary>
        /// Gets the file path to the file creating thumbnail for.
        /// </summary>
        public string FilePath { get; private set; }

        /// <summary>
        ///  The maximum dimension (width or height) thumbnail we will generate.
        /// </summary>
        private const uint MaxThumbnailSize = 10000;

        /// <summary>
        /// Generate thumbnail bitmap for provided Pdf file/stream.
        /// </summary>
        /// <param name="cx">Maximum thumbnail size, in pixels.</param>
        /// <returns>Generated bitmap</returns>
        public Bitmap GetThumbnail(uint cx)
        {
            return DoGetThumbnail(cx).Result;
        }

        /// <summary>
        /// Generate thumbnail bitmap for provided Pdf file/stream.
        /// </summary>
        /// <param name="cx">Maximum thumbnail size, in pixels.</param>
        /// <returns>Generated bitmap</returns>
        private async Task<Bitmap> DoGetThumbnail(uint cx)
        {
            if (cx == 0 || cx > MaxThumbnailSize)
            {
                return null;
            }

            if (global::PowerToys.GPOWrapper.GPOWrapper.GetConfiguredPdfThumbnailsEnabledValue() == global::PowerToys.GPOWrapper.GpoRuleConfigured.Disabled)
            {
                // GPO is disabling this utility.
                return null;
            }

            Bitmap thumbnail = null;
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(FilePath);
                var pdf = await PdfDocument.LoadFromFileAsync(file);

                if (pdf.PageCount > 0)
                {
                    using var page = pdf.GetPage(0);

                    var image = PageToImage(page, cx);

                    thumbnail = new Bitmap(image);
                }
            }
            catch (Exception)
            {
                // TODO: add logger
            }

            return thumbnail;
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
