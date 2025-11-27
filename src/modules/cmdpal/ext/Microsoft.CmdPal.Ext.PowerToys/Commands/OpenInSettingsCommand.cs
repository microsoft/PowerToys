// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Common.UI;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace PowerToysExtension.Commands;

/// <summary>
/// Opens the PowerToys settings page for the given module via SettingsDeepLink.
/// </summary>
internal sealed partial class OpenInSettingsCommand : InvokableCommand
{
    private readonly SettingsDeepLink.SettingsWindow _module;

    internal OpenInSettingsCommand(SettingsDeepLink.SettingsWindow module, string? title = null)
    {
        _module = module;
        Name = string.IsNullOrWhiteSpace(title) ? $"Open {_module} settings" : title;
    }

    public override CommandResult Invoke()
    {
        SettingsDeepLink.OpenSettings(_module);
        return CommandResult.Hide();
    }
}
