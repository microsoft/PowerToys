// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

internal sealed partial class FallbackReloadItem : FallbackCommandItem
{
    private readonly ReloadExtensionsCommand _reloadCommand;

    private const string _id = "com.microsoft.cmdpal.reload";

    public FallbackReloadItem()
        : base(
            new ReloadExtensionsCommand() { Id = _id },
            Properties.Resources.builtin_reload_display_title,
            _id)
    {
        _reloadCommand = (ReloadExtensionsCommand)Command!;
        Title = string.Empty;
        Subtitle = Properties.Resources.builtin_reload_subtitle;
    }

    public override void UpdateQuery(string query)
    {
        _reloadCommand.Name = query.StartsWith("r", StringComparison.OrdinalIgnoreCase) ? "Reload" : string.Empty;
        Title = _reloadCommand.Name;
    }
}
