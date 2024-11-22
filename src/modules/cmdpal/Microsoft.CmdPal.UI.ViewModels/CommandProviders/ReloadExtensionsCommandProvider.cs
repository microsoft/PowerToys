// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.UI.ViewModels.BuiltinCommands;

/// <summary>
/// Built-in Provider for a top-level command which can reload extensions. Invokes the <see cref="ReloadExtensionsAction"/>.
/// </summary>
public partial class ReloadExtensionsCommandProvider : CommandProvider
{
    private readonly ReloadExtensionsAction reloadAction = new();

    public override ICommandItem[] TopLevelCommands()
    {
        return [];
    }

    public override IFallbackCommandItem[] FallbackCommands()
    {
        return [new FallbackCommandItem(reloadAction) { Subtitle = "Reload Command Palette extensions" }];
    }
}
