// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.ClipboardHistory;

internal sealed class Icons
{
    internal static IconInfo Copy { get; } = new("\xE8C8");

    internal static IconInfo Picture { get; } = new("\xE8B9");

    internal static IconInfo Paste { get; } = new("\uE77F");

    internal static IconInfo ClipboardList { get; } = new("\uF0E3");
}
