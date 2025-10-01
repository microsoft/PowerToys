// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

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
        new ListItem(new EvilSlowDynamicPage())
        {
            Title = "Slow loading Dynamic Page",
            Subtitle = "Takes 5 seconds to load each time you type",
            Tags = [new Tag("GH #38190")],
        },
        new ListItem(new EvilFastUpdatesPage())
        {
            Title = "Fast updating Dynamic Page",
            Subtitle = "Updates in the middle of a GetItems call",
            Tags = [new Tag("GH #41149")],
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
        },

        // More edge cases than truly evil
        new ListItem(
            new ToastCommand("Primary command invoked", MessageState.Info) { Name = "Primary command", Icon = new IconInfo("\uF146") }) // dial 1
        {
            Title = "anonymous command test",
            Subtitle = "Try pressing Ctrl+1 with me selected",
            Icon = new IconInfo("\uE712"),  // "More" dots
            MoreCommands = [
                new CommandContextItem(
                    new ToastCommand("Secondary command invoked", MessageState.Warning) { Name = "Secondary command", Icon = new IconInfo("\uF147") }) // dial 2
                {
                    Title = "I'm a second command",
                    RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.Number1),
                },
                new CommandContextItem("nested...")
                {
                    Title = "We can go deeper...",
                    Icon = new IconInfo("\uF148"),
                    RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.Number2),
                    MoreCommands = [
                        new CommandContextItem(
                            new ToastCommand("Nested A invoked") { Name = "Do it", Icon = new IconInfo("A") })
                        {
                            Title = "Nested A",
                            RequestedShortcut = KeyChordHelpers.FromModifiers(alt: true, vkey: VirtualKey.A),
                        },

                        new CommandContextItem(
                            new ToastCommand("Nested B invoked") { Name = "Do it", Icon = new IconInfo("B") })
                        {
                            Title = "Nested B...",
                            RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.B),
                            MoreCommands = [
                                new CommandContextItem(
                                    new ToastCommand("Nested C invoked") { Name = "Do it" })
                                {
                                    Title = "You get it",
                                    RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.B),
                                }
                            ],
                        },
                    ],
                }
            ],
        },
        new ListItem(
            new ToastCommand("Primary command invoked", MessageState.Info) { Name = "Primary command", Icon = new IconInfo("\uF146") }) // dial 1
        {
            Title = "noop command test",
            Subtitle = "Try pressing Ctrl+1 with me selected",
            Icon = new IconInfo("\uE712"),  // "More" dots
            MoreCommands = [
                new CommandContextItem(
                    new ToastCommand("Secondary command invoked", MessageState.Warning) { Name = "Secondary command", Icon = new IconInfo("\uF147") }) // dial 2
                {
                    Title = "I'm a second command",
                    RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.Number1),
                },
                new CommandContextItem(new NoOpCommand())
                {
                    Title = "We can go deeper...",
                    Icon = new IconInfo("\uF148"),
                    RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.Number2),
                    MoreCommands = [
                        new CommandContextItem(
                            new ToastCommand("Nested A invoked") { Name = "Do it", Icon = new IconInfo("A") })
                        {
                            Title = "Nested A",
                            RequestedShortcut = KeyChordHelpers.FromModifiers(alt: true, vkey: VirtualKey.A),
                        },

                        new CommandContextItem(
                            new ToastCommand("Nested B invoked") { Name = "Do it", Icon = new IconInfo("B") })
                        {
                            Title = "Nested B...",
                            RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.B),
                            MoreCommands = [
                                new CommandContextItem(
                                    new ToastCommand("Nested C invoked") { Name = "Do it" })
                                {
                                    Title = "You get it",
                                    RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.B),
                                }
                            ],
                        },
                    ],
                }
            ],
        },
        new ListItem(
            new ToastCommand("Primary command invoked", MessageState.Info) { Name = "Primary command", Icon = new IconInfo("\uF146") }) // dial 1
        {
            Title = "noop secondary command test",
            Subtitle = "Try pressing Ctrl+1 with me selected",
            Icon = new IconInfo("\uE712"),  // "More" dots
            MoreCommands = [
                new CommandContextItem(new NoOpCommand())
                {
                    Title = "We can go deeper...",
                    Icon = new IconInfo("\uF148"),
                    RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.Number2),
                    MoreCommands = [
                        new CommandContextItem(
                            new ToastCommand("Nested A invoked") { Name = "Do it", Icon = new IconInfo("A") })
                        {
                            Title = "Nested A",
                            RequestedShortcut = KeyChordHelpers.FromModifiers(alt: true, vkey: VirtualKey.A),
                        },

                        new CommandContextItem(
                            new ToastCommand("Nested B invoked") { Name = "Do it", Icon = new IconInfo("B") })
                        {
                            Title = "Nested B...",
                            RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.B),
                            MoreCommands = [
                                new CommandContextItem(
                                    new ToastCommand("Nested C invoked") { Name = "Do it" })
                                {
                                    Title = "You get it",
                                    RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.B),
                                }
                            ],
                        },
                    ],
                }
            ],
        },
        new ListItem(
            new ToastCommand("Primary command invoked", MessageState.Info) { Name = "H W\r\nE O\r\nL R\r\nL L\r\nO D", Icon = new IconInfo("\uF146") })
        {
            Title = "noop third command test",
            Icon = new IconInfo("\uE712"),  // "More" dots
        },
        new ListItem(new EvilDuplicateRequestedShortcut())
        {
            Title = "Evil keyboard shortcuts",
            Subtitle = "Two commands with the same shortcut and more...",
            Icon = new IconInfo("\uE765"),
        },
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

