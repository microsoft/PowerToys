// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class SelectParameterCommand<T> : InvokableCommand
{
    public event TypedEventHandler<object, T>? ValueSelected;

    public T Value { get; protected set; }

    public SelectParameterCommand(T value)
    {
        Value = value;
    }

    public override ICommandResult Invoke()
    {
        ValueSelected?.Invoke(this, Value);
        return CommandResult.KeepOpen();
    }
}
