// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using TopToolbar.Models.Abstractions;

namespace TopToolbar.Models;

/// <summary>
/// A group of actions within a profile
/// </summary>
public class ProfileGroup : INotifyPropertyChanged, IProfileGroup
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

    private List<ProfileAction> _actions = new();

    public List<ProfileAction> Actions
    {
        get => _actions;
        set
        {
            if (_actions != value)
            {
                _actions = value ?? new List<ProfileAction>();
                OnPropertyChanged();
            }
        }
    }

    IReadOnlyList<IProfileAction> IProfileGroup.Actions => _actions.Cast<IProfileAction>().ToList();

    public IEnumerable<ProfileAction> GetActiveActions()
    {
        return (Actions ?? new List<ProfileAction>())
            .Where(a => a != null && a.IsEnabled)
            .OrderBy(a => a.SortOrder)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    IEnumerable<IProfileAction> IProfileGroup.GetActiveActions() => GetActiveActions().Cast<IProfileAction>();

    public ProfileGroup Clone()
    {
        return new ProfileGroup
        {
            Id = Id,
            Name = Name,
            Description = Description,
            IsEnabled = IsEnabled,
            SortOrder = SortOrder,
            Actions = Actions.ConvertAll(a => a.Clone()),
        };
    }
}
