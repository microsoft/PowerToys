// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

public sealed partial class OpenGallerySettingsCommand : OpenSettingsCommand
{
    public OpenGallerySettingsCommand()
        : base(
            settingsPageTag: "Gallery",
            name: Properties.Resources.builtin_open_gallery_name,
            glyph: "\uE719",
            id: "com.microsoft.cmdpal.opengallerysettings")
    {
    }
}
