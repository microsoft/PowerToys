// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;
using ManagedCommon;

namespace Microsoft.PowerToys.ThumbnailHandler.Svg
{
    internal static class Program
    {
        private static SvgThumbnailProvider _thumbnailProvider;

        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        public static void Main(string[] args)
        {
            ApplicationConfiguration.Initialize();
            Logger.InitializeLogger("\\FileExplorer_localLow\\SvgThumbnails\\logs", true);
            if (args != null)
            {
                if (args.Length == 2)
                {
                    string filePath = args[0];
                    uint cx = Convert.ToUInt32(args[1], 10);

                    _thumbnailProvider = new SvgThumbnailProvider(filePath);
                    Bitmap thumbnail = _thumbnailProvider.GetThumbnail(cx);
                    if (thumbnail != null )
                    {
                        filePath = filePath.Replace(".svg", ".bmp");
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
