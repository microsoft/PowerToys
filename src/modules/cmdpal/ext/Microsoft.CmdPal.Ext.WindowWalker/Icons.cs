// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowWalker;

internal static class Icons
{
    internal static IconInfo WindowWalkerIcon { get; } = IconHelpers.FromRelativePath("Assets\\WindowWalker.svg");

    internal static IconInfo EndTask { get; } = new("\uF140"); // StatusCircleBlock

    internal static IconInfo CloseWindow { get; } = new("\uE894"); // Clear

    internal static IconInfo Info { get; } = new("\uE946"); // Info

    internal static IconInfo GenericAppIcon { get; } = new("\uE737"); // Favicon
}
