// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace SamplePagesExtension;

public partial class EvilSamplesPage : ListPage
{
    private readonly IListItem[] _commands = [
       new ListItem(new EvilSampleListPage())
       {
           Title = "List Page without items",
           Subtitle = "Throws exception on GetItems",
       },
       new ListItem(new ExplodeInFiveSeconds(false))
       {
           Title = "Page that will throw an exception after loading it",
           Subtitle = "Throws exception on GetItems _after_ a ItemsChanged",
       },
       new ListItem(new ExplodeInFiveSeconds(true))
       {
           Title = "Page that keeps throwing exceptions",
           Subtitle = "Will throw every 5 seconds once you open it",
       },
       new ListItem(new SelfImmolateCommand())
       {
           Title = "Terminate this extension",
           Subtitle = "Will exit this extension (while it's loaded!)",
       },
    ];

    public EvilSamplesPage()
    {
        Name = "Evil Samples";
        Icon = new("👿"); // Info
    }

    public override IListItem[] GetItems() => _commands;
}