/// <summary>
/// This sample simulates a long delay in handling UpdateSearchText. I've found
/// that if I type "124356781234", then somewhere around the second "1234",
/// we'll get into a state where the character is typed, but then CmdPal snaps
/// back to a previous query.
///
/// We can use this to validate that we're always sticking with the last
/// SearchText. My guess is that it's a bug in
/// Toolkit.DynamicListPage.SearchText.set
///
/// see GH #38190
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Sample code")]
internal sealed partial class EvilSlowDynamicPage : DynamicListPage
{
    private IListItem[] _items = [];

    public EvilSlowDynamicPage()
    {
        Icon = new IconInfo(string.Empty);
        Name = "Open";
        Title = "Evil Slow Dynamic Page";
        PlaceholderText = "Type to see items appear after a delay";
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        DoQuery(newSearch);
        RaiseItemsChanged(newSearch.Length);
    }

    public override IListItem[] GetItems()
    {
        return _items.Length > 0 ? _items : DoQuery(SearchText);
    }

    private IListItem[] DoQuery(string newSearch)
    {
        IsLoading = true;

        // Sleep for longer for shorter search terms
        var delay = 10000 - (newSearch.Length * 2000);
        delay = delay < 0 ? 0 : delay;
        if (newSearch.Length == 0)
        {
            delay = 0;
        }

        delay += 50;

        Thread.Sleep(delay); // Simulate a long load time

        var items = newSearch.ToCharArray().Select(ch => new ListItem(new NoOpCommand()) { Title = ch.ToString() }).ToArray();
        if (items.Length == 0)
        {
            items = [new ListItem(new NoOpCommand()) { Title = "Start typing in the search box" }];
        }

        if (items.Length > 0)
        {
            items[0].Subtitle = "Notice how the number of items changes for this page when you type in the filter box";
        }

        IsLoading = false;

        return items;
    }
}

/// <summary>
/// A sample for a page that updates its items in the middle of a GetItems call.
/// In this sample, we're returning 10000 items, which genuinely marshal slowly
/// (even before we start retrieving properties from them).
///
///  While we're in the middle of the marshalling of that GetItems call, the
///  background thread we started will kick off another GetItems (via the
///  RaiseItemsChanged).
///
/// That second GetItems will return a single item, which marshals quickly.
/// CmdPal _should_ only display that single green item. However, as of v0.4,
/// we'll display that green item, then "snap back" to the red items, when they
/// finish marshalling.
///
/// See GH #41149
/// </summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Sample code")]
internal sealed partial class EvilFastUpdatesPage : DynamicListPage
{
    private static readonly IconInfo _red = new("🔴"); // "Red" icon
    private static readonly IconInfo _green = new("🟢"); // "Green" icon

    private IListItem[] _redItems = [];
    private IListItem[] _greenItems = [];
    private bool _sentRed;

    public EvilFastUpdatesPage()
    {
        Icon = new IconInfo(string.Empty);
        Name = "Open";
        Title = "Evil Fast Updates Page";
        PlaceholderText = "Type to trigger an update";

        _redItems = Enumerable.Range(0, 10000).Select(i => new ListItem(new NoOpCommand())
        {
            Icon = _red,
            Title = $"Item {i + 1}",
            Subtitle = "CmdPal is doing it wrong",
        }).ToArray();
        _greenItems = [new ListItem(new NoOpCommand()) { Icon = _green, Title = "It works" }];
    }

    public override void UpdateSearchText(string oldSearch, string newSearch)
    {
        _sentRed = false;
        RaiseItemsChanged();
    }

    public override IListItem[] GetItems()
    {
        if (!_sentRed)
        {
            IsLoading = true;
            _sentRed = true;

            // kick off a task to update the items after a delay
            _ = Task.Run(() =>
            {
                Thread.Sleep(5);
                RaiseItemsChanged();
            });

            return _redItems;
        }
        else
        {
            IsLoading = false;
            return _greenItems;
        }
    }
}

[System.Diagnostics.CodeAnalysis.SuppressMessage("StyleCop.CSharp.MaintainabilityRules", "SA1402:File may only contain a single type", Justification = "Sample code")]
internal sealed partial class EvilDuplicateRequestedShortcut : ListPage
{
    private readonly IListItem[] _items =
    [
        new ListItem(new NoOpCommand())
        {
            Title = "I'm evil!",
            Subtitle = "I have multiple commands sharing the same keyboard shortcut",
            MoreCommands = [
                new CommandContextItem(new AnonymousCommand(() => new ToastStatusMessage("Me too executed").Show())
                {
                    Result = CommandResult.KeepOpen(),
                })
                {
                    Title = "Me too",
                    RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.Number1),
                },
                new CommandContextItem(new AnonymousCommand(() => new ToastStatusMessage("Me three executed").Show())
                {
                    Result = CommandResult.KeepOpen(),
                })
                {
                    Title = "Me three",
                    RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.Number1),
                },
            ],
        },
    ];

    public override IListItem[] GetItems() => _items;

    public EvilDuplicateRequestedShortcut()
    {
        Icon = new IconInfo(string.Empty);
        Name = "Open";
    }
}
