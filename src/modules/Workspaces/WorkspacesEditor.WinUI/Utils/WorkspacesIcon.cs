// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;

namespace WorkspacesEditor.Utils
{
    internal static class WorkspacesIcon
    {
        private const int IconSize = 128;

        private static readonly Brush LightThemeIconBackground = new SolidBrush(Color.FromArgb(255, 239, 243, 251));
        private static readonly Brush LightThemeIconForeground = new SolidBrush(Color.FromArgb(255, 47, 50, 56));
        private static readonly Brush DarkThemeIconBackground = new SolidBrush(Color.FromArgb(255, 55, 55, 55));
        private static readonly Brush DarkThemeIconForeground = new SolidBrush(Color.FromArgb(255, 228, 228, 228));
        private static readonly Font IconFont = new("Aptos", 24, FontStyle.Bold);

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
                    result += word.ToUpper(CultureInfo.CurrentCulture)[0];
                }
            }

            return result;
        }

        public static Bitmap DrawIcon(string text, bool isDarkTheme)
        {
            Brush background = isDarkTheme ? DarkThemeIconBackground : LightThemeIconBackground;
            Brush foreground = isDarkTheme ? DarkThemeIconForeground : LightThemeIconForeground;
            Bitmap bitmap = new(IconSize, IconSize);

            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.FillEllipse(background, 0, 0, IconSize, IconSize);

                var textSize = graphics.MeasureString(text, IconFont);
                var state = graphics.Save();

                float scaleX = IconSize / textSize.Width;
                float scaleY = IconSize / textSize.Height;
                float scale = Math.Min(scaleX, scaleY) * 0.8f;

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
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using var fileStream = new FileStream(path, FileMode.CreateNew);
            using var memoryStream = new MemoryStream();
            WorkspacesCsharpLibrary.DrawHelper.SaveBitmap(icon, memoryStream);

            using var iconWriter = new BinaryWriter(fileStream);
            iconWriter.Write((byte)0);
            iconWriter.Write((byte)0);
            iconWriter.Write((short)1);
            iconWriter.Write((short)1);
            iconWriter.Write((byte)IconSize);
            iconWriter.Write((byte)IconSize);
            iconWriter.Write((byte)0);
            iconWriter.Write((byte)0);
            iconWriter.Write((short)0);
            iconWriter.Write((short)32);
            iconWriter.Write((int)memoryStream.Length);
            iconWriter.Write(6 + 16);
            iconWriter.Write(memoryStream.ToArray());
            iconWriter.Flush();
        }
    }
}
