// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using System.Windows.Media.Imaging;
using Wox.Infrastructure.Image;

namespace Microsoft.PowerToys.Run.Plugin.WindowsTerminal
{
    public class TerminalPackage
    {
        public string AppUserModelId { get; }

        public Version Version { get; }

        public string DisplayName { get; }

        public string SettingsPath { get; }

        public string LogoPath { get; }

        public TerminalPackage(string appUserModelId, Version version, string displayName, string settingsPath, string logoPath)
        {
            AppUserModelId = appUserModelId;
            Version = version;
            DisplayName = displayName;
            SettingsPath = settingsPath;
            LogoPath = logoPath;
        }

        public BitmapImage GetLogo()
        {
            if (File.Exists(LogoPath))
            {
                var memoryStream = new MemoryStream();
                using (var fileStream = File.OpenRead(LogoPath))
                {
                    fileStream.CopyTo(memoryStream);
                    memoryStream.Position = 0;

                    var image = new BitmapImage();
                    image.BeginInit();
                    image.StreamSource = memoryStream;
                    image.EndInit();
                    return image;
                }
            }
            else
            {
                return new BitmapImage(new Uri(ImageLoader.ErrorIconPath));
            }
        }
    }
}
