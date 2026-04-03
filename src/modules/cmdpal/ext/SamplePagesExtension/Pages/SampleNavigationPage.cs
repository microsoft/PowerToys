// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

#pragma warning disable SA1402 // File may only contain a single type
#pragma warning disable SA1649 // File name should match first type name
namespace SamplePagesExtension;

internal static class SampleNavigationCommandCatalog
{
    public static ListItem CreateRootListItem() =>
        new(new SampleNavigationPage())
        {
            Title = "Navigation results and GoToPage",
            Subtitle = "Shows GoBack, GoHome, and GoToPage working together",
        };

    public static ListItem CreatePlaygroundListItem() =>
        new(new SampleNavigationPlaygroundPage())
        {
            Title = "Open a nested navigation playground",
            Subtitle = "Adds one more page to the stack so GoBack-oriented samples are easier to see",
        };

    public static ICommandItem GetCommandItem(string id) => id switch
    {
        SampleNavigationPage.CommandId => CreateRootListItem(),
        SampleNavigationPlaygroundPage.CommandId => CreateCommandItem(
            new SampleNavigationPlaygroundPage(),
            "Nested navigation playground",
            "Adds another page to the stack before trying GoToPage"),
        SampleNavigationTargetListPage.CommandId => CreateCommandItem(
            new SampleNavigationTargetListPage(),
            "GoToPage target list page",
            "A list page resolved by ICommandProvider4.GetCommandItem"),
        SampleNavigationTargetContentPage.CommandId => CreateCommandItem(
            new SampleNavigationTargetContentPage(),
            "GoToPage target content page",
            "A content page resolved by ICommandProvider4.GetCommandItem"),
        _ => null,
    };

    private static CommandItem CreateCommandItem(ICommand command, string title, string subtitle) =>
        new(command)
        {
            Title = title,
            Subtitle = subtitle,
        };
}

internal sealed partial class SampleNavigationPage : ListPage
{
    public const string CommandId = "sample.navigation.root";

    public SampleNavigationPage()
    {
        Id = CommandId;
        Name = "Navigation results";
        Title = "Navigation results and GoToPage";
        Icon = new IconInfo("\uE8AB");
    }

    public override IListItem[] GetItems() =>
    [
        ResultItem(
            "Return CommandResult.GoBack()",
            "Pop back to the previous page in the shell stack",
            CommandResult.GoBack()),
        ResultItem(
            "Return CommandResult.GoHome()",
            "Jump all the way back to the palette home page",
            CommandResult.GoHome()),
        SampleNavigationCommandCatalog.CreatePlaygroundListItem(),
        ResultItem(
            "Return GoToPage(Push) to a list page",
            "Resolve the target by id and push it on top of the current page",
            CommandResult.GoToPage(new GoToPageArgs
            {
                PageId = SampleNavigationTargetListPage.CommandId,
                NavigationMode = NavigationMode.Push,
            })),
        ResultItem(
            "Return GoToPage(GoHome) to a content page",
            "Go home first, then navigate to the resolved content page",
            CommandResult.GoToPage(new GoToPageArgs
            {
                PageId = SampleNavigationTargetContentPage.CommandId,
                NavigationMode = NavigationMode.GoHome,
            })),
    ];

    private static ListItem ResultItem(string title, string subtitle, ICommandResult result) =>
        new(new AnonymousCommand(() => { })
        {
            Name = title,
            Result = result,
        })
        {
            Title = title,
            Subtitle = subtitle,
        };
}

internal sealed partial class SampleNavigationPlaygroundPage : ListPage
{
    public const string CommandId = "sample.navigation.playground";

    public SampleNavigationPlaygroundPage()
    {
        Id = CommandId;
        Name = "Nested navigation playground";
        Title = "Nested navigation playground";
        Icon = new IconInfo("\uE8AB");
    }

