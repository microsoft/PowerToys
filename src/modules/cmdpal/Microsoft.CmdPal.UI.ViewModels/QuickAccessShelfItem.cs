// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Commands;
using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;
using Microsoft.CommandPalette.Extensions.Toolkit;
using Windows.ApplicationModel.DataTransfer;

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class QuickAccessShelfItem : IEquatable<QuickAccessShelfItem>
{
    private readonly IListItem _item;
    private readonly object? _sourceIcon;
    private readonly int _shortcutIndex;

    public string Title { get; }

    public IconInfoViewModel Icon { get; }

    public string ProviderId { get; }

    public string CommandId { get; }

    public bool StartsNewSection { get; }

    public bool IsPinned { get; }

    public bool CanPin { get; }

    public DataPackageView? DataPackage { get; }

    public string ShortcutDigit => QuickAccessShelfResolver.IndexToShortcutDigit(_shortcutIndex);

    private QuickAccessShelfItem(
        IListItem item,
        string title,
        object? sourceIcon,
        IconInfoViewModel icon,
        int shortcutIndex,
        bool startsNewSection,
        bool isPinned,
        bool canPin,
        DataPackageView? dataPackage)
    {
        _item = item;
        Title = title;
        _sourceIcon = sourceIcon;
        Icon = icon;
        _shortcutIndex = shortcutIndex;
        StartsNewSection = startsNewSection;
        IsPinned = isPinned;
        CanPin = canPin;
        DataPackage = dataPackage;
        ProviderId = TopLevelCommandResolver.GetProviderId(item);
        CommandId = TopLevelCommandResolver.GetCommandId(item);
    }

    // Accessing extension properties and initializing IconInfoViewModel must stay off the UI thread.
    internal static QuickAccessShelfItem CreateOrReuse(
        IReadOnlyList<QuickAccessShelfItem> existingItems,
        IListItem item,
        int shortcutIndex,
        bool startsNewSection,
        bool isPinned = false,
        bool canPin = false)
    {
        var title = item.Title;
        object? sourceIcon;
        IconInfoViewModel? icon = null;
        if (item is TopLevelViewModel topLevel)
        {
            sourceIcon = topLevel.IconViewModel;
            icon = topLevel.IconViewModel;
        }
        else
        {
            sourceIcon = item.Icon;
        }

        var dataPackage =
            item is IExtendedAttributesProvider attributes &&
            attributes.GetProperties()?.TryGetValue(WellKnownExtensionAttributes.DataPackage, out var dataPackageValue) == true &&
            dataPackageValue is DataPackageView dataPackageView
                ? dataPackageView
                : null;

        foreach (var existingItem in existingItems)
        {
            if (existingItem.Matches(item, title, sourceIcon, shortcutIndex, startsNewSection, isPinned, canPin, dataPackage))
            {
                return existingItem;
            }
        }

        if (icon is null)
        {
            icon = new IconInfoViewModel((IIconInfo?)sourceIcon);
            icon.InitializeProperties();
        }

        return new QuickAccessShelfItem(item, title, sourceIcon, icon, shortcutIndex, startsNewSection, isPinned, canPin, dataPackage);
    }

    public IListItem GetContextMenuItem() =>
        IsPinned || _item is RecentCommandListItem ? _item : new RecentCommandListItem(_item, CommandId);

    public PerformCommandMessage GetPerformCommandMessage()
    {
        if (_item is TopLevelViewModel topLevel)
        {
            return topLevel.GetPerformCommandMessage();
        }

        var command = _item.Command ?? throw new InvalidOperationException("Quick-access items must have a command");
        return new PerformCommandMessage(
            new ExtensionObject<ICommand>(command),
            new ExtensionObject<IListItem>(_item));
    }

    public bool Equals(QuickAccessShelfItem? other) =>
        other is not null &&
        ReferenceEquals(_item, other._item) &&
        ReferenceEquals(_sourceIcon, other._sourceIcon) &&
        string.Equals(Title, other.Title, StringComparison.Ordinal) &&
        _shortcutIndex == other._shortcutIndex &&
        StartsNewSection == other.StartsNewSection &&
        IsPinned == other.IsPinned &&
        CanPin == other.CanPin &&
        ReferenceEquals(DataPackage, other.DataPackage);

    public override bool Equals(object? obj) => Equals(obj as QuickAccessShelfItem);

    public override int GetHashCode() => HashCode.Combine(_item, _sourceIcon, Title, _shortcutIndex, StartsNewSection, IsPinned, CanPin, DataPackage);

    private bool Matches(
        IListItem item,
        string title,
        object? sourceIcon,
        int shortcutIndex,
        bool startsNewSection,
        bool isPinned,
        bool canPin,
        DataPackageView? dataPackage) =>
        ReferenceEquals(_item, item) &&
        ReferenceEquals(_sourceIcon, sourceIcon) &&
        string.Equals(Title, title, StringComparison.Ordinal) &&
        _shortcutIndex == shortcutIndex &&
        StartsNewSection == startsNewSection &&
        IsPinned == isPinned &&
        CanPin == canPin &&
        ReferenceEquals(DataPackage, dataPackage);
}
