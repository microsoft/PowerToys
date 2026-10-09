// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using AdvancedPaste.Models;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;

namespace AdvancedPaste.Controls;

public sealed partial class PasteFormatTemplateSelector : DataTemplateSelector
{
    public DataTemplate ItemTemplate { get; set; }

    public DataTemplate ItemTemplateDisabled { get; set; }

    protected override DataTemplate SelectTemplateCore(object item, DependencyObject container)
    {
        bool isEnabled = item is PasteFormat pasteFormat && pasteFormat.IsEnabled;

        if (container is SelectorItem selector)
        {
            selector.IsEnabled = isEnabled;
        }

        return isEnabled ? ItemTemplate : ItemTemplateDisabled;
    }
}
