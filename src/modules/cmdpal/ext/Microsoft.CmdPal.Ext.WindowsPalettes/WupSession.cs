// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.WindowsPalettes;

// Command Palette doesn't tell extensions when the palette window closes, so the bridge treats an
// invocation that dismisses the palette as the end of a Palettes "conversation".
internal static class WupSession
{
    internal static event Action<bool>? Ended;

    internal static void End(bool itemSelected) => Ended?.Invoke(itemSelected);
}
