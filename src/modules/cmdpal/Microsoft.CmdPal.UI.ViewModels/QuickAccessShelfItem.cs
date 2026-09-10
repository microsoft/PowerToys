// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.UI.ViewModels;

public sealed class QuickAccessShelfItem(TopLevelViewModel command, int index) : IEquatable<QuickAccessShelfItem>
{
    public TopLevelViewModel Command { get; } = command;

    public int Index { get; } = index;

    public string ShortcutDigit => QuickAccessShelfResolver.IndexToShortcutDigit(Index);

    public bool Equals(QuickAccessShelfItem? other) =>
        other is not null &&
        ReferenceEquals(Command, other.Command) &&
        Index == other.Index;

    public override bool Equals(object? obj) => Equals(obj as QuickAccessShelfItem);

    public override int GetHashCode() => HashCode.Combine(Command, Index);
}
