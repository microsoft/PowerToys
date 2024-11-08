// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.UI;

namespace Microsoft.CmdPal.Extensions.Helpers;

public class Tag : BaseObservable, ITag
{
    private Color _color;
    private IconDataType _icon = new(string.Empty);
    private string _text = string.Empty;
    private string _toolTip = string.Empty;
    private ICommand? _command;

    public Color Color
    {
        get => _color;
        set
        {
            _color = value;
            OnPropertyChanged(nameof(Color));
        }
    }

    public IconDataType Icon
    {
        get => _icon;
        set
        {
            _icon = value;
            OnPropertyChanged(nameof(Icon));
        }
    }

    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            OnPropertyChanged(nameof(Text));
        }
    }

    public string ToolTip
    {
        get => _toolTip;
        set
        {
            _toolTip = value;
            OnPropertyChanged(nameof(ToolTip));
        }
    }

    public ICommand? Command
    {
        get => _command;
        set
        {
            _command = value;
            OnPropertyChanged(nameof(Command));
        }
    }
}
