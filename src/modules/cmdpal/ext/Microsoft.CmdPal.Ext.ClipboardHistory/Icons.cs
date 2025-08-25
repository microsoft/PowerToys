// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory;

internal sealed class Icons
{
    internal static IconInfo CopyIcon { get; } = new("\xE8C8");

    internal static IconInfo PictureIcon { get; } = new("\xE8B9");

    internal static IconInfo PasteIcon { get; } = new("\uE77F");

    internal static IconInfo ClipboardListIcon { get; } = IconHelpers.FromRelativePath("Assets\\ClipboardHistory.svg");
}
