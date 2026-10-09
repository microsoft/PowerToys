// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.CmdPal.Ext.Shell.Helpers;

public interface ISettingsInterface
{
    public bool LeaveShellOpen { get; }

    public string ShellCommandExecution { get; }

    public bool RunAsAdministrator { get; }

    public Dictionary<string, int> Count { get; }

    public void AddCmdHistory(string cmdName);
}
