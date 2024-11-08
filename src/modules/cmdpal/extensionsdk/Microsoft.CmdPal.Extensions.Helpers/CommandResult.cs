// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public class CommandResult : ICommandResult
{
    // TODO: is Args needed?
    private ICommandResultArgs? _args;
    private CommandResultKind _kind = CommandResultKind.Dismiss;

    // TODO: is Args needed?
    public ICommandResultArgs? Args => _args;

    public CommandResultKind Kind => _kind;

    public static CommandResult Dismiss()
    {
        return new CommandResult()
        {
            _kind = CommandResultKind.Dismiss,
        };
    }

    public static CommandResult GoHome()
    {
        return new CommandResult()
        {
            _kind = CommandResultKind.GoHome,
            _args = null,
        };
    }

    public static CommandResult KeepOpen()
    {
        return new CommandResult()
        {
            _kind = CommandResultKind.KeepOpen,
            _args = null,
        };
    }
}
