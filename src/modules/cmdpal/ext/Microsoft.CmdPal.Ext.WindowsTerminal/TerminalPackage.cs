// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.IO;
using Microsoft.UI.Xaml.Media.Imaging;

// using Wox.Infrastructure.Image;
namespace Microsoft.CmdPal.Ext.WindowsTerminal;

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
        var image = new BitmapImage();

        if (File.Exists(LogoPath))
        {
            using var fileStream = File.OpenRead(LogoPath);
            image.SetSource(fileStream.AsRandomAccessStream());
        }
        else
        {
            // Not using wox anymore, TODO: find the right new way to handle this
            // image.UriSource = new Uri(ImageLoader.ErrorIconPath);
        }

        return image;
    }
}
