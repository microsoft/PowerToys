// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Microsoft.Terminal.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Navigation;
using Microsoft.Windows.CommandPalette.Extensions;
using Windows.ApplicationModel.AppExtensions;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;

namespace DeveloperCommandPalette;

public sealed class TagViewModel : INotifyPropertyChanged
{
    internal ITag Tag;

    internal IconDataType Icon => Tag.Icon;

    internal string Text => Tag.Text;

    public event PropertyChangedEventHandler? PropertyChanged;

    public bool HasIcon => !string.IsNullOrEmpty(Icon?.Icon);

    internal IconElement IcoElement => Microsoft.Terminal.UI.IconPathConverter.IconMUX(Icon?.Icon ?? string.Empty, 10);

    // TODO! VV These guys should have proper theme-aware lookups for default values
    internal Brush BorderBrush => new SolidColorBrush(Tag.Color);

    internal Brush TextBrush => new SolidColorBrush(Tag.Color.A == 0 ? Color.FromArgb(255, 255, 255, 255) : Tag.Color);

    internal Brush BackgroundBrush => new SolidColorBrush(Tag.Color.A == 0 ? Tag.Color : Color.FromArgb((byte)(Tag.Color.A / 4), Tag.Color.R, Tag.Color.G, Tag.Color.B));


    public TagViewModel(ITag tag)
    {
        this.Tag = tag;
        // this.Tag.PropChanged += Tag_PropertyChanged;
    }

    private void Tag_PropertyChanged(object sender, Microsoft.Windows.CommandPalette.Extensions.PropChangedEventArgs args)
    {
        this.PropertyChanged?.Invoke(this, new(args.PropertyName));
    }
}
