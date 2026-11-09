// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using SamplePagesExtension.Pages.IssueSpecificPages;
using Windows.System;

namespace SamplePagesExtension;

public partial class SamplePagesCommandsProvider : CommandProvider
{
    private readonly SampleButtonsDockBand _buttonsBand = new();
    private readonly ICommandItem[] _bands;

    public SamplePagesCommandsProvider()
    {
        DisplayName = "Sample Pages Commands";
        Icon = new IconInfo("\uE82D");
        _bands = [new SampleDockBand(), _buttonsBand];
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
        return _commands;
    }

    public override ICommandItem GetCommandItem(string id) =>
        id == SampleCompactPinToDockPage.PinnableItem.Command.Id ? SampleCompactPinToDockPage.PinnableItem : null;

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
