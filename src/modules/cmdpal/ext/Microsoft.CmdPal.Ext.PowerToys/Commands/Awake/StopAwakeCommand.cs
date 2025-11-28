// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Awake.ModuleServices;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Commands;

internal sealed partial class StopAwakeCommand : InvokableCommand
{
    internal StopAwakeCommand()
    {
        Name = "Set Awake to Off";
    }

    public override CommandResult Invoke()
    {
        try
        {
            var result = AwakeService.Instance.SetOffAsync().GetAwaiter().GetResult();
            if (result.Success)
            {
                return CommandResult.ShowToast("Awake switched to Off.");
            }

            return CommandResult.ShowToast(result.Error ?? "Awake does not appear to be running.");
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to switch Awake off: {ex.Message}");
        }
    }
}
