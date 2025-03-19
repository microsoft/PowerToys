// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Microsoft.CmdPal.UI;

public partial class ContentTemplateSelector : DataTemplateSelector
{
    // Define the (currently empty) data templates to return
    // These will be "filled-in" in the XAML code.
    public DataTemplate? FormTemplate { get; set; }

    public DataTemplate? MarkdownTemplate { get; set; }

    public DataTemplate? TreeTemplate { get; set; }

    protected override DataTemplate? SelectTemplateCore(object item)
    {
        return item is ContentViewModel element
            ? element switch
            {
                ContentFormViewModel => FormTemplate,
                ContentMarkdownViewModel => MarkdownTemplate,
                ContentTreeViewModel => TreeTemplate,
                _ => null,
            }
            : null;
    }
}
