// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public sealed partial class AnonymousCommand : InvokableCommand
{
    private readonly Action? _action;

    public ICommandResult Result { get; set; } = CommandResult.Dismiss();

    public AnonymousCommand(Action? action)
    {
        Name = "Invoke";
        _action = action;
    }

    public override ICommandResult Invoke()
    {
        if (_action != null)
        {
            _action();
        }

        return Result;
    }
}
