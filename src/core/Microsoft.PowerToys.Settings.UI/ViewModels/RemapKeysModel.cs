// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

namespace Microsoft.PowerToys.Settings.UI.ViewModels
{
    // Dummy data model for the UI. Will be removed moving forward.
    public class RemapKeysModel : ObservableCollection<Keys>
    {
        public RemapKeysModel()
        {
            Add(new Keys { From = "A", To = "B" });
            Add(new Keys { From = "B", To = "A" });
            Add(new Keys { From = "Ctrl", To = "Shift" });
            Add(new Keys { From = "Shift", To = "Ctrl" });
            Add(new Keys { From = "A", To = "B" });
            Add(new Keys { From = "B", To = "B" });
            Add(new Keys { From = "Ctrl", To = "Shift" });
            Add(new Keys { From = "Shift", To = "Ctrl" });
        }
    }
}
