// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Bookmarks;

internal static class Icons
{
    internal static IconInfo BookmarkIcon { get; } = IconHelpers.FromRelativePath("Assets\\Bookmark.svg");

    internal static IconInfo DeleteIcon { get; } = new("\uE74D"); // Delete

    internal static IconInfo EditIcon { get; } = new("\uE70F"); // Edit

    internal static IconInfo PinIcon { get; } = new IconInfo("\uE718"); // Pin

    internal static IconInfo Reloading { get; } = new IconInfo("\uF16A"); // ProgressRing

    internal static IconInfo CopyPath { get; } = new IconInfo("\uE8C8"); // Copy

    internal static class BookmarkTypes
    {
        internal static IconInfo WebUrl { get; } = new("\uE774"); // Globe

        internal static IconInfo FilePath { get; } = new("\uE8A5"); // OpenFile

        internal static IconInfo FolderPath { get; } = new("\uE8B7"); // OpenFolder

        internal static IconInfo Application { get; } = new("\uE737"); // Favicon (~looks like empty window)

        internal static IconInfo Command { get; } = new("\uE756"); // CommandPrompt

        internal static IconInfo Unknown { get; } = new("\uE71B"); // Link

        internal static IconInfo Game { get; } = new("\uE7FC"); // Game controller
    }

    private static IconInfo DualColorFromRelativePath(string name)
    {
        return IconHelpers.FromRelativePaths($"Assets\\Icons\\{name}.light.svg", $"Assets\\Icons\\{name}.dark.svg");
    }
}
