// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PluginAdditionalOption
    {
        public enum SelectionType
        {
            Checkbox = 0,
            Combobox = 1,
        }

        public string Key { get; set; }

        public string DisplayLabel { get; set; }

        /// <summary>
        /// Gets or sets a value to show a description of this setting in the settings ui. (Optional)
        /// </summary>
        public string DisplayDescription { get; set; }

        public bool Value { get; set; }

        public List<string> ComboBoxOptions { get; set; }

        public int Option { get; set; }

        public int SelectionTypeValue { get; set; }
    }
}
