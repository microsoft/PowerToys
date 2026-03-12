// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Commands;

/// <summary>
/// Opens the new Keyboard Manager editor.
/// </summary>
internal sealed partial class OpenNewKeyboardManagerEditorCommand : InvokableCommand
{
    private static readonly RunnerActionClient ActionClient = new();

    public OpenNewKeyboardManagerEditorCommand()
    {
        Name = "Open New Keyboard Manager Editor";
    }

    public override CommandResult Invoke()
    {
        try
        {
            var result = ActionClient.InvokeAction(RunnerActionIds.KeyboardManagerOpenEditor);
            return result.Success
                ? CommandResult.Dismiss()
                : CommandResult.ShowToast(string.IsNullOrWhiteSpace(result.Message) ? "Failed to open New Keyboard Manager Editor." : result.Message);
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to open New Keyboard Manager Editor: {ex.Message}");
        }
    }
}
