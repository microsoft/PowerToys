// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace SamplePagesExtension;

public partial class SamplePagesCommandsProvider : CommandProvider
{
    public SamplePagesCommandsProvider()
    {
        DisplayName = "Sample Pages Commands";
        Icon = new IconInfo("\uE82D");
    }

    private readonly ICommandItem[] _commands = [
       new CommandItem(new SamplesListPage())
       {
           Title = "Sample Pages",
           Subtitle = "View example commands",
       },
        new FallbackCommand(new FallbackPageSample(), title: "just be sure")
       {
           Title = "be sure for sure",
           Subtitle = "You can use this to respond to the user's search",
       },
    ];

    public override ICommandItem[] TopLevelCommands()
    {
        return _commands;
    }

    private sealed partial class FallbackCommand : FallbackCommandItem
    {
        public FallbackCommand(ListPage page, string title = null)
            : base(page, string.IsNullOrEmpty(title) ? title : page.Title)
        {
        }

        public override void UpdateQuery(string query)
        {
            if (Command is ListPage page)
            {
                page.SearchText = query;
            }
        }
    }

    private sealed partial class FallbackPageSample : ListPage
    {
        private readonly List<ListItem> _items = [];

        public FallbackPageSample()
        {
            Title = "Fallback sample";
            EmptyContent = new CommandItem() { Title = "You invoked the fallback page directly" };
        }

        public override IListItem[] GetItems()
        {
            return _items.ToArray();
        }

        public override string SearchText
        {
            get => base.SearchText;
            set
            {
                base.SearchText = value;
                _items.Insert(0, new ListItem() { Title = value, Subtitle = "This is the text you had typed on the main page" });
            }
        }
    }
}
