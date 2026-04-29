// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

#nullable enable

using System.IO;
using System.Threading.Tasks;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;
using PowerToys.MacroCommon.Models;
using PowerToys.MacroCommon.Serialization;

namespace Microsoft.PowerToys.Settings.UI.ViewModels;

public sealed class MacroListItem : Observable
{
    private MacroDefinition _definition;
    private bool _isEnabled;

    public MacroListItem(MacroDefinition definition, string filePath)
    {
        _definition = definition;
        _isEnabled = definition.IsEnabled;
        FilePath = filePath;
    }

    public MacroDefinition Definition => _definition;

    public string FilePath { get; }

    public string Name => _definition.Name;

    public string? Hotkey => _definition.Hotkey;

    public bool HasHotkey => !string.IsNullOrEmpty(_definition.Hotkey);

    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (Set(ref _isEnabled, value))
            {
                _definition = _definition with { IsEnabled = value };
                string json = MacroSerializer.Serialize(_definition);
                string path = FilePath;
                _ = Task.Run(() => File.WriteAllText(path, json));
            }
        }
    }
}
