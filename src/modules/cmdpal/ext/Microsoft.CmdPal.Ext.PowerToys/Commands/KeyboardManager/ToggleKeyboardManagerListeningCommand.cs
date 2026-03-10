// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;
using PowerToysExtension.Helpers;
using PowerToysExtension.Properties;

namespace PowerToysExtension.Commands;

internal sealed partial class ToggleKeyboardManagerListeningCommand : InvokableCommand
{
    public ToggleKeyboardManagerListeningCommand()
    {
        Name = "Toggle Keyboard Manager active state";
    }

    public override CommandResult Invoke()
    {
        return KeyboardManagerStateService.TryToggleListening()
            ? CommandResult.KeepOpen()
            : CommandResult.ShowToast(Resources.ResourceManager.GetString("KeyboardManager_ToggleListening_Error", Resources.Culture) ?? "Keyboard Manager is unavailable. Try enabling it in PowerToys settings.");
    }
}
