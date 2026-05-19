// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using KeyboardManagerEditorUI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace KeyboardManagerEditorUI.Controls
{
    public sealed partial class TemplateParameterSelector : DataTemplateSelector
    {
        public DataTemplate? TextTemplate { get; set; }

        public DataTemplate? ComboTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            if (item is TemplateParameterViewModel vm)
            {
                return vm.Type switch
                {
                    "Combo" => ComboTemplate ?? TextTemplate!,
                    _       => TextTemplate!,
                };
            }

            return TextTemplate!;
        }
    }
}
