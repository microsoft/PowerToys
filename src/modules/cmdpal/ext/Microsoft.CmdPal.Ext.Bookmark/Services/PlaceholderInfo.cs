// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Bookmarks.Services;

public sealed class PlaceholderInfo
{
    public string Name { get; }

    public int Index { get; }

    public PlaceholderInfo(string name, int index)
    {
        ArgumentNullException.ThrowIfNull(name);
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 0);

        Name = name;
        Index = index;
    }

    private bool Equals(PlaceholderInfo other) => Name == other.Name && Index == other.Index;

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }

        if (ReferenceEquals(this, obj))
        {
            return true;
        }

        if (obj.GetType() != GetType())
        {
            return false;
        }

        return Equals((PlaceholderInfo)obj);
    }

    public override int GetHashCode() => HashCode.Combine(Name, Index);

    public static bool operator ==(PlaceholderInfo? left, PlaceholderInfo? right)
    {
        return Equals(left, right);
    }

    public static bool operator !=(PlaceholderInfo? left, PlaceholderInfo? right)
    {
        return !Equals(left, right);
    }

    public override string ToString() => Name;
}
