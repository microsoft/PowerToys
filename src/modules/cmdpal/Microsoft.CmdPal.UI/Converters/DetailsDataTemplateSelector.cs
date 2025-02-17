// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

public partial class DetailsDataTemplateSelector : DataTemplateSelector
{
    // Define the (currently empty) data templates to return
    // These will be "filled-in" in the XAML code.
    public DataTemplate? LinkTemplate { get; set; }

    public DataTemplate? SeparatorTemplate { get; set; }

    public DataTemplate? TagTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        if (item is DetailsElementViewModel element)
        {
            var data = element;
            return data switch
            {
                DetailsSeparatorViewModel => SeparatorTemplate,
                DetailsLinkViewModel => LinkTemplate,
                DetailsTagsViewModel => TagTemplate,
                _ => null,
            };
        }

        return null;
    }
}
