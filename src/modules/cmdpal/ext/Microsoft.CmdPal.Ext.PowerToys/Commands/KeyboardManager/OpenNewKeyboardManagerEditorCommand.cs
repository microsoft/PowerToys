// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;

namespace PowerToysExtension.Commands;

/// <summary>
/// Opens the new Keyboard Manager editor.
/// </summary>
internal sealed partial class OpenNewKeyboardManagerEditorCommand : InvokableCommand
{
    public OpenNewKeyboardManagerEditorCommand()
    {
        Name = "Open New Keyboard Manager Editor";
    }

    public override CommandResult Invoke()
    {
        try
        {
            using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.OpenNewKeyboardManagerEvent());
            evt.Set();
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to open New Keyboard Manager Editor: {ex.Message}");
        }
    }
}
