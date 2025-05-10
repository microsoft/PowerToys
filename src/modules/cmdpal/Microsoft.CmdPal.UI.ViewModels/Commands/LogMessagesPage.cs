// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Specialized;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;

namespace Microsoft.CmdPal.UI.ViewModels.Commands;

public partial class LogMessagesPage : ListPage
{
    private readonly List<IListItem> _listItems = new();

    public LogMessagesPage()
    {
        Name = Properties.Resources.builtin_log_name;
        Title = Properties.Resources.builtin_log_page_name;
        Icon = new IconInfo("\uE8FD"); // BulletedList icon
        CommandPaletteHost.LogMessages.CollectionChanged += LogMessages_CollectionChanged;
    }

    private void LogMessages_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action == NotifyCollectionChangedAction.Add && e.NewItems != null)
        {
            foreach (var item in e.NewItems)
            {
                if (item is LogMessageViewModel logMessageViewModel)
                {
                    var li = new ListItem(new NoOpCommand())
                    {
                        Title = logMessageViewModel.Message,
                        Subtitle = logMessageViewModel.ExtensionPfn,
                    };
                    _listItems.Insert(0, li);
                }
            }

            RaiseItemsChanged(_listItems.Count);
        }
    }

    public override IListItem[] GetItems()
    {
        return _listItems.ToArray();
    }
}
