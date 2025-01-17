// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Extensions.Helpers;

public class Tag : BaseObservable, ITag
{
    private OptionalColor _foreground;
    private OptionalColor _background;
    private IconInfo _icon = new(string.Empty);
    private string _text = string.Empty;
    private string _toolTip = string.Empty;
    private ICommand? _command;

    public OptionalColor Foreground
    {
        get => _foreground;
        set
        {
            _foreground = value;
            OnPropertyChanged(nameof(Foreground));
        }
    }

    public OptionalColor Background
    {
        get => _background;
        set
        {
            _background = value;
            OnPropertyChanged(nameof(Background));
        }
    }

    public IconInfo Icon
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

    public Tag()
    {
    }

    public Tag(string text)
    {
        _text = text;
    }
}
