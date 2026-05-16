// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class CommandParameterRun : ParameterValueRun, ICommandParameterRun
{
    public virtual string? DisplayText { get; set => SetProperty(ref field, value); }

    public virtual ICommand? Command { get; set => SetProperty(ref field, value); }

    public virtual IIconInfo? Icon { get; set => SetProperty(ref field, value); }

    public override bool NeedsValue => Value == null;

    public virtual ICommand? GetSelectValueCommand(ulong hostHwnd)
    {
        if (Command is IRequiresHostHwnd requiresHwnd)
        {
            requiresHwnd.SetHostHwnd((nint)hostHwnd);
        }

        return Command;
    }

    // Toolkit helper: a value for the parameter
    private object? _value;

    public override object? Value
    {
        get => _value;
        set
        {
            if (SetProperty(ref _value, value))
            {
                OnPropertyChanged(nameof(NeedsValue));
            }
        }
    }

    public override void ClearValue()
    {
        Value = null;
    }
}
