// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Shell.Commands;
using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.Shell;

internal sealed partial class FallbackExecuteItem : FallbackCommandItem
{
    private readonly ExecuteItem _executeItem;

    public FallbackExecuteItem(SettingsManager settings)
        : base(new ExecuteItem(string.Empty, settings), Resources.shell_command_display_title)
    {
        _executeItem = (ExecuteItem)this.Command!;
        Title = string.Empty;
        _executeItem.Name = string.Empty;
        Subtitle = Properties.Resources.generic_run_command;
        Icon = new IconInfo("\uE756");
    }

    public override void UpdateQuery(string query)
    {
        _executeItem.Cmd = query;
        _executeItem.Name = string.IsNullOrEmpty(query) ? string.Empty : Properties.Resources.generic_run_command;
        Title = query;
    }
}
