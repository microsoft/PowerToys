// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Awake.ModuleServices;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Commands;

internal sealed partial class StopAwakeCommand : InvokableCommand
{
    private readonly Action? _onSuccess;

    internal StopAwakeCommand(Action? onSuccess = null)
    {
        _onSuccess = onSuccess;
        Name = "Set Awake to Off";
    }

    public override CommandResult Invoke()
    {
        try
        {
            var result = AwakeService.Instance.SetOffAsync().GetAwaiter().GetResult();
            if (result.Success)
            {
                _onSuccess?.Invoke();
                return ShowToastKeepOpen("Awake switched to Off.");
            }

            return ShowToastKeepOpen(result.Error ?? "Awake does not appear to be running.");
        }
        catch (Exception ex)
        {
            return ShowToastKeepOpen($"Failed to switch Awake off: {ex.Message}");
        }
    }

    private static CommandResult ShowToastKeepOpen(string message)
    {
        return CommandResult.ShowToast(new ToastArgs()
        {
            Message = message,
            Result = CommandResult.KeepOpen(),
        });
    }
}
