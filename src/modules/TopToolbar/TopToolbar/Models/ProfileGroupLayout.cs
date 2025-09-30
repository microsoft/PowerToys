// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TopToolbar.Models;

/// <summary>
/// Layout configuration for a profile group
/// </summary>
public class ProfileGroupLayout : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _style = "horizontal";

    public string Style
    {
        get => _style;
        set
        {
            if (_style != value)
            {
                _style = value ?? "horizontal";
                OnPropertyChanged();
            }
        }
    }

    private string _overflow = "wrap";

    public string Overflow
    {
        get => _overflow;
        set
        {
            if (_overflow != value)
            {
                _overflow = value ?? "wrap";
                OnPropertyChanged();
            }
        }
    }

    private int _maxInline = 6;

    public int MaxInline
    {
        get => _maxInline;
        set
        {
            if (_maxInline != value)
            {
                _maxInline = value;
                OnPropertyChanged();
            }
        }
    }

    private bool _showLabels = true;

    public bool ShowLabels
    {
        get => _showLabels;
        set
        {
            if (_showLabels != value)
            {
                _showLabels = value;
                OnPropertyChanged();
            }
        }
    }

    public ProfileGroupLayout Clone()
    {
        return new ProfileGroupLayout
        {
            Style = Style,
            Overflow = Overflow,
            MaxInline = MaxInline,
            ShowLabels = ShowLabels,
        };
    }
}
