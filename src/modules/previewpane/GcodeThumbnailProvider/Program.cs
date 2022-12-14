// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.PowerToys.ThumbnailHandler.Gcode
{
    using System.Globalization;

    internal static class Program
    {
        private static GcodeThumbnailProvider _thumbnailProvider;

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

                    _thumbnailProvider = new GcodeThumbnailProvider(filePath);
                    Bitmap thumbnail = _thumbnailProvider.GetThumbnail(cx);
                    filePath = filePath.Replace(".gcode", ".bmp");
                    thumbnail.Save(filePath, System.Drawing.Imaging.ImageFormat.Bmp);
                }
                else
                {
                    MessageBox.Show("Gcode thumbnail - wrong number of args: " + args.Length.ToString(CultureInfo.InvariantCulture));
                }
            }
        }
    }
}
