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
/// A complete profile containing metadata and its own actions
/// </summary>
public class Profile : INotifyPropertyChanged, IProfile
{
    public event PropertyChangedEventHandler PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

    private string _id = string.Empty;

    public string Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value ?? string.Empty;
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

    private DateTime _createdAt = DateTime.UtcNow;

    public DateTime CreatedAt
    {
        get => _createdAt;
        set
        {
            if (_createdAt != value)
            {
                _createdAt = value;
                OnPropertyChanged();
            }
        }
    }

    private DateTime _modifiedAt = DateTime.UtcNow;

    public DateTime ModifiedAt
    {
        get => _modifiedAt;
        set
        {
            if (_modifiedAt != value)
            {
                _modifiedAt = value;
                OnPropertyChanged();
            }
        }
    }

    private List<ProfileGroup> _groups = new();

    public List<ProfileGroup> Groups
    {
        get => _groups;
        set
        {
            if (_groups != value)
            {
                _groups = value ?? new List<ProfileGroup>();
                OnPropertyChanged();
            }
        }
    }

    IReadOnlyList<IProfileGroup> IProfile.Groups => _groups.Cast<IProfileGroup>().ToList();

    /// <summary>
    /// Returns a new list of groups that are currently considered "active" for rendering/execution.
    /// Active definition (v1): Group.IsEnabled == true AND there is at least one enabled action inside it.
    /// Ordering: by SortOrder ascending, then by Name (stable deterministic ordering for UI diffing).
    /// This keeps filtering logic in the data model layer instead of scattering IsEnabled checks in UI code.
    /// </summary>
    public List<ProfileGroup> GetActiveGroups()
    {
        // Defensive: treat null or mutated list gracefully; never throw.
        IEnumerable<ProfileGroup> source = Groups ?? new List<ProfileGroup>();

        // Filter groups first.
        var active = new List<ProfileGroup>();
        foreach (var g in source)
        {
            if (g == null || !g.IsEnabled)
            {
                continue;
            }

            // Evaluate enabled actions; if none, skip group.
            var enabledActions = g.Actions?.Where(a => a != null && a.IsEnabled).ToList() ?? new List<ProfileAction>();
            if (enabledActions.Count == 0)
            {
                continue;
            }

            active.Add(g);
        }

        // Order groups deterministically.
        return active
            .OrderBy(g => g.SortOrder)
            .ThenBy(g => g.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Helper to get enabled actions for a given group id (returns empty list if group missing or has none).
    /// Provided for callers that need quick access without re-filtering all groups.
    /// </summary>
    // Explicit interface for active groups
    IEnumerable<IProfileGroup> IProfile.GetActiveGroups() => GetActiveGroups().Cast<IProfileGroup>();
}
