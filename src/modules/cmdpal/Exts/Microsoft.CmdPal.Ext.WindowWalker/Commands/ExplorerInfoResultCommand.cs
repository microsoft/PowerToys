// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CmdPal.Ext.WindowWalker.Components;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Ext.WindowWalker.Commands;

internal sealed partial class ExplorerInfoResultCommand : InvokableCommand
{
    public ExplorerInfoResultCommand()
    {
    }

    public static bool OpenInShell(string path, string? arguments = null, string? workingDir = null, ShellRunAsType runAs = ShellRunAsType.None, bool runWithHiddenWindow = false)
    {
        using var process = new Process();
        process.StartInfo.FileName = path;
        process.StartInfo.WorkingDirectory = string.IsNullOrWhiteSpace(workingDir) ? string.Empty : workingDir;
        process.StartInfo.Arguments = string.IsNullOrWhiteSpace(arguments) ? string.Empty : arguments;
        process.StartInfo.WindowStyle = runWithHiddenWindow ? ProcessWindowStyle.Hidden : ProcessWindowStyle.Normal;
        process.StartInfo.UseShellExecute = true;

        if (runAs == ShellRunAsType.Administrator)
        {
            process.StartInfo.Verb = "RunAs";
        }
        else if (runAs == ShellRunAsType.OtherUser)
        {
            process.StartInfo.Verb = "RunAsUser";
        }

        try
        {
            process.Start();
            return true;
        }
        catch (Win32Exception ex)
        {
            ExtensionHost.LogMessage(new LogMessage() { Message = $"Unable to open {path}: {ex.Message}" });
            return false;
        }
    }

    public override ICommandResult Invoke()
    {
        OpenInShell("rundll32.exe", "shell32.dll,Options_RunDLL 7"); // "shell32.dll,Options_RunDLL 7" opens the view tab in folder options of explorer.
        return CommandResult.Dismiss();
    }

    public enum ShellRunAsType
    {
        None,
        Administrator,
        OtherUser,
    }
}
