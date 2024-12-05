// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace SamplePagesExtension;

public partial class SamplesListPage : ListPage
{
    private readonly IListItem[] _commands = [
       new ListItem(new SampleMarkdownPage())
       {
           Title = "Markdown Page Sample Command",
           Subtitle = "Display a page of rendered markdown",
       },
       new ListItem(new SampleListPage())
       {
           Title = "List Page Sample Command",
           Subtitle = "Display a list of items",
       },
       new ListItem(new SampleFormPage())
       {
           Title = "Form Page Sample Command",
           Subtitle = "Define inputs to retrieve input from the user",
       },
       new ListItem(new SampleListPageWithDetails())
       {
           Title = "List Page With Details Sample Command",
           Subtitle = "A list of items, each with additional details to display",
       },
       new ListItem(new SampleUpdatingItemsPage())
       {
           Title = "List page with items that change",
           Subtitle = "The items on the list update themselves in real time",
       },
       new ListItem(new SampleDynamicListPage())
       {
           Title = "Dynamic List Page Command",
           Subtitle = "Changes the list of items in response to the typed query",
       },
       new ListItem(new SampleSettingsPage())
       {
           Title = "Sample settings page",
           Subtitle = "A demo of the settings helpers",
       },
       new ListItem(new EvilSamplesPage())
       {
           Title = "Evil samples",
           Subtitle = "Samples designed to break the palette in many different evil ways",
       }
    ];

    public SamplesListPage()
    {
        Name = "Samples";
        Icon = new("\ue946"); // Info
    }

    public override IListItem[] GetItems() => _commands;
}
