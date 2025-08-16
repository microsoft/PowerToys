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
    private bool _isShown;
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

    [JsonPropertyName("isShown")]
    public bool IsShown
    {
        get => _isShown;
        set => Set(ref _isShown, value);
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
