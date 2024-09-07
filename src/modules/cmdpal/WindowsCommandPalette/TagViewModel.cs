// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.ComponentModel;
using Microsoft.CmdPal.Extensions;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Windows.UI;

namespace DeveloperCommandPalette;

public sealed class TagViewModel : INotifyPropertyChanged
{
    private readonly ITag _tag;

    internal IconDataType Icon => _tag.Icon;

    internal string Text => _tag.Text;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool HasIcon => !string.IsNullOrEmpty(Icon?.Icon);

    internal IconElement IcoElement => Microsoft.Terminal.UI.IconPathConverter.IconMUX(Icon?.Icon ?? string.Empty, 10);

    // TODO! VV These guys should have proper theme-aware lookups for default values
    internal Brush BorderBrush => new SolidColorBrush(_tag.Color);

    internal Brush TextBrush => new SolidColorBrush(_tag.Color.A == 0 ? Color.FromArgb(255, 255, 255, 255) : _tag.Color);

    internal Brush BackgroundBrush => new SolidColorBrush(_tag.Color.A == 0 ? _tag.Color : Color.FromArgb((byte)(_tag.Color.A / 4), _tag.Color.R, _tag.Color.G, _tag.Color.B));

    public TagViewModel(ITag tag)
    {
        this._tag = tag;

        // this.Tag.PropChanged += Tag_PropertyChanged;
    }

    private void Tag_PropertyChanged(object sender, Microsoft.CmdPal.Extensions.PropChangedEventArgs args)
    {
        this.PropertyChanged?.Invoke(this, new(args.PropertyName));
    }
}
