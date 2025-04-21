﻿// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;
using Windows.Win32;

namespace SamplePagesExtension;

internal sealed partial class SampleListPage : ListPage
{
    public SampleListPage()
    {
        Icon = new IconInfo("\uEA37");
        Name = "Sample List Page";
    }

    public override IListItem[] GetItems()
    {
        var confirmOnceArgs = new ConfirmationArgs
        {
            PrimaryCommand = new AnonymousCommand(
                () =>
                {
                    var t = new ToastStatusMessage("The dialog was confirmed");
                    t.Show();
                })
            {
                Name = "Confirm",
                Result = CommandResult.KeepOpen(),
            },
            Title = "You can set a title for the dialog",
            Description = "Are you really sure you want to do the thing?",
        };
        var confirmTwiceArgs = new ConfirmationArgs
        {
            PrimaryCommand = new AnonymousCommand(() => { })
            {
                Name = "How sure are you?",
                Result = CommandResult.Confirm(confirmOnceArgs),
            },
            Title = "You can ask twice too",
            Description = "You probably don't want to though, that'd be annoying.",
        };

        return [
            new ListItem(new NoOpCommand())
            {
                Title = "This is a basic item in the list",
                Subtitle = "I don't do anything though",
            },
            new ListItem(new SampleListPageWithDetails())
            {
                Title = "This item will take you to another page",
                Subtitle = "This allows for nested lists of items",
            },
            new ListItem(new OpenUrlCommand("https://github.com/microsoft/powertoys"))
            {
                Title = "Or you can go to links",
                Subtitle = "This takes you to the PowerToys repo on GitHub",
            },
            new ListItem(new SampleMarkdownPage())
            {
                Title = "Items can have tags",
                Subtitle = "and I'll take you to a page with markdown content",
                Tags = [new Tag("Sample Tag")],
            },

            new ListItem(
                new ToastCommand("Primary command invoked", MessageState.Info) { Name = "Primary command", Icon = new IconInfo("\uF146") }) // dial 1
            {
                Title = "You can add context menu items too. Press Ctrl+k",
                Subtitle = "Try pressing Ctrl+1 with me selected",
                Icon = new IconInfo("\uE712"),  // "More" dots
                MoreCommands = [
                    new CommandContextItem(
                        new ToastCommand("Secondary command invoked", MessageState.Warning) { Name = "Secondary command", Icon = new IconInfo("\uF147") }) // dial 2
                    {
                        Title = "I'm a second command",
                        RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.Number1),
                    },
                    new CommandContextItem(
                        new ToastCommand("Third command invoked", MessageState.Error) { Name = "Do 3", Icon = new IconInfo("\uF148") }) // dial 3
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

            new ListItem(new SendMessageCommand())
            {
                Title = "I send lots of messages",
                Subtitle = "Status messages can be used to provide feedback to the user in-app",
            },
            new SendSingleMessageItem(),
            new ListItem(new IndeterminateProgressMessageCommand())
            {
                Title = "Do a thing with a spinner",
                Subtitle = "Messages can have progress spinners, to indicate something is happening in the background",
            },
            new ListItem(
                new AnonymousCommand(() => { })
                {
                    Result = CommandResult.Confirm(confirmOnceArgs),
                })
            {
                Title = "Confirm before doing something",
            },
            new ListItem(
                new AnonymousCommand(() => { })
                {
                    Result = CommandResult.Confirm(confirmTwiceArgs),
                })
            {
                Title = "Confirm twice before doing something",
            },
            new ListItem(
                new AnonymousCommand(() =>
                {
                    var fg = PInvoke.GetForegroundWindow();
                    var bufferSize = PInvoke.GetWindowTextLength(fg) + 1;
                    unsafe
                   {
                       fixed (char* windowNameChars = new char[bufferSize])
                       {
                           if (PInvoke.GetWindowText(fg, windowNameChars, bufferSize) == 0)
                           {
                                var emptyToast = new ToastStatusMessage(new StatusMessage() { Message = "FG Window didn't have a title",  State = MessageState.Warning });
                                emptyToast.Show();
                           }

                           var windowName = new string(windowNameChars);
                           var nameToast = new ToastStatusMessage(new StatusMessage() { Message = $"FG Window is {windowName}", State = MessageState.Success });
                           nameToast.Show();
                       }
                   }
                })
                {
                    Result = CommandResult.KeepOpen(),
                })
            {
                Title = "Get the name of the Foreground window",
            },
        ];
    }
}
