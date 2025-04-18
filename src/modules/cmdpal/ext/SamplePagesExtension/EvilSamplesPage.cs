// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

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
       new ListItem(new ExplodeOnPropChange())
       {
           Title = "Throw in the middle of a PropChanged",
           Subtitle = "Will throw every 5 seconds once you open it",
       },
       new ListItem(new SelfImmolateCommand())
       {
           Title = "Terminate this extension",
           Subtitle = "Will exit this extension (while it's loaded!)",
       },
        new ListItem(new NoOpCommand())
        {
           Title = "I have lots of nulls",
           Subtitle = null,
           MoreCommands = null,
           Tags = null,
           Details = new Details()
           {
               Title = null,
               HeroImage = null,
               Metadata = null,
           },
        },
        new ListItem(new NoOpCommand())
        {
           Title = "I also have nulls",
           Subtitle = null,
           MoreCommands = null,
           Details = new Details()
           {
               Title = null,
               HeroImage = null,
               Metadata = [new DetailsElement() { Key = "Oops all nulls", Data = new DetailsTags() { Tags = null } }],
           },
        },
        new ListItem(new AnonymousCommand(action: () =>
        {
            ToastStatusMessage toast = new("I should appear immediately");
            toast.Show();
            Thread.Sleep(5000);
        }) { Result = CommandResult.KeepOpen() })
        {
           Title = "I take just forever to return something",
           Subtitle = "The toast should appear immediately.",
           MoreCommands = null,
           Details = new Details()
           {
               Body = "This is a test for GH#512. If it doesn't appear immediately, it's likely InvokeCommand is happening on the UI thread.",
           },
        }
    ];

    public EvilSamplesPage()
    {
        Name = "Evil Samples";
        Icon = new IconInfo("👿"); // Info
    }

    public override IListItem[] GetItems() => _commands;
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Sample code")]
internal sealed partial class ExplodeOnPropChange : ListPage
{
    private bool _explode;

    public override string Title
    {
        get => _explode ? Commands[9001].Title : base.Title;
        set => base.Title = value;
    }

    private IListItem[] Commands => [
      new ListItem(new NoOpCommand())
           {
               Title = "This page will explode in five seconds!",
               Subtitle = "I'll change my Name, then explode",
           },
        ];

    public ExplodeOnPropChange()
    {
        Icon = new IconInfo(string.Empty);
        Name = "Open";
    }

    public override IListItem[] GetItems()
    {
        _ = Task.Run(() =>
        {
            Thread.Sleep(1000);
            Title = "Ready? 3...";
            Thread.Sleep(1000);
            Title = "Ready? 2...";
            Thread.Sleep(1000);
            Title = "Ready? 1...";
            Thread.Sleep(1000);
            _explode = true;
            Title = "boom";
        });
        return Commands;
    }
}
