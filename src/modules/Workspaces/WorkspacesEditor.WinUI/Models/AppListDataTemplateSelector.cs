// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace WorkspacesEditor.Models
{
    public sealed partial class AppListDataTemplateSelector : DataTemplateSelector
    {
        public DataTemplate HeaderTemplate { get; set; }

        public DataTemplate AppTemplate { get; set; }

        protected override DataTemplate SelectTemplateCore(object item)
        {
            return item is MonitorHeaderRow ? HeaderTemplate : AppTemplate;
        }

        protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
        {
            return SelectTemplateCore(item);
        }
    }
}
