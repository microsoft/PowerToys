// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Threading.Tasks;

using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;

namespace SoundManager
{
    // Reads the pictures that belong to the Sound Manager itself.
    internal static class AppIconLoader
    {
        // The icon of the Sound Manager itself, used when the icon of an application cannot be found.
        public static BitmapImage ManagerIcon { get; } = new()
        {
            UriSource = new Uri("/Assets/SoundManager/SoundManager.ico"),
        };

        // The logo of Windows, for the sounds of Windows itself. Windows keeps it in its own folder, so it is only there when the system has it.
        public static async Task<ImageSource> LoadWindowsLogoAsync()
        {
            var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
            if (string.IsNullOrEmpty(windows))
            {
                return null;
            }

            return await LoadImageAsync(Path.Combine(windows, "System32", "@WLOGO_96x96.png"));
        }

        private static async Task<ImageSource> LoadImageAsync(string path)
        {
            try
            {
                var file = await StorageFile.GetFileFromPathAsync(path);
                using var stream = await file.OpenReadAsync();

                var decoder = await BitmapDecoder.CreateAsync(stream);
                var bitmap = await decoder.GetSoftwareBitmapAsync(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied);

                var source = new SoftwareBitmapSource();
                await source.SetBitmapAsync(bitmap);
                return source;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
