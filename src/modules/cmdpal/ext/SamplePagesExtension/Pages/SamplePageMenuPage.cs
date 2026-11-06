// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.System;

namespace SamplePagesExtension.Pages;

// Declaring IExtendedAttributesProvider publishes MenuCommands to Command Palette.
internal sealed partial class SamplePageMenuPage : ListPage, IExtendedAttributesProvider
{
    internal const string CommandId = "samples.page-menu";

    internal static ListItem Item { get; } = CreateItem();

    private readonly IListItem[] _items;

    public SamplePageMenuPage()
    {
        Id = CommandId;
        Name = "Page menu sample";
        Icon = new IconInfo("\uE713");
        MenuCommands =
        [
            new CommandContextItem(new SampleSettingsPage())
            {
                Title = "Settings",
                RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.S),
            },
        ];

        _items =
        [
            new ListItem(new ShowToastCommand("Primary item action") { Name = "Primary item action" })
            {
                Title = "Item actions and page Settings",
                Subtitle = "Ctrl+K opens this item's menu. Ctrl+Shift+S opens page Settings.",
                MoreCommands =
                [
                    new CommandContextItem(new ShowToastCommand("Secondary item action") { Name = "Secondary item action" })
                    {
                        RequestedShortcut = KeyChordHelpers.FromModifiers(ctrl: true, shift: true, vkey: VirtualKey.I),
                    },
                    new CommandContextItem(new NoOpCommand { Name = "Item submenu" })
                    {
                        MoreCommands = [new CommandContextItem(new ShowToastCommand("Nested item action") { Name = "Nested item action" })],
                    },
                ],
            },
        ];
    }

    public override IListItem[] GetItems() => _items;

    private static ListItem CreateItem()
    {
        var page = new SamplePageMenuPage();
        return new ListItem(page)
        {
            Title = page.Name,
            Subtitle = "Settings is available from this item's menu and the page title menu, including in the dock.",
            MoreCommands = page.MenuCommands,
        };
    }
}
