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
            this.Add(new Keys { From = "A", To = "B" });
            this.Add(new Keys { From = "B", To = "A" });
            this.Add(new Keys { From = "Ctrl", To = "Shift" });
            this.Add(new Keys { From = "Shift", To = "Ctrl" });
            this.Add(new Keys { From = "A", To = "B" });
            this.Add(new Keys { From = "B", To = "B" });
            this.Add(new Keys { From = "Ctrl", To = "Shift" });
            this.Add(new Keys { From = "Shift", To = "Ctrl" });
        }
    }

    public class Keys
    {
        public string From { get; set; }

        public string To { get; set; }
    }
}
