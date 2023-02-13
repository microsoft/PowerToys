// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.PowerToys.ThumbnailHandler.Pdf
{
    internal static class Program
    {
        private static PdfThumbnailProvider _thumbnailProvider;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            if (args != null)
            {
                if (args.Length == 2)
                {
                    string filePath = args[0];
                    uint cx = Convert.ToUInt32(args[1], 10);

                    _thumbnailProvider = new PdfThumbnailProvider(filePath);
                    Bitmap thumbnail = _thumbnailProvider.GetThumbnail(cx);
                    if (thumbnail != null)
                    {
                        filePath = filePath.Replace(".pdf", ".bmp");
                        thumbnail.Save(filePath, System.Drawing.Imaging.ImageFormat.Bmp);
                    }
                }
                else
                {
                    MessageBox.Show("Gcode thumbnail - wrong number of args: " + args.Length.ToString(CultureInfo.InvariantCulture));
                }
            }
        }
    }
}
