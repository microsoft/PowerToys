// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace TopToolbar.Services
{
    internal static class IconStorageService
    {
        public static string SaveIconFromFile(string sourcePath, string preferredName = "")
        {
            if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
            {
                throw new FileNotFoundException("Icon source file not found.", sourcePath);
            }

            var extension = Path.GetExtension(sourcePath);
            if (string.IsNullOrWhiteSpace(extension))
            {
                extension = ".png";
            }

            using var stream = File.OpenRead(sourcePath);
            return SaveIconFromStream(stream, extension, string.IsNullOrWhiteSpace(preferredName) ? Path.GetFileNameWithoutExtension(sourcePath) : preferredName);
        }

        public static string SaveIconFromStream(Stream sourceStream, string extension, string preferredName = "")
        {
            ArgumentNullException.ThrowIfNull(sourceStream);
            extension = NormalizeExtension(extension);
            var safeName = SanitizeName(string.IsNullOrWhiteSpace(preferredName) ? "icon" : preferredName.Trim());
            var fileName = string.Concat(safeName, "_", DateTime.UtcNow.ToString("yyyyMMddHHmmssfff", CultureInfo.InvariantCulture), extension);
            Directory.CreateDirectory(AppPaths.IconsDirectory);
            var targetPath = Path.Combine(AppPaths.IconsDirectory, fileName);
            using var target = File.Create(targetPath);
            if (sourceStream.CanSeek)
            {
                sourceStream.Position = 0;
            }

            sourceStream.CopyTo(target);
            return targetPath;
        }

        public static string SaveSvg(string svgContent, string preferredName = "")
        {
            if (string.IsNullOrWhiteSpace(svgContent))
            {
                throw new ArgumentException("SVG content cannot be empty.", nameof(svgContent));
            }

            var bytes = Encoding.UTF8.GetBytes(svgContent);
            using var memory = new MemoryStream(bytes, writable: false);
            return SaveIconFromStream(memory, ".svg", preferredName);
        }

        private static string NormalizeExtension(string extension)
        {
            if (string.IsNullOrWhiteSpace(extension))
            {
                return ".png";
            }

            var ext = extension.Trim();

            if (!ext.StartsWith('.'))
            {
                ext = "." + ext;
            }

            return ext.ToLowerInvariant();
        }

        private static string SanitizeName(string name)
        {
            var invalid = Path.GetInvalidFileNameChars();
            var safeChars = name.Select(c => invalid.Contains(c) ? '-' : c).ToArray();
            var sanitized = new string(safeChars);
            return string.IsNullOrWhiteSpace(sanitized) ? "icon" : sanitized;
        }
    }
}
