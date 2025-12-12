// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading.Tasks;
using Awake.ModuleServices;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.ModuleContracts;

namespace PowerToysExtension.Commands;

internal sealed partial class StartAwakeCommand : InvokableCommand
{
    private readonly Func<Task<OperationResult>> _action;
    private readonly string _successToast;

    internal StartAwakeCommand(string title, Func<Task<OperationResult>> action, string successToast = "")
    {
        ArgumentNullException.ThrowIfNull(action);
        ArgumentException.ThrowIfNullOrWhiteSpace(title);

        _action = action;
        _successToast = successToast ?? string.Empty;
        Name = title;
    }

    public override CommandResult Invoke()
    {
        try
        {
            var result = _action().GetAwaiter().GetResult();
            if (!result.Success)
            {
                return CommandResult.ShowToast(result.Error ?? "Failed to start Awake.");
            }

            return string.IsNullOrWhiteSpace(_successToast) ? CommandResult.Hide() : CommandResult.ShowToast(_successToast);
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Launching Awake failed: {ex.Message}");
        }
    }
}
