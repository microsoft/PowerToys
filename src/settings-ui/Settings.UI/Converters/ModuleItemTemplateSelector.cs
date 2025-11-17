// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.PowerToys.Settings.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.PowerToys.Settings.UI.Converters
{
    public partial class ModuleItemTemplateSelector : DataTemplateSelector
    {
        public DataTemplate ShortcutTemplate { get; set; }

        public DataTemplate ActivationTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            switch (item)
            {
                case DashboardModuleShortcutItem: return ShortcutTemplate;
                case DashboardModuleActivationItem: return ActivationTemplate;
                default: return ActivationTemplate;
            }
        }
    }
}
