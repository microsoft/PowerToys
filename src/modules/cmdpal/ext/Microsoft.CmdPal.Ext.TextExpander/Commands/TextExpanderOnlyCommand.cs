// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.TextExpander.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.TextExpander.Commands;

/// <summary>
/// Stands in for pasting a snippet only Text Expander can fill in, and says to type its trigger instead.
/// </summary>
internal sealed partial class TextExpanderOnlyCommand : InvokableCommand
{
    internal TextExpanderOnlyCommand()
    {
        Name = Resources.text_expander_only_name;
        Icon = Icons.TextExpander;
    }

    public override ICommandResult Invoke() => CommandResult.ShowToast(new ToastArgs
    {
        Message = Resources.text_expander_only_toast,
        Result = CommandResult.KeepOpen(),
    });
}
