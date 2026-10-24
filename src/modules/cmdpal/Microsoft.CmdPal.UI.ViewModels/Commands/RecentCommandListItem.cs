// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Diagnostics.CodeAnalysis;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Windows.Foundation;

namespace Microsoft.CmdPal.UI.ViewModels.Commands;

/// <summary>
/// Marks an item as the recent-section presentation of another list item while preserving the
/// in-proc item and its live properties. The host uses this marker to add recent-only
/// context commands without changing the item everywhere else it is displayed.
/// </summary>
[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)]
public sealed partial class RecentCommandListItem : IListItem, IExtendedAttributesProvider, ICommandContextSource
{
    public event TypedEventHandler<object, IPropChangedEventArgs>? PropChanged
    {
        add => Source.Safe.PropChanged += value;
        remove => Source.Safe.PropChanged -= value;
    }

    public InProcObject<IListItem> Source { get; }

    public string CommandId { get; }

    AppExtensionHost? ICommandContextSource.ExtensionHost => (Source.Safe as ICommandContextSource)?.ExtensionHost;

    ICommandProviderContext? ICommandContextSource.ProviderContext => (Source.Safe as ICommandContextSource)?.ProviderContext;

    public ICommand? Command => Source.Safe.Command;

    public IContextItem?[] MoreCommands => Source.Safe.MoreCommands;

    public IIconInfo? Icon => Source.Safe.Icon;

    public string Title => Source.Safe.Title;

    public string Subtitle => Source.Safe.Subtitle;

    public ITag[] Tags => Source.Safe.Tags;

    public IDetails? Details => Source.Safe.Details;

    public string Section => Source.Safe.Section;

    public string TextToSuggest => Source.Safe.TextToSuggest;

    public RecentCommandListItem(InProcObject<IListItem> source, string commandId)
    {
        Source = source;
        CommandId = commandId;
    }

    internal static RecentCommandListItem CreateOrReuse(
        IReadOnlyList<InProcObject<IListItem>>? existingItems,
        InProcObject<IListItem> source,
        string commandId)
    {
        if (existingItems is not null)
        {
            foreach (var existingItem in existingItems)
            {
                if (existingItem.Safe is RecentCommandListItem recentItem &&
                    ReferenceEquals(recentItem.Source.Safe, source.Safe) &&
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
        return Source.Safe is IExtendedAttributesProvider attributes
            ? attributes.GetProperties()
            : new Dictionary<string, object?>();
    }
}
