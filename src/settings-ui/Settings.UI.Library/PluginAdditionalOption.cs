// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;

namespace Microsoft.PowerToys.Settings.UI.Library
{
    public class PluginAdditionalOption
    {
        public enum AdditionalOptionType
        {
            Checkbox = 0,
            Combobox = 1,
            Textbox = 2,
            Numberbox = 3,
            CheckboxAndCombobox = 11,
            CheckboxAndTextbox = 12,
            CheckboxAndNumberbox = 13,
        }

        /// <summary>
        /// Gets or sets the layout type of the option in settings ui (Optional; Default is checkbox)
        /// </summary>
        public AdditionalOptionType PluginOptionType { get; set; }

        public string Key { get; set; }

        public string DisplayLabel { get; set; }

        /// <summary>
        /// Gets or sets a value to show a description of this setting in the settings ui. (Optional)
        /// </summary>
        public string DisplayDescription { get; set; }

        /// <summary>
        /// Gets or sets a value to show a label for the second setting if two combined settings are shown
        /// </summary>
        public string SecondDisplayLabel { get; set; }

        /// <summary>
        /// Gets or sets a value to show a description for the second setting in the settings ui if two combined settings are shown. (Optional)
        /// </summary>
        public string SecondDisplayDescription { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the checkbox is set or not set
        /// </summary>
        public bool Value { get; set; }

        public List<string> ComboBoxOptions { get; set; }

        public int ComboBoxValue { get; set; }

        public string TextValue { get; set; }

        public double NumberValue { get; set; }

        /// <summary>
        /// Gets or sets a minimal value for the number box. (Optional; Default is Double.MinValue)
        /// </summary>
        public double? NumberBoxMin { get; set; }

        /// <summary>
        /// Gets or sets a maximal value for the number box. (Optional; Default is Double.MaxValue)
        /// </summary>
        public double? NumberBoxMax { get; set; }

        /// <summary>
        /// Gets or sets the value for small changes of the number box. (Optional; Default is 1)
        /// </summary>
        public double? NumberBoxSmallChange { get; set; }

        /// <summary>
        /// Gets or sets the value for large changes of the number box. (Optional; Default is 10)
        /// </summary>
        public double? NumberBoxLargeChange { get; set; }
    }
}
