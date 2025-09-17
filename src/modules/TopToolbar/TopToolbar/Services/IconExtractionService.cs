// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

namespace TopToolbar.Services
{
    public static class IconExtractionService
    {
        public static bool TryExtractExeIconToPng(string exePath, string targetPngPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath) ||
                    !exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPngPath)!);

                using var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon == null)
                {
                    return false;
                }

                using var bmp = icon.ToBitmap();
                bmp.Save(targetPngPath, ImageFormat.Png);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryExtractFileIconToPng(string filePath, string targetPngPath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
                {
                    return false;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(targetPngPath)!);

                using var icon = Icon.ExtractAssociatedIcon(filePath);
                if (icon == null)
                {
                    return false;
                }

                using var bmp = icon.ToBitmap();
                bmp.Save(targetPngPath, ImageFormat.Png);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
