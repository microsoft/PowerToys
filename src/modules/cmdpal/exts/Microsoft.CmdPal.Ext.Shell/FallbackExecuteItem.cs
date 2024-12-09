// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Ext.Shell.Commands;
using Microsoft.CmdPal.Ext.Shell.Helpers;
using Microsoft.CmdPal.Ext.Shell.Pages;
using Microsoft.CmdPal.Ext.Shell.Properties;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace Microsoft.CmdPal.Ext.Shell;

internal sealed partial class FallbackExecuteItem : FallbackCommandItem
{
    private readonly ExecuteItem _executeItem;

    public FallbackExecuteItem(SettingsManager settings)
        : base(new ExecuteItem(string.Empty, settings))
    {
        _executeItem = (ExecuteItem)this.Command!;
        Title = string.Empty;
        Subtitle = Properties.Resources.generic_run_command;
        Icon = new("\uE756");

        // TODO: this is a bug in the current POC. I don't think Fallback items
        // get icons set correctly.
        _executeItem.Icon = Icon;
    }

    public override void UpdateQuery(string query)
    {
        _executeItem.Cmd = query;
        Title = query;
    }
}
