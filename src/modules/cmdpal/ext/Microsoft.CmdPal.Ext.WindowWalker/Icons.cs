// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowWalker;

internal sealed class Icons
{
    internal static IconInfo WindowWalkerIcon { get; } = IconHelpers.FromRelativePath("Assets\\WindowWalker.svg");

    internal static IconInfo EndTask { get; } = new IconInfo("\uF140"); // StatusCircleBlock

    internal static IconInfo CloseWindow { get; } = new IconInfo("\uE894"); // Clear

    internal static IconInfo Info { get; } = new IconInfo("\uE946"); // Info

    internal static IconInfo GenericAppIcon { get; } = new("\uE737"); // Favicon
}
