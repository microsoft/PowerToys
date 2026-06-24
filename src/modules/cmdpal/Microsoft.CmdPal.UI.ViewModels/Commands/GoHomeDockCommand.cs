// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Commands;

/// <summary>
/// A lightweight command used as a dock band item that navigates the user
/// back to the Command Palette home page when invoked.
/// </summary>
internal sealed partial class GoHomeDockCommand : InvokableCommand
{
    public GoHomeDockCommand()
    {
        Name = Properties.Resources.builtin_command_palette_title;
        Icon = IconHelpers.FromRelativePath("Assets\\Square44x44Logo.altform-unplated_targetsize-256.png");
    }

    public override ICommandResult Invoke() => CommandResult.GoHome();
}
