// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Windows.Forms;
using ColorPicker.ModuleServices;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Commands;

/// <summary>
/// Copies a saved color in a chosen format.
/// </summary>
internal sealed partial class CopySavedColorCommand : InvokableCommand
{
    private readonly SavedColor _color;
    private readonly string _copyValue;

    public CopySavedColorCommand(SavedColor color, string copyValue)
    {
        _color = color;
        _copyValue = copyValue;
        Name = $"Copy {_color.Hex}";
    }

    public override CommandResult Invoke()
    {
        try
        {
            Clipboard.SetText(_copyValue);
            return CommandResult.ShowToast($"Copied {_copyValue}");
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to copy color: {ex.Message}");
        }
    }
}
