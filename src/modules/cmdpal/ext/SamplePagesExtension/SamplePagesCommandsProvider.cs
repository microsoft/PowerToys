// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using SamplePagesExtension.Pages.IssueSpecificPages;
using Windows.Foundation;
using Windows.System;

namespace SamplePagesExtension;

public partial class SamplePagesCommandsProvider : CommandProvider
{
    private readonly SampleButtonsDockBand _buttonsBand = new();
    private readonly ICommandItem[] _bands;
    private readonly CommandItem _mediaPlayer = new(new SampleMediaPlayerPage()) { Title = "Sample media player" };

    public SamplePagesCommandsProvider()
    {
        DisplayName = "Sample Pages Commands";
        Icon = new IconInfo("\uE82D");
        _bands = [new SampleDockBand(), _buttonsBand, _mediaPlayer];
    }

    private readonly ICommandItem[] _commands = [
       new CommandItem(new SamplesListPage())
       {
           Title = "Sample Pages",
           Subtitle = "View example commands",
           MoreCommands = [
               new CommandContextItem(new SampleListPage())
               {
                   Title = "Open list sample with shortcut",
                   RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.Number1),
               },
               new CommandContextItem(new NoOpCommand { Name = "Shortcut submenu" })
               {
                   Title = "Shortcut submenu",
                   RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.Number2),
                   MoreCommands = [
                       new CommandContextItem(new SampleListPageWithDetails())
                       {
                           Title = "Open nested list sample",
                           RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, vkey: VirtualKey.Number1),
                       },
                   ],
               },
           ],
       },
    ];

    public override ICommandItem[] TopLevelCommands()
    {
        return [.. _commands, _mediaPlayer];
    }

    public override ICommandItem GetCommandItem(string id) =>
        id == SampleMediaPlayerPage.CommandId ? _mediaPlayer :
        id == SampleCompactPinToDockPage.PinnableItem.Command.Id ? SampleCompactPinToDockPage.PinnableItem : null;

    public override string[] GetAvailableSmallContent() => [SampleMediaPlayerPage.CommandId];

    public override IContent GetSmallContent(string commandId, Size availableSpace) =>
        commandId == SampleMediaPlayerPage.CommandId ? SampleMediaPlayerPage.CreateSmallContent() : null;

    public override ICommandItem[] GetDockBands()
    {
        return _bands;
    }

    public override void Dispose()
    {
        _buttonsBand.Dispose();
        GC.SuppressFinalize(this);
        base.Dispose();
    }
}
