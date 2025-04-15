// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Resources;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.WindowsTerminal.Helpers;
using Microsoft.CmdPal.Ext.WindowsTerminal.Properties;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.UI;

namespace Microsoft.CmdPal.Ext.WindowsTerminal.Commands;

internal sealed partial class LaunchProfileCommand : InvokableCommand
{
    private readonly string _id;
    private readonly string _profile;
    private readonly bool _openNewTab;
    private readonly bool _openQuake;

    internal LaunchProfileCommand(string id, string profile, string iconPath, bool openNewTab, bool openQuake)
    {
        this._id = id;
        this._profile = profile;
        this._openNewTab = openNewTab;
        this._openQuake = openQuake;

        this.Name = Resources.launch_profile;
        this.Icon = new IconInfo(iconPath);
    }

    private void Launch(string id, string profile)
    {
        var appManager = new ApplicationActivationManager();
        const ActivateOptions noFlags = ActivateOptions.None;
        var queryArguments = TerminalHelper.GetArguments(profile, _openNewTab, _openQuake);
        try
        {
            appManager.ActivateApplication(id, queryArguments, noFlags, out var unusedPid);
        }
#pragma warning disable IDE0059, CS0168
        catch (Exception ex)
        {
            // TODO GH #108 We need to figure out some logging
            // var name = "Plugin: " + Resources.plugin_name;
            // var message = Resources.run_terminal_failed;
            // Log.Exception("Failed to open Windows Terminal", ex, GetType());
            // _context.API.ShowMsg(name, message, string.Empty);
        }
    }
#pragma warning restore IDE0059, CS0168

    public override CommandResult Invoke()
    {
        try
        {
            Launch(_id, _profile);
        }
        catch
        {
            // TODO GH #108 We need to figure out some logging
        }

        return CommandResult.Dismiss();
    }
}
