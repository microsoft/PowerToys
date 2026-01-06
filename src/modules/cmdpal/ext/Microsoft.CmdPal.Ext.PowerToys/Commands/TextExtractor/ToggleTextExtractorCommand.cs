// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Threading;
using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToys.Interop;

namespace PowerToysExtension.Commands;

/// <summary>
/// Triggers the Text Extractor UI via the existing show event.
/// </summary>
internal sealed partial class ToggleTextExtractorCommand : InvokableCommand
{
    public ToggleTextExtractorCommand()
    {
        Name = "Toggle Text Extractor";
    }

    public override CommandResult Invoke()
    {
        try
        {
            using var evt = new EventWaitHandle(false, EventResetMode.AutoReset, Constants.ShowPowerOCRSharedEvent());
            evt.Set();
            return CommandResult.Dismiss();
        }
        catch (Exception ex)
        {
            return CommandResult.ShowToast($"Failed to toggle Text Extractor: {ex.Message}");
        }
    }
}
