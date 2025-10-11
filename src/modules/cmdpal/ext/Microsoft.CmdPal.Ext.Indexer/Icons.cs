// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer;

internal static class Icons
{
    internal static IconInfo FileExplorerSegoeIcon { get; } = new("\uEC50");

    internal static IconInfo FileExplorerIcon { get; } = IconHelpers.FromRelativePath("Assets\\FileExplorer.png");

    internal static IconInfo ActionsIcon { get; } = IconHelpers.FromRelativePath("Assets\\Actions.png");

    internal static IconInfo OpenFileIcon { get; } = new("\uE8E5"); // OpenFile

    internal static IconInfo DocumentIcon { get; } = new("\uE8A5"); // Document

    internal static IconInfo FolderOpenIcon { get; } = new("\uE838"); // FolderOpen

    internal static IconInfo FilesIcon { get; } = new("\uF571"); // PrintAllPages

    internal static IconInfo FilterIcon { get; } = new("\uE71C"); // Filter
}
