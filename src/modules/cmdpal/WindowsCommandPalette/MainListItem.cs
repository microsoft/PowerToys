// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.InteropServices;
using Microsoft.CmdPal.Extensions;
using Microsoft.CmdPal.Extensions.Helpers;

namespace WindowsCommandPalette;

public sealed partial class MainListItem : ListItem
{
    public IListItem Item { get; set; }

    internal MainListItem(IListItem listItem)
        : base(listItem.Command)
    {
        Item = listItem;

        Title = Item.Title ?? Item.Command.Name;
        Subtitle = Item.Subtitle;
        MoreCommands = Item.MoreCommands;
        FallbackHandler = Item.FallbackHandler;
        Tags = Item.Tags;

        if (Command != null)
        {
            Command.PropChanged += Action_PropertyChanged;
        }

        Item.PropChanged += Action_PropertyChanged;
    }

    private void Action_PropertyChanged(object sender, PropChangedEventArgs args)
    {
        // why does this class even exist we shouldn't need it... right?
        try
        {
            if (args.PropertyName == "Name")
            {
                Title = !string.IsNullOrEmpty(Item.Title) ? Item.Title : Command?.Name ?? string.Empty;
                OnPropertyChanged(nameof(Title));
            }
            else if (args.PropertyName == nameof(Title))
            {
                Title = Item.Title;
            }
            else if (args.PropertyName == nameof(Subtitle))
            {
                Subtitle = Item.Subtitle;
            }
            else if (args.PropertyName == nameof(MoreCommands))
            {
                MoreCommands = Item.MoreCommands;
            }

            OnPropertyChanged(args.PropertyName);
        }
        catch (COMException)
        {
            /* log something */
        }
    }
}
