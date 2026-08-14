// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.CmdPal.UI.ViewModels.Messages;
using Microsoft.CmdPal.UI.ViewModels.Models;
using Microsoft.CommandPalette.Extensions;

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

    public bool StartsRecentSection { get; }

    public string ShortcutDigit => QuickAccessShelfResolver.IndexToShortcutDigit(_shortcutIndex);

    public QuickAccessShelfItem(IListItem item, int shortcutIndex, bool startsRecentSection)
    {
        _item = item;
        Title = item.Title;
        _shortcutIndex = shortcutIndex;
        StartsRecentSection = startsRecentSection;
        ProviderId = TopLevelCommandResolver.GetProviderId(item);
        CommandId = TopLevelCommandResolver.GetCommandId(item);

        if (item is TopLevelViewModel topLevel)
        {
            Icon = topLevel.IconViewModel;
            _sourceIcon = Icon;
        }
        else
        {
            var sourceIcon = item.Icon;
            _sourceIcon = sourceIcon;
            Icon = new IconInfoViewModel(sourceIcon);
            Icon.InitializeProperties();
        }
    }

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
        StartsRecentSection == other.StartsRecentSection;

    public override bool Equals(object? obj) => Equals(obj as QuickAccessShelfItem);

    public override int GetHashCode() => HashCode.Combine(_item, _sourceIcon, Title, _shortcutIndex, StartsRecentSection);
}
