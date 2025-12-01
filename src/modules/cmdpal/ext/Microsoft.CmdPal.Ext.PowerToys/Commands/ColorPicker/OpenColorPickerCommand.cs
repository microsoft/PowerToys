// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using ColorPicker.ModuleServices;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Commands;

/// <summary>
/// Opens the Color Picker picker session via shared event.
/// </summary>
internal sealed partial class OpenColorPickerCommand : InvokableCommand
{
    public OpenColorPickerCommand()
    {
        Name = "Open Color Picker";
    }

    public override CommandResult Invoke()
    {
        try
        {
            var result = ColorPickerService.Instance.OpenPickerAsync().GetAwaiter().GetResult();
            if (!result.Success)
            {
                return CommandResult.ShowToast(result.Error ?? "Failed to open Color Picker.");
            }

            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to open Color Picker: {ex.Message}");
        }
    }
}
