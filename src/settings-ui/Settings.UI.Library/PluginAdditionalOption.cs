// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using System.Text.Json.Serialization;

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
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string DisplayDescription { get; set; }

        /// <summary>
        /// Gets or sets a value to show a label for the second setting if two combined settings are shown
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string SecondDisplayLabel { get; set; }

        /// <summary>
        /// Gets or sets a value to show a description for the second setting in the settings ui if two combined settings are shown. (Optional)
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string SecondDisplayDescription { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the checkbox is set or not set
        /// </summary>
        public bool Value { get; set; }

        public int ComboBoxValue { get; set; }

        /// <summary>
        /// Gets or sets the list of dropdown items for the ComboBox. Please use the item name as Key and an integer as Value.
        /// The value gets converted in settings UI to an integer and will be saved in <see cref="ComboBoxValue"/>.
        /// You can define the visibility order in settings ui by arranging the list items.
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public List<KeyValuePair<string, string>> ComboBoxItems { get; set; }

        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string TextValue { get; set; }

        /// <summary>
        /// Gets or sets the value that specifies the maximum number of characters allowed for user input in the text box. (Optional; Default is 0 which means no limit.)
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? TextBoxMaxLength { get; set; }

        public double NumberValue { get; set; }

        /// <summary>
        /// Gets or sets a minimal value for the number box. (Optional; Default is Double.MinValue)
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? NumberBoxMin { get; set; }

        /// <summary>
        /// Gets or sets a maximal value for the number box. (Optional; Default is Double.MaxValue)
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? NumberBoxMax { get; set; }

        /// <summary>
        /// Gets or sets the value for small changes of the number box. (Optional; Default is 1)
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? NumberBoxSmallChange { get; set; }

        /// <summary>
        /// Gets or sets the value for large changes of the number box. (Optional; Default is 10)
        /// </summary>
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public double? NumberBoxLargeChange { get; set; }

        // Outdated properties kept for backward compatibility with third-party plugins. (They are only required to not have old third-party plugins crashing when propagating their plugin options.)
#pragma warning disable SA1623 // Property summary documentation should match accessors

        /// <summary>
        /// PLEASE DON'T USE ANYMORE!! (The property was used for the list of combobox items in the past and is not functional anymore.)
        /// </summary>
        [JsonIgnore]
        public List<string> ComboBoxOptions { get; set; }
#pragma warning restore SA1623 // Property summary documentation should match accessors
    }
}
