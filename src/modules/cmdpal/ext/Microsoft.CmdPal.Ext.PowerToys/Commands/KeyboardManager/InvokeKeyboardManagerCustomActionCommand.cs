// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ManagedCommon;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Commands;

internal sealed partial class InvokeKeyboardManagerCustomActionCommand : InvokableCommand
{
    private static readonly RunnerActionClient ActionClient = new();
    private readonly string _actionId;
    private readonly string _fallbackError;

    public InvokeKeyboardManagerCustomActionCommand(string actionId, string displayName)
    {
        _actionId = actionId;
        _fallbackError = $"Failed to invoke {displayName}.";
        Name = displayName;
    }

    public override CommandResult Invoke()
    {
        try
        {
            var result = ActionClient.InvokeAction(_actionId);
            return result.Success
                ? CommandResult.Dismiss()
                : CommandResult.ShowToast(string.IsNullOrWhiteSpace(result.Message) ? _fallbackError : result.Message);
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"{_fallbackError} {ex.Message}");
        }
    }
}
