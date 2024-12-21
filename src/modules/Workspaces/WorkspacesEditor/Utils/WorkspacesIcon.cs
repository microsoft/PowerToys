// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

using ManagedCommon;

namespace WorkspacesEditor.Utils
{
    public class WorkspacesIcon : IDisposable
    {
        private const int IconSize = 128;

        public static readonly Brush LightThemeIconBackground = new SolidBrush(Color.FromArgb(255, 239, 243, 251));
        public static readonly Brush LightThemeIconForeground = new SolidBrush(Color.FromArgb(255, 47, 50, 56));
        public static readonly Brush DarkThemeIconBackground = new SolidBrush(Color.FromArgb(255, 55, 55, 55));
        public static readonly Brush DarkThemeIconForeground = new SolidBrush(Color.FromArgb(255, 228, 228, 228));

        public static readonly Font IconFont = new("Aptos", 24, FontStyle.Bold);

        public static string IconTextFromProjectName(string projectName)
        {
            string result = string.Empty;
            char[] delimiterChars = { ' ', ',', '.', ':', '-', '\t' };
            string[] words = projectName.Split(delimiterChars);

            foreach (string word in words)
            {
                if (string.IsNullOrEmpty(word))
                {
                    continue;
                }

                if (word.All(char.IsDigit))
                {
                    result += word;
                }
                else
                {
                    result += word.ToUpper(System.Globalization.CultureInfo.CurrentCulture).ToCharArray()[0];
                }
            }

            return result;
        }

        public static Bitmap DrawIcon(string text, Theme currentTheme)
        {
            Brush background = currentTheme == Theme.Dark ? DarkThemeIconBackground : LightThemeIconBackground;
            Brush foreground = currentTheme == Theme.Dark ? DarkThemeIconForeground : LightThemeIconForeground;
            Bitmap bitmap = new Bitmap(IconSize, IconSize);

            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.FillEllipse(background, 0, 0, IconSize, IconSize);

                var textSize = graphics.MeasureString(text, IconFont);
                var state = graphics.Save();

                // Calculate scaling factors
                float scaleX = (float)IconSize / textSize.Width;
                float scaleY = (float)IconSize / textSize.Height;
                float scale = Math.Min(scaleX, scaleY) * 0.8f;  // Use the smaller scale factor to maintain aspect ratio

                // Calculate the position to center the text
                float textX = (IconSize - (textSize.Width * scale)) / 2;
                float textY = ((IconSize - (textSize.Height * scale)) / 2) + 6;

                graphics.TranslateTransform(textX, textY);
                graphics.ScaleTransform(scale, scale);
                graphics.DrawString(text, IconFont, foreground, 0, 0);
                graphics.Restore(state);
            }

            return bitmap;
        }

        public static void SaveIcon(Bitmap icon, string path)
        {
            if (Path.Exists(path))
            {
                File.Delete(path);
            }

            FileStream fileStream = new FileStream(path, FileMode.CreateNew);
            using (var memoryStream = new MemoryStream())
            {
                WorkspacesCsharpLibrary.DrawHelper.SaveBitmap(icon, memoryStream);

                BinaryWriter iconWriter = new BinaryWriter(fileStream);
                if (fileStream != null && iconWriter != null)
                {
                    // 0-1 reserved, 0
                    iconWriter.Write((byte)0);
                    iconWriter.Write((byte)0);

                    // 2-3 image type, 1 = icon, 2 = cursor
                    iconWriter.Write((short)1);

                    // 4-5 number of images
                    iconWriter.Write((short)1);

                    // image entry 1
                    // 0 image width
                    iconWriter.Write((byte)IconSize);

                    // 1 image height
                    iconWriter.Write((byte)IconSize);

                    // 2 number of colors
                    iconWriter.Write((byte)0);

                    // 3 reserved
                    iconWriter.Write((byte)0);

                    // 4-5 color planes
                    iconWriter.Write((short)0);

                    // 6-7 bits per pixel
                    iconWriter.Write((short)32);

                    // 8-11 size of image data
                    iconWriter.Write((int)memoryStream.Length);

                    // 12-15 offset of image data
                    iconWriter.Write((int)(6 + 16));

                    // write image data
                    // png data must contain the whole png data file
                    iconWriter.Write(memoryStream.ToArray());

                    iconWriter.Flush();
                }
            }

            fileStream.Flush();
            fileStream.Close();
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
