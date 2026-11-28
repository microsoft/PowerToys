// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TextExpander;

internal static class Icons
{
    /// <summary>Gets the icon Text Expander has in PowerToys Settings.</summary>
    internal static IconInfo TextExpander { get; } = IconHelpers.FromRelativePath("Assets\\TextExpander.png");

    internal static IconInfo Paste { get; } = new("\uE77F");

    internal static IconInfo Copy { get; } = new("\uE8C8");

    internal static IconInfo Settings { get; } = new("\uE713");

    internal static IconInfo Search { get; } = new("\uE721");

    internal static IconInfo Warning { get; } = new("\uE7BA");

    internal static IconInfo Email { get; } = new("\uE715");

    internal static IconInfo Link { get; } = new("\uE71B");

    internal static IconInfo Phone { get; } = new("\uE717");

    internal static IconInfo Number { get; } = new("\uE8EF");
}
