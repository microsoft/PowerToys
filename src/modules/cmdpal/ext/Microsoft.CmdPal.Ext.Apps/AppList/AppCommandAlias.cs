// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.Generic;
using Microsoft.CmdPal.Ext.Apps.Commands;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.Foundation;

namespace Microsoft.CmdPal.Ext.Apps.AppList;

/// <summary>Preserves a saved command ID while sharing the canonical app's current presentation.</summary>
internal sealed partial class AppCommandAlias : IListItem, IExtendedAttributesProvider
{
    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add => _item.PropChanged += value;
        remove => _item.PropChanged -= value;
    }

    private readonly AppListItem _item;

    public ICommand Command { get; }

    public IContextItem[] MoreCommands => _item.MoreCommands;

    public IIconInfo? Icon => _item.Icon;

    public string Title => _item.Title;

    public string Subtitle => _item.Subtitle;

    public ITag[] Tags => _item.Tags;

    public IDetails? Details => _item.Details;

    public string Section => _item.Section;

    public string TextToSuggest => _item.TextToSuggest;

    /// <summary>Initializes a new instance of the <see cref="AppCommandAlias"/> class. Wraps a current app while retaining the command ID used by an existing saved reference.</summary>
    public AppCommandAlias(AppListItem item, string commandId)
    {
        _item = item;
        Command = new AppCommand(item.App) { Id = commandId, Icon = item.Icon as IconInfo ?? Icons.GenericAppIcon };
    }

    /// <summary>Gets extended attributes from the current canonical app row.</summary>
    public IDictionary<string, object> GetProperties()
    {
        return _item.GetProperties();
    }
}
