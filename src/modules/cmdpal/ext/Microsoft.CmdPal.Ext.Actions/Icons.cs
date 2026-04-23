// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Actions;

internal sealed class Icons
{
    internal static IconInfo ActionsPng { get; } = IconHelpers.FromRelativePath("Assets\\Actions.png");

    // Action input icons
    internal static IconInfo DocumentInput { get; } = new IconInfo("Assets\\Document.png");

    internal static IconInfo FileInput { get; } = new IconInfo("\uE8A5");

    internal static IconInfo PhotoInput { get; } = new IconInfo("\uE91b");

    internal static IconInfo TextInput { get; } = new IconInfo("\uE710");

    internal static IconInfo StreamingTextInput { get; } = new IconInfo("\uE710");

    internal static IconInfo RemoteFileInput { get; } = new IconInfo("\uE8E5");

    internal static IconInfo TableInput { get; } = new IconInfo("\uf575");

    internal static IconInfo ContactInput { get; } = new IconInfo("\uE77b");
}
