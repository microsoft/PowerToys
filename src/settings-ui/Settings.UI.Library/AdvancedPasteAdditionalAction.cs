// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace Microsoft.PowerToys.Settings.UI.Library;

public sealed partial class AdvancedPasteAdditionalAction : Observable, IAdvancedPasteAction
{
    private HotkeySettings _shortcut = new();
    private HotkeySettings _coachingShortcut = new();
    private bool _isShown;
    private string _prompt = string.Empty;
    private string _systemPrompt = string.Empty;
    private string _coachingPrompt = string.Empty;
    private string _coachingSystemPrompt = string.Empty;
    private string _providerId = string.Empty;
    private bool _coachingEnabled;
    private bool _hasConflict;
    private string _tooltip;

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

    [JsonPropertyName("coaching-shortcut")]
    public HotkeySettings CoachingShortcut
    {
        get => _coachingShortcut;
        set
        {
            if (_coachingShortcut != value)
            {
                _coachingShortcut = value ?? new();
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

    [JsonPropertyName("prompt")]
    public string Prompt
    {
        get => _prompt;
        set => Set(ref _prompt, value ?? string.Empty);
    }

    [JsonPropertyName("system-prompt")]
    public string SystemPrompt
    {
        get => _systemPrompt;
        set => Set(ref _systemPrompt, value ?? string.Empty);
    }

    [JsonPropertyName("coaching-prompt")]
    public string CoachingPrompt
    {
        get => _coachingPrompt;
        set => Set(ref _coachingPrompt, value ?? string.Empty);
    }

    [JsonPropertyName("coaching-system-prompt")]
    public string CoachingSystemPrompt
    {
        get => _coachingSystemPrompt;
        set => Set(ref _coachingSystemPrompt, value ?? string.Empty);
    }

    [JsonPropertyName("provider-id")]
    public string ProviderId
    {
        get => _providerId;
        set => Set(ref _providerId, value ?? string.Empty);
    }

    [JsonPropertyName("coaching-enabled")]
    public bool CoachingEnabled
    {
        get => _coachingEnabled;
        set => Set(ref _coachingEnabled, value);
    }

    [JsonIgnore]
    public bool HasConflict
    {
        get => _hasConflict;
        set => Set(ref _hasConflict, value);
    }

    [JsonIgnore]
    public string Tooltip
    {
        get => _tooltip;
        set => Set(ref _tooltip, value);
    }

    [JsonIgnore]
    public IEnumerable<IAdvancedPasteAction> SubActions => [];
}
