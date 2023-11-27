// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Globalization;

namespace Microsoft.PowerToys.ThumbnailHandler.Svg
{
    internal static class Program
    {
        private static SvgThumbnailProvider _thumbnailProvider;

        private static Mutex mutex = new Mutex(true, "PowerToys_SvgThumbnailProvider_477AA096-FFFA-42CC-A9D0-C0FF6EC09496");

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
                    if (mutex.WaitOne(TimeSpan.Zero, true))
                    {
                        try
                        {
                            // do the app code
                            string filePath = args[0];
                            uint cx = Convert.ToUInt32(args[1], 10);

                            _thumbnailProvider = new SvgThumbnailProvider(filePath);
                            Bitmap thumbnail = _thumbnailProvider.GetThumbnail(cx);
                            if (thumbnail != null)
                            {
                                filePath = filePath.Replace(".svg", ".bmp");
                                thumbnail.Save(filePath, System.Drawing.Imaging.ImageFormat.Bmp);
                            }
                        }
                        catch
                        {
                            // To ensure mutex is released if some unexpected exception happens.
                        }

                        mutex.ReleaseMutex();
                        mutex.Dispose();
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
