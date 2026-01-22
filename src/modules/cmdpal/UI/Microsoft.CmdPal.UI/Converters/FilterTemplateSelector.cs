// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.Core.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

internal sealed partial class FilterTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Default { get; set; }

    public DataTemplate? Separator { get; set; }

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "Microsoft.UI.Xaml.Controls.ComboBoxItem", "Microsoft.WinUI")]
    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject dependencyObject)
    {
        DataTemplate? dataTemplate = Default;

        if (dependencyObject is ComboBoxItem comboBoxItem)
        {
            comboBoxItem.IsEnabled = true;

            if (item is SeparatorViewModel)
            {
                comboBoxItem.IsEnabled = false;
                comboBoxItem.AllowFocusWhenDisabled = false;
                comboBoxItem.AllowFocusOnInteraction = false;
                dataTemplate = Separator;
            }
        }

        return dataTemplate;
    }
}
