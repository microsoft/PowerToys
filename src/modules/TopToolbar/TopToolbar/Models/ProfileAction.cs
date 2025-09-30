// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using TopToolbar.Models.Abstractions;

namespace TopToolbar.Models;

/// <summary>
/// An action within a profile group
/// </summary>
public class ProfileAction : INotifyPropertyChanged, IProfileAction
{
    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _id = Guid.NewGuid().ToString();

    public string Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value ?? Guid.NewGuid().ToString();
                OnPropertyChanged();
            }
        }
    }

    private string _name = string.Empty;

    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    private string _description = string.Empty;

    public string Description
    {
        get => _description;
        set
        {
            if (_description != value)
            {
                _description = value ?? string.Empty;
                OnPropertyChanged();
            }
        }
    }

    private bool _isEnabled = true;

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (_isEnabled != value)
            {
                _isEnabled = value;
                OnPropertyChanged();
            }
        }
    }

    private int _sortOrder;

    public int SortOrder
    {
        get => _sortOrder;
        set
        {
            if (_sortOrder != value)
            {
                _sortOrder = value;
                OnPropertyChanged();
            }
        }
    }

    // ActionType removed from minimal contract (execution layer can extend via pattern matching)
    private string _iconGlyph = "\uE8EF";

    public string IconGlyph
    {
        get => _iconGlyph;
        set
        {
            if (_iconGlyph != value)
            {
                _iconGlyph = value ?? "\uE8EF";
                OnPropertyChanged();
            }
        }
    }

    // Removed IconPath/IconType for minimal glyph-only representation

    // Command Line Action Properties
    // Removed command-line execution fields from minimal model

    // Provider Action Properties
    // Removed provider action fields

    // Chat Action Properties (for future chat integration)
    // Removed chat config

    // Removed type helper properties
    [JsonIgnore]
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? "Untitled Action" : Name;

    public ProfileAction Clone()
    {
        return new ProfileAction
        {
            Id = Id,
            Name = Name,
            Description = Description,
            IsEnabled = IsEnabled,
            SortOrder = SortOrder,
            IconGlyph = IconGlyph,
        };
    }
}
