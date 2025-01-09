// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

internal sealed partial class FallbackReloadItem : FallbackCommandItem
{
    private readonly ReloadExtensionsAction _reloadAction;

    public FallbackReloadItem()
        : base(new ReloadExtensionsAction())
    {
        _reloadAction = (ReloadExtensionsAction)Command!;
        Title = string.Empty;
        Subtitle = "Reload Command Palette extensions";
    }

    public override void UpdateQuery(string query)
    {
        _reloadAction.Name = query.StartsWith('r') ? "Reload" : string.Empty;
        Title = _reloadAction.Name;
    }
}
