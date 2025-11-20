// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class SelectParameterCommand<T> : InvokableCommand
{
    public event TypedEventHandler<object, T>? ValueSelected;

    private T _value;

    public T Value
    {
        get => _value; protected set { _value = value; }
    }

    public SelectParameterCommand(T value)
    {
        _value = value;
    }

    public override ICommandResult Invoke()
    {
        ValueSelected?.Invoke(this, _value);
        return CommandResult.KeepOpen();
    }
}
