// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

internal sealed partial class FilterTemplateSelector : DataTemplateSelector
{
    public DataTemplate? Default { get; set; }

    public DataTemplate? Separator { get; set; }

    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "Microsoft.UI.Xaml.Controls.ComboBoxItem", "Microsoft.WinUI")]
    [DynamicDependency(DynamicallyAccessedMemberTypes.All, "Microsoft.UI.Xaml.Controls.ListViewItem", "Microsoft.WinUI")]
    protected override DataTemplate? SelectTemplateCore(object item, DependencyObject dependencyObject)
    {
        DataTemplate? dataTemplate = Default;

        var isSeparator = item is SeparatorViewModel;

        switch (dependencyObject)
        {
            case ComboBoxItem comboBoxItem:
                comboBoxItem.IsEnabled = !isSeparator;
                if (isSeparator)
                {
                    comboBoxItem.AllowFocusWhenDisabled = false;
                    comboBoxItem.AllowFocusOnInteraction = false;
                }

                break;
            case ListViewItem listViewItem:
                listViewItem.IsEnabled = !isSeparator;
                if (isSeparator)
                {
                    listViewItem.MinHeight = 0;
                    listViewItem.IsHitTestVisible = false;
                }

                break;
        }

        if (isSeparator)
        {
            dataTemplate = Separator;
        }

        return dataTemplate;
    }
}
