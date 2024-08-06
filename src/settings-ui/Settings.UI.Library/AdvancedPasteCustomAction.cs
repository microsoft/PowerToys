// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library;

public sealed class AdvancedPasteCustomAction : INotifyPropertyChanged, ICloneable
{
    private int _id;
    private string _name = string.Empty;
    private string _prompt = string.Empty;
    private HotkeySettings _shortcut = new();
    private bool _isShown;
    private bool _canMoveUp;
    private bool _canMoveDown;
    private bool _isValid;

    [JsonPropertyName("id")]
    public int Id
    {
        get => _id;
        set
        {
            if (_id != value)
            {
                _id = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set
        {
            if (_name != value)
            {
                _name = value;
                OnPropertyChanged();
                UpdateIsValid();
            }
        }
    }

    [JsonPropertyName("prompt")]
    public string Prompt
    {
        get => _prompt;
        set
        {
            if (_prompt != value)
            {
                _prompt = value;
                OnPropertyChanged();
                UpdateIsValid();
            }
        }
    }

    [JsonPropertyName("shortcut")]
    public HotkeySettings Shortcut
    {
        get => _shortcut;
        set
        {
            if (_shortcut != value)
            {
                // We null-coalesce here rather than outside this branch as we want to raise PropertyChanged when the setter is called
                // with null; the ShortcutControl depends on this.
                _shortcut = value ?? new();

                OnPropertyChanged();
            }
        }
    }

    [JsonPropertyName("isShown")]
    public bool IsShown
    {
        get => _isShown;
        set
        {
            if (_isShown != value)
            {
                _isShown = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonIgnore]
    public bool CanMoveUp
    {
        get => _canMoveUp;
        set
        {
            if (_canMoveUp != value)
            {
                _canMoveUp = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonIgnore]
    public bool CanMoveDown
    {
        get => _canMoveDown;
        set
        {
            if (_canMoveDown != value)
            {
                _canMoveDown = value;
                OnPropertyChanged();
            }
        }
    }

    [JsonIgnore]
    public bool IsValid
    {
        get => _isValid;
        private set
        {
            if (_isValid != value)
            {
                _isValid = value;
                OnPropertyChanged();
            }
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public string ToJsonString() => JsonSerializer.Serialize(this);

    public object Clone()
    {
        AdvancedPasteCustomAction clone = new();
        clone.Update(this);
        return clone;
    }

    public void Update(AdvancedPasteCustomAction other)
    {
        Id = other.Id;
        Name = other.Name;
        Prompt = other.Prompt;
        Shortcut = other.GetShortcutClone();
        IsShown = other.IsShown;
        CanMoveUp = other.CanMoveUp;
        CanMoveDown = other.CanMoveDown;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private HotkeySettings GetShortcutClone()
    {
        object shortcut = null;
        if (Shortcut.TryToCmdRepresentable(out string shortcutString))
        {
            _ = HotkeySettings.TryParseFromCmd(shortcutString, out shortcut);
        }

        return (shortcut as HotkeySettings) ?? new HotkeySettings();
    }

    private void UpdateIsValid()
    {
        IsValid = !string.IsNullOrWhiteSpace(Name) && !string.IsNullOrWhiteSpace(Prompt);
    }
}
