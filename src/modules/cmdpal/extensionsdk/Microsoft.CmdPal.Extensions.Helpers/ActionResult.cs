// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public class ActionResult : ICommandResult
{
    private ICommandResultArgs _args;
    
    private CommandResultKind _kind = CommandResultKind.Dismiss;
    
    public ICommandResultArgs Args => _args;

    public CommandResultKind Kind => _kind;

    public static ActionResult Dismiss()
    {
        return new ActionResult()
        {
            _kind = CommandResultKind.Dismiss
        };
    }

    public static ActionResult GoHome()
    {
        return new ActionResult()
        {
            _kind = CommandResultKind.GoHome
        };
    }
    public static ActionResult KeepOpen()
    {
        return new ActionResult()
        {
            _kind = CommandResultKind.KeepOpen
        };
    }
}
