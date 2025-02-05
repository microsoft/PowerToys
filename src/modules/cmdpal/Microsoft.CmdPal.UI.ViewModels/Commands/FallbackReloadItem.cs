// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

internal sealed partial class FallbackReloadItem : FallbackCommandItem
{
    private readonly ReloadExtensionsCommand _reloadCommand;

    public FallbackReloadItem()
        : base(new ReloadExtensionsCommand())
    {
        _reloadCommand = (ReloadExtensionsCommand)Command!;
        Title = string.Empty;
        Subtitle = "Reload Command Palette extensions";
    }

    public override void UpdateQuery(string query)
    {
        _reloadCommand.Name = query.StartsWith('r') ? "Reload" : string.Empty;
        Title = _reloadCommand.Name;
    }
}
