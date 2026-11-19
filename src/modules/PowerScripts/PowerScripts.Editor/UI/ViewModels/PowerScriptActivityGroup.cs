// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;
using Microsoft.PowerToys.Settings.UI.Library.Helpers;

namespace PowerScripts.UI.ViewModels;

public sealed class PowerScriptActivityGroup : Observable
{
    private string _header = string.Empty;

    public required string Key { get; init; }

    public string Header
    {
        get => _header;
        set => Set(ref _header, value);
    }

    public ObservableCollection<PowerScriptActivityItem> Items { get; } = new();
}
