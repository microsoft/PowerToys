// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace Microsoft.CmdPal.Ext.Calc.Helper;

internal static class PasteHelper
{
    private static readonly ushort[] PasteChord = [(ushort)VirtualKey.Control, (ushort)VirtualKey.V];

    private static readonly InputChordOptions PasteChordOptions = new()
    {
        // Any key event with the extraInfo set to this value will be ignored
        // by the keyboard hook and sent to the system instead.
        ExtraInfo = (UIntPtr)0x5555,
    };

    internal static void SendPasteKeyCombination()
    {
        ExtensionHost.LogMessage(new LogMessage { Message = "Sending paste keys..." });

        // Releases any modifier that is still held and sends Ctrl+V, all in one batch.
        var result = InputInjection.SendChord(PasteChord, PasteChordOptions);
        if (!result.Succeeded)
        {
            ExtensionHost.LogMessage(new LogMessage { Message = $"Paste input was not fully sent: sent {result.Sent} of {result.Requested} events, error {result.Error}, cleanup sent {result.CleanupSent} of {result.CleanupRequested}", State = MessageState.Error });
            return;
        }

        ExtensionHost.LogMessage(new LogMessage { Message = "Paste sent" });
    }
}
