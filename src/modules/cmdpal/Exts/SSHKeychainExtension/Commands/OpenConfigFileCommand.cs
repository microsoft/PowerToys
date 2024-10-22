// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;
using SSHKeychainExtension.Data;

namespace SSHKeychainExtension.Commands;

internal sealed partial class OpenConfigFileCommand : InvokableCommand
{
    private readonly string _configFilePath;

    internal OpenConfigFileCommand(string configFilePath)
    {
        this._configFilePath = configFilePath;
        this.Name = "Open Config File";

        // TODO: Add Icon for OpenConfigFileCommand
        this.Icon = new("\uE8A7");
    }

    public override CommandResult Invoke()
    {
        // Just open the config file in the default editor using shell execute
        try
        {
            if (!string.IsNullOrEmpty(_configFilePath))
            {
                Process.Start(new ProcessStartInfo(_configFilePath) { UseShellExecute = true });
                return CommandResult.KeepOpen();
            }
        }
        catch
        {
            Debug.WriteLine("Failed to open config file");
        }

        return CommandResult.KeepOpen();
    }
}
