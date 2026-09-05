// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Commands;

/// <summary>
/// Marks an item as the recent-section presentation of another list item while preserving the
/// extension-owned item and its live properties. The host uses this marker to add recent-only
/// context commands without changing the item everywhere else it is displayed.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public sealed partial class RecentCommandListItem : IListItem, IExtendedAttributesProvider, ICommandContextSource
{
    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add => Source.PropChanged += value;
        remove => Source.PropChanged -= value;
    }

    public IListItem Source { get; }

    public string CommandId { get; }

    AppExtensionHost? ICommandContextSource.ExtensionHost => (Source as ICommandContextSource)?.ExtensionHost;

    ICommandProviderContext? ICommandContextSource.ProviderContext => (Source as ICommandContextSource)?.ProviderContext;

    public ICommand? Command => Source.Command;

    public IContextItem?[] MoreCommands => Source.MoreCommands;

    public IIconInfo? Icon => Source.Icon;

    public string Title => Source.Title;

    public string Subtitle => Source.Subtitle;

    public ITag[] Tags => Source.Tags;

    public IDetails? Details => Source.Details;

    public string Section => Source.Section;

    public string TextToSuggest => Source.TextToSuggest;

    public RecentCommandListItem(IListItem source, string commandId)
    {
        Source = source;
        CommandId = commandId;
    }

    internal static RecentCommandListItem CreateOrReuse(
        IReadOnlyList<IListItem>? existingItems,
        IListItem source,
        string commandId)
    {
        if (existingItems is not null)
        {
            foreach (var existingItem in existingItems)
            {
                if (existingItem is RecentCommandListItem recentItem &&
                    ReferenceEquals(recentItem.Source, source) &&
                    string.Equals(recentItem.CommandId, commandId, StringComparison.Ordinal))
                {
                    return recentItem;
                }
            }
        }

        return new RecentCommandListItem(source, commandId);
    }

    public IDictionary<string, object?> GetProperties()
    {
        return Source is IExtendedAttributesProvider attributes
            ? attributes.GetProperties()
            : new Dictionary<string, object?>();
    }
}
