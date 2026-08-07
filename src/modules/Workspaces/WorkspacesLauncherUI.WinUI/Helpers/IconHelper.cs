// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;

using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Management.Deployment;
using WorkspacesCsharpLibrary;

namespace WorkspacesLauncherUI.Helpers
{
    internal static class IconHelper
    {
        public static BitmapImage TryGetApplicationIcon(string path, string packageFullName, string pwaAppId)
        {
            return TryGetPackagedAppIcon(packageFullName) ??
                   TryGetPwaIcon(pwaAppId) ??
                   TryGetExecutableIcon(path) ??
                   TryLoadImageFile(Path.Combine(AppContext.BaseDirectory, "Assets", "Workspaces", "DefaultIcon.ico"));
        }

        private static BitmapImage TryGetPackagedAppIcon(string packageFullName)
        {
            if (string.IsNullOrWhiteSpace(packageFullName))
            {
                return null;
            }

            try
            {
                var package = new PackageManager().FindPackageForUser(string.Empty, packageFullName);
                return TryLoadImageFile(package?.Logo?.LocalPath);
            }
            catch (Exception ex) when (ex is ArgumentException
                                    or UnauthorizedAccessException
                                    or FileNotFoundException
                                    or IOException
                                    or InvalidOperationException
                                    or COMException)
            {
                return null;
            }
        }

        private static BitmapImage TryGetPwaIcon(string pwaAppId)
        {
            if (string.IsNullOrWhiteSpace(pwaAppId))
            {
                return null;
            }

            return TryLoadImageFile(PwaHelper.GetPwaIconFilename(pwaAppId));
        }

        private static BitmapImage TryGetExecutableIcon(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                using Icon icon = Icon.ExtractAssociatedIcon(path);
                return CreateBitmapImage(icon);
            }
            catch (Exception ex) when (ex is FileNotFoundException
                                    or UnauthorizedAccessException
                                    or Win32Exception
                                    or ArgumentException
                                    or InvalidOperationException
                                    or COMException
                                    or IOException)
            {
                return null;
            }
        }

        private static BitmapImage TryLoadImageFile(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                return null;
            }

            try
            {
                if (Path.GetExtension(path).Equals(".ico", StringComparison.OrdinalIgnoreCase))
                {
                    using Icon icon = new(path);
                    return CreateBitmapImage(icon);
                }

                using Image image = Image.FromFile(path);
                return CreateBitmapImage(image);
            }
            catch (Exception ex) when (ex is FileNotFoundException
                                    or UnauthorizedAccessException
                                    or Win32Exception
                                    or ArgumentException
                                    or InvalidOperationException
                                    or NotSupportedException
                                    or COMException
                                    or OutOfMemoryException
                                    or IOException)
            {
                return null;
            }
        }

        private static BitmapImage CreateBitmapImage(Icon icon)
        {
            if (icon is null)
            {
                return null;
            }

            using Bitmap bitmap = icon.ToBitmap();
            return CreateBitmapImage(bitmap);
        }

        private static BitmapImage CreateBitmapImage(Image image)
        {
            if (image is null)
            {
                return null;
            }

            using Bitmap bitmap = new(32, 32);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.DrawImage(image, 0, 0, 32, 32);
            }

            using MemoryStream stream = new();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;

            BitmapImage bitmapImage = new();
            bitmapImage.SetSource(stream.AsRandomAccessStream());
            return bitmapImage;
        }
    }
}
