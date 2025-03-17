// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Indexer;

internal sealed class Icons
{
    internal static IconInfo FileExplorerSegoe { get; } = new("\uEC50");

    internal static IconInfo FileExplorer { get; } = IconHelpers.FromRelativePath("Assets\\FileExplorer.png");

    internal static IconInfo OpenFile { get; } = new("\uE8E5"); // OpenFile

    internal static IconInfo Document { get; } = new("\uE8A5"); // Document

    internal static IconInfo FolderOpen { get; } = new("\uE838"); // FolderOpen
}
