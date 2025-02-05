// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public sealed partial class OpenUrlCommand : InvokableCommand
{
    private readonly string _target;

    public CommandResult Result { get; set; } = CommandResult.KeepOpen();

    public OpenUrlCommand(string target)
    {
        _target = target;
        Name = "Open";
        Icon = new IconInfo("\uE8A7");
    }

    public override CommandResult Invoke()
    {
        ShellHelpers.OpenInShell(_target);
        return Result;
    }
}
