// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public abstract partial class InvokableCommand : Command, IInvokableCommand
{
    public virtual ICommandResult Invoke() => CommandResult.KeepOpen();

    public virtual ICommandResult Invoke(object? sender) => Invoke();
}
