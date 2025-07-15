// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Data;

namespace Microsoft.CmdPal.Core.Control;

internal sealed partial class ContextItemTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Default { get; set; }

    public DataTemplate? Critical { get; set; }

    public DataTemplate? Separator { get; set; }

    // [DynamicWindowsRuntimeCast(typeof(ListViewItem))]
    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject dependencyObject)
    {
        DataTemplate? dataTemplate = Default;

        if (dependencyObject is ListViewItem li)
        {
            li.IsEnabled = true;

            if (item is SeparatorContextItemViewModel)
            {
                li.IsEnabled = false;
                li.AllowFocusWhenDisabled = false;
                li.AllowFocusOnInteraction = false;
                dataTemplate = Separator;
            }
            else
            {
                dataTemplate = ((CommandContextItemViewModel)item).IsCritical ? Critical : Default;
            }
        }

        return dataTemplate;
    }
}
