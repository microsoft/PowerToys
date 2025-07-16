// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal sealed class Icons
{
    internal static IconInfo BookmarkIcon => IconHelpers.FromRelativePath("Assets\\Bookmark.svg");

    internal static IconInfo DeleteIcon { get; private set; } = new("\uE74D"); // Delete

    internal static IconInfo EditIcon { get; private set; } = new("\uE70F"); // Edit

    internal static IconInfo PinIcon { get; private set; } = new IconInfo("\uE718"); // Pin
}
