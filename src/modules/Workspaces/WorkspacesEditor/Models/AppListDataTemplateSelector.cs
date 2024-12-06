// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace WorkspacesEditor.Models
{
    public sealed class AppListDataTemplateSelector : System.Windows.Controls.DataTemplateSelector
    {
        public System.Windows.DataTemplate HeaderTemplate { get; set; }

        public System.Windows.DataTemplate AppTemplate { get; set; }

        public AppListDataTemplateSelector()
        {
            HeaderTemplate = new System.Windows.DataTemplate();
            AppTemplate = new System.Windows.DataTemplate();
        }

        public override System.Windows.DataTemplate SelectTemplate(object item, System.Windows.DependencyObject container)
        {
            return item is MonitorHeaderRow ? HeaderTemplate : AppTemplate;
        }
    }
}
