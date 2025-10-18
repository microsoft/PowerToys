// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;
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
                    new Separator(),
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
                                Title = "Nested B with a really, really long title that should be trimmed",
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

            new ListItem(new CommandWithProperties())
            {
                Title = "I have properties",
            },
            new ListItem(new OtherCommandWithProperties())
            {
                Title = "I also have properties",
            },
            new ListItem(new EverChangingCommand("Cat", "🐈‍⬛", "🐈"))
            {
                Title = "And I have a commands with changing name and icon",
                MoreCommands = [
                    new CommandContextItem(new EverChangingCommand("Water", "🐬", "🐳", "🐟", "🦈")),
                    new CommandContextItem(new EverChangingCommand("Faces", "😁", "🥺", "😍")),
                    new CommandContextItem(new EverChangingCommand("Hearts", "♥️", "💚", "💜", "🧡", "💛", "💙")),
                ],
            }
        ];
    }

    internal sealed partial class CommandWithProperties : InvokableCommand, IExtendedAttributesProvider
    {
        private FontIconData _icon = new("\u0026", "Wingdings");

        public override IconInfo Icon => new(_icon, _icon);

        public override string Name => "Whatever";

        // LOAD-BEARING: Use a Windows.Foundation.Collections.ValueSet as the
        // backing store for Properties. A regular `Dictionary<string, object>`
        // will not work across the ABI
        public IDictionary<string, object> GetProperties() => new Windows.Foundation.Collections.ValueSet()
        {
            { "Foo", "bar" },
            { "Secret", 42 },
            { "hmm?", null },
        };
    }

    internal sealed partial class OtherCommandWithProperties : IExtendedAttributesProvider, IInvokableCommand
    {
        public string Name => "Whatever 2";

        public IIconInfo Icon => new IconInfo("\uF146");

        public string Id => string.Empty;

        public event TypedEventHandler<object, IPropChangedEventArgs> PropChanged;

        public ICommandResult Invoke(object sender)
        {
            PropChanged?.Invoke(this, new PropChangedEventArgs(nameof(Name)));
            return CommandResult.ShowToast("whoop");
        }

        // LOAD-BEARING: Use a Windows.Foundation.Collections.ValueSet as the
        // backing store for Properties. A regular `Dictionary<string, object>`
        // will not work across the ABI
        public IDictionary<string, object> GetProperties() => new Windows.Foundation.Collections.ValueSet()
        {
            { "yo", "dog" },
            { "Secret", 12345 },
            { "hmm?", null },
        };
    }

    internal sealed partial class EverChangingCommand : InvokableCommand, IDisposable
    {
        private readonly string[] _icons;
        private readonly Timer _timer;
        private readonly string _name;
        private int _currentIndex;

        public EverChangingCommand(string name, params string[] icons)
        {
            _icons = icons ?? throw new ArgumentNullException(nameof(icons));
            if (_icons.Length == 0)
            {
                throw new ArgumentException("Icons array cannot be empty", nameof(icons));
            }

            _name = name;
            Name = $"{_name} {DateTimeOffset.UtcNow:hh:mm:ss}";
            Icon = new IconInfo(_icons[_currentIndex]);

            // Start timer to change icon and name every 5 seconds
            _timer = new Timer(OnTimerElapsed, null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
        }

        private void OnTimerElapsed(object state)
        {
            var nextIndex = (_currentIndex + 1) % _icons.Length;
            if (nextIndex == _currentIndex && _icons.Length > 1)
            {
                nextIndex = (_currentIndex + 1) % _icons.Length;
            }

            _currentIndex = nextIndex;

            Name = $"{_name} {DateTimeOffset.UtcNow:hh:mm:ss}";
            Icon = new IconInfo(_icons[_currentIndex]);
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