    public override IListItem[] GetItems() =>
    [
        new ListItem(new NoOpCommand())
        {
            Title = "This page gives GoBack-oriented samples something to unwind",
            Subtitle = "Try the GoToPage(GoBack) item below to pop back once and then open a new target page",
        },
        ResultItem(
            "Return GoToPage(GoBack) to a list page",
            "Go back one level, then navigate to the resolved list page",
            CommandResult.GoToPage(new GoToPageArgs
            {
                PageId = SampleNavigationTargetListPage.CommandId,
                NavigationMode = NavigationMode.GoBack,
            })),
        ResultItem(
            "Return GoToPage(Push) to a content page",
            "Stay on this stack and push a resolved content page on top",
            CommandResult.GoToPage(new GoToPageArgs
            {
                PageId = SampleNavigationTargetContentPage.CommandId,
                NavigationMode = NavigationMode.Push,
            })),
        ResultItem(
            "Return CommandResult.GoBack()",
            "Pop back to the previous sample page without resolving anything",
            CommandResult.GoBack()),
        ResultItem(
            "Return CommandResult.GoHome()",
            "Leave the sample flow and jump to the palette home page",
            CommandResult.GoHome()),
    ];

    private static ListItem ResultItem(string title, string subtitle, ICommandResult result) =>
        new(new AnonymousCommand(() => { })
        {
            Name = title,
            Result = result,
        })
        {
            Title = title,
            Subtitle = subtitle,
        };
}

internal sealed partial class SampleNavigationTargetListPage : ListPage
{
    public const string CommandId = "sample.navigation.target.list";

    public SampleNavigationTargetListPage()
    {
        Id = CommandId;
        Name = "GoToPage target list page";
        Title = "GoToPage target list page";
        Icon = new IconInfo("\uE8FD");
    }

    public override IListItem[] GetItems() =>
    [
        new ListItem(new NoOpCommand())
        {
            Title = "You landed on a list page resolved through GetCommandItem",
            Subtitle = "This page was not opened directly from the visible samples list",
        },
        new ListItem(new AnonymousCommand(() => { })
        {
            Name = "Push content target",
            Result = CommandResult.GoToPage(new GoToPageArgs
            {
                PageId = SampleNavigationTargetContentPage.CommandId,
                NavigationMode = NavigationMode.Push,
            }),
        })
        {
            Title = "Push the content target with GoToPage",
            Subtitle = "Resolve another sample page by id and push it on top of this one",
        },
        new ListItem(new AnonymousCommand(() => { })
        {
            Name = "GoBack",
            Result = CommandResult.GoBack(),
        })
        {
            Title = "Return CommandResult.GoBack()",
            Subtitle = "Go back to whatever page navigated here",
        },
    ];
}

internal sealed partial class SampleNavigationTargetContentPage : ContentPage
{
    public const string CommandId = "sample.navigation.target.content";

    private readonly MarkdownContent _markdown = new()
    {
        Body = """
# GoToPage target content page

This page was resolved by id through `ICommandProvider4.GetCommandItem`.

- `Push` keeps the existing stack and places this page on top.
- `GoBack` unwinds once before opening the resolved page.
- `GoHome` clears back to the palette home page first, then opens this page.
""",
    };

    public SampleNavigationTargetContentPage()
    {
        Id = CommandId;
        Name = "GoToPage target content page";
        Title = "GoToPage target content page";
        Icon = new IconInfo("\uE8A5");

        Commands =
        [
            new CommandContextItem(
                title: "Go back",
                name: "Go back",
                subtitle: "Return CommandResult.GoBack()",
                result: CommandResult.GoBack()),
            new CommandContextItem(
                title: "Go home",
                name: "Go home",
                subtitle: "Return CommandResult.GoHome()",
                result: CommandResult.GoHome()),
            new CommandContextItem(
                title: "Push the list target",
                name: "Push the list target",
                subtitle: "Return GoToPage(Push) to the list target page",
                result: CommandResult.GoToPage(new GoToPageArgs
                {
                    PageId = SampleNavigationTargetListPage.CommandId,
                    NavigationMode = NavigationMode.Push,
                })),
        ];
    }

    public override IContent[] GetContent() => [_markdown];
}
#pragma warning restore SA1402 // File may only contain a single type
#pragma warning restore SA1649 // File name should match first type name
