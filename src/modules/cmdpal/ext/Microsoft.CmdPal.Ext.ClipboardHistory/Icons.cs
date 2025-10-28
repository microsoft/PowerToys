// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory;

internal static class Icons
{
    internal static IconInfo CopyIcon { get; } = new("\xE8C8");

    internal static IconInfo PictureIcon { get; } = new("\xE8B9");

    internal static IconInfo PasteIcon { get; } = new("\uE77F");

    internal static IconInfo DeleteIcon { get; } = new("\uE74D");

    internal static IconInfo ClipboardListIcon { get; } = IconHelpers.FromRelativePath("Assets\\ClipboardHistory.svg");

    internal static IconInfo Clipboard { get; } = Create("ic_fluent_clipboard_20_regular");

    internal static IconInfo ClipboardImage { get; } = Create("ic_fluent_clipboard_image_20_regular");

    internal static IconInfo ClipboardLetter { get; } = Create("ic_fluent_clipboard_letter_20_regular");

    internal static IconInfo Copy { get; } = Create("   ic_fluent_copy_20_regular");

    internal static IconInfo DocumentCopy { get; } = Create("ic_fluent_document_copy_20_regular");

    internal static IconInfo ImageCopy { get; } = Create("ic_fluent_image_copy_20_regular");

    private static IconInfo Create(string name)
    {
        return IconHelpers.FromRelativePaths($"Assets\\Icons\\{name}.light.svg", $"Assets\\Icons\\{name}.dark.svg");
    }
}
