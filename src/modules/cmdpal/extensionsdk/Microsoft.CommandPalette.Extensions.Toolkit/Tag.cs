// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class Tag : BaseObservable, ITag
{
    private OptionalColor _foreground;
    private OptionalColor _background;
    private string _text = string.Empty;

    public virtual OptionalColor Foreground
    {
        get => _foreground;
        set
        {
            _foreground = value;
            OnPropertyChanged(nameof(Foreground));
        }
    }

    public virtual OptionalColor Background
    {
        get => _background;
        set
        {
            _background = value;
            OnPropertyChanged(nameof(Background));
        }
    }

    public virtual IIconInfo Icon
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(Icon));
        }
    }

= new IconInfo();

    public virtual string Text
    {
        get => _text;
        set
        {
            _text = value;
            OnPropertyChanged(nameof(Text));
        }
    }

    public virtual string ToolTip
    {
        get;
        set
        {
            field = value;
            OnPropertyChanged(nameof(ToolTip));
        }
    }

= string.Empty;

    public Tag()
    {
    }

    public Tag(string text)
    {
        _text = text;
    }
}
