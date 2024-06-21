// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.PowerToys.Settings.UI.Library;
using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public sealed class RunOptionTemplateSelector : DataTemplateSelector
    {
        public DataTemplate CheckBoxTemplate { get; set; }

        public DataTemplate ComboBoxTemplate { get; set; }

        public DataTemplate TextboxTemplate { get; set; }

        public DataTemplate NumberBoxTemplate { get; set; }

        public DataTemplate MultilineTextBoxTemplate { get; set; }

        public DataTemplate CheckBoxComboBoxTemplate { get; set; }

        public DataTemplate CheckBoxTextBoxTemplate { get; set; }

        public DataTemplate CheckBoxMultilineTextBoxTemplate { get; set; }

        public DataTemplate CheckBoxNumberBoxTemplate { get; set; }

        /// <summary>
        /// Gets or sets an empty template used as fall back in case of malformed data
        /// </summary>
        public DataTemplate EmptyTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is PluginAdditionalOptionViewModel optionViewModel)
            {
                return optionViewModel.Type switch
                {
                    PluginAdditionalOption.AdditionalOptionType.Checkbox => CheckBoxTemplate,
                    PluginAdditionalOption.AdditionalOptionType.Combobox => ComboBoxTemplate,
                    PluginAdditionalOption.AdditionalOptionType.Textbox => TextboxTemplate,
                    PluginAdditionalOption.AdditionalOptionType.Numberbox => NumberBoxTemplate,
                    PluginAdditionalOption.AdditionalOptionType.MultilineTextbox => MultilineTextBoxTemplate,
                    PluginAdditionalOption.AdditionalOptionType.CheckboxAndCombobox => CheckBoxComboBoxTemplate,
                    PluginAdditionalOption.AdditionalOptionType.CheckboxAndTextbox => CheckBoxTextBoxTemplate,
                    PluginAdditionalOption.AdditionalOptionType.CheckboxAndNumberbox => CheckBoxNumberBoxTemplate,
                    PluginAdditionalOption.AdditionalOptionType.CheckboxAndMultilineTextbox => CheckBoxMultilineTextBoxTemplate,
                    _ => EmptyTemplate,
                };
            }

            throw new ArgumentException("Item must be an PluginAdditionalOptionViewModel", nameof(item));
        }
    }
}
