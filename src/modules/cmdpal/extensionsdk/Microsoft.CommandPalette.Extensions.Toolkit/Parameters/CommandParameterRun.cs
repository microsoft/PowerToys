// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class CommandParameterRun : ParameterValueRun, ICommandParameterRun
{
    private string? _displayText;

    public virtual string? DisplayText
    {
        get => _displayText;
        set
        {
            _displayText = value;
            OnPropertyChanged(nameof(DisplayText));
        }
    }

    private ICommand? _command;

    public virtual ICommand? Command
    {
        get => _command;
        set
        {
            _command = value;
            OnPropertyChanged(nameof(Command));
        }
    }

    private IIconInfo? _icon;

    public virtual IIconInfo? Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            OnPropertyChanged(nameof(Icon));
        }
    }

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
            _value = value;
            OnPropertyChanged(nameof(Value));
            OnPropertyChanged(nameof(NeedsValue));
        }
    }

    public override void ClearValue()
    {
        Value = null;
    }
}
