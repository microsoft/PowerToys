// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace Microsoft.PowerToys.Settings.UI.Library;

public class AdvancedPasteAIServiceParameter : INotifyPropertyChanged
{
    public string Name { get; set; }

    public string DisplayName { get; set; }

    public string Type { get; set; }

    public string Description { get; set; }

    public object Value { get; set; }

    public event PropertyChangedEventHandler PropertyChanged;

    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
