// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;

namespace FileActionsMenu.Ui.Helpers
{
    public static class Extensions
    {
        /// <summary>
        /// Returns <typeparamref name="T"/> if it is not null, otherwise throws an <see cref="ArgumentNullException"/>.
        /// </summary>
        /// <typeparam name="T">The type of the value.</typeparam>
        /// <param name="value">The value to check and return.</param>
        /// <returns>The specified value.</returns>
        /// <exception cref="ArgumentNullException">When <paramref name="value"/> is null.</exception>
        public static T GetOrArgumentNullException<T>(this T? value)
        {
            return value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Checks whether the file is an image.
        /// </summary>
        /// <param name="fileName">A filename or a file path.</param>
        /// <returns>If the file has an image file extension.</returns>
        public static bool IsImage(this string fileName)
        {
            string extension = System.IO.Path.GetExtension(fileName).ToLowerInvariant();
            return extension switch
            {
                ".bmp" => true,
                ".dib" => true,
                ".exif" => true,
                ".gif" => true,
                ".jfif" => true,
                ".jpe" => true,
                ".jpeg" => true,
                ".jpg" => true,
                ".jxr" => true,
                ".png" => true,
                ".rle" => true,
                ".tif" => true,
                ".tiff" => true,
                ".wdp" => true,
                _ => false,
            };
        }
    }
}
