// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Common.Properties;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.Common.Commands;

public sealed partial class RunAsAdministratorCommand : InvokableCommand
{
    private readonly string _target;
    private readonly string? _arguments;
    private readonly string? _workingDirectory;

    public RunAsAdministratorCommand(string target, string? arguments = null, string? workingDirectory = null)
    {
        _target = target;
        _arguments = arguments;
        _workingDirectory = workingDirectory;
        Name = Resources.RunAsAdministratorCommand_Name;
        Icon = new IconInfo("\uE7EF");
    }

    public static bool IsSupportedFileType(string path)
    {
        var extension = Path.GetExtension(path);
        return extension.Equals(".exe", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".bat", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cmd", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".msc", StringComparison.OrdinalIgnoreCase)
            || extension.Equals(".cpl", StringComparison.OrdinalIgnoreCase);
    }

    public override CommandResult Invoke()
    {
        ShellHelpers.OpenInShell(_target, _arguments, _workingDirectory, ShellHelpers.ShellRunAsType.Administrator);
        return CommandResult.Dismiss();
    }
}
