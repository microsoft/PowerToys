// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using SamplePagesExtension.Pages.SectionsPages;

namespace SamplePagesExtension.Pages;

internal sealed partial class SectionsIndexPage : ListPage
{
    public SectionsIndexPage()
    {
        Name = "Sections Index Page";
        Icon = new IconInfo("\uF168");
    }

    public override IListItem[] GetItems()
    {
        return [
               new ListItem(new SampleListPageWithSections())
               {
                   Title = "A list page with sections",
               },
               new ListItem(new SampleListPageWithSections(new SmallGridLayout()))
               {
                   Title = "A small grid page with sections",
               },
               new ListItem(new SampleListPageWithSections(new MediumGridLayout()))
               {
                   Title = "A medium grid page with sections",
               },
               new ListItem(new SampleListPageWithSections(new GalleryGridLayout()))
               {
                   Title = "A Gallery grid page with sections",
               },
            ];
    }
}
