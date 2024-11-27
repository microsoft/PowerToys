// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library;

public sealed class AdvancedPasteCustomAction : Observable, IAdvancedPasteAction, ICloneable
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
        set => Set(ref _id, value);
    }

    [JsonPropertyName("name")]
    public string Name
    {
        get => _name;
        set
        {
            if (Set(ref _name, value))
            {
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
            if (Set(ref _prompt, value))
            {
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
        set => Set(ref _isShown, value);
    }

    [JsonIgnore]
    public bool CanMoveUp
    {
        get => _canMoveUp;
        set => Set(ref _canMoveUp, value);
    }

    [JsonIgnore]
    public bool CanMoveDown
    {
        get => _canMoveDown;
        set => Set(ref _canMoveDown, value);
    }

    [JsonIgnore]
    public bool IsValid
    {
        get => _isValid;
        private set => Set(ref _isValid, value);
    }

    [JsonIgnore]
    public IEnumerable<IAdvancedPasteAction> SubActions => [];

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
