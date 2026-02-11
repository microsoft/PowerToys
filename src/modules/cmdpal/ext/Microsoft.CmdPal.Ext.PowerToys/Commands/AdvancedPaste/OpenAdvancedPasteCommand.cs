// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;

namespace PowerToysExtension.Commands;

/// <summary>
/// Opens Advanced Paste UI by signaling the module's show event.
/// The DLL interface handles starting the process if it's not running.
/// </summary>
internal sealed partial class OpenAdvancedPasteCommand : InvokableCommand
{
    public OpenAdvancedPasteCommand()
    {
        Name = "Open Advanced Paste";
    }

    public override CommandResult Invoke()
    {
        try
        {
            using var showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.AdvancedPasteShowUIEvent());
            showEvent.Set();
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to open Advanced Paste: {ex.Message}");
        }
    }
}
