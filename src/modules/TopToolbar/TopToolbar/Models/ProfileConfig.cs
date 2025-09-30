// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace TopToolbar.Models;

/// <summary>
/// Complete profile configuration containing all actions and settings for a specific profile
/// </summary>
public class ProfileConfig : INotifyPropertyChanged
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

    private DateTime _lastModified = DateTime.UtcNow;

    public DateTime LastModified
    {
        get => _lastModified;
        set
        {
            if (_lastModified != value)
            {
                _lastModified = value;
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

    private ProfileChatActionConfig _chatConfig = new();

    public ProfileChatActionConfig ChatConfig
    {
        get => _chatConfig;
        set
        {
            if (_chatConfig != value)
            {
                _chatConfig = value ?? new ProfileChatActionConfig();
                OnPropertyChanged();
            }
        }
    }

    private Dictionary<string, object> _metadata = new();

    public Dictionary<string, object> Metadata
    {
        get => _metadata;
        set
        {
            if (_metadata != value)
            {
                _metadata = value ?? new Dictionary<string, object>();
                OnPropertyChanged();
            }
        }
    }

    public ProfileConfig Clone()
    {
        return new ProfileConfig
        {
            Id = Id,
            Name = Name,
            Description = Description,
            CreatedAt = CreatedAt,
            LastModified = DateTime.UtcNow,
            Groups = Groups.ConvertAll(g => g.Clone()),
            ChatConfig = ChatConfig.Clone(),
            Metadata = new Dictionary<string, object>(Metadata),
        };
    }
}
