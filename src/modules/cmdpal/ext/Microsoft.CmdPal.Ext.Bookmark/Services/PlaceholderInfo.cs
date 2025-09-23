// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Microsoft.CmdPal.Ext.Bookmarks.Services;

public class PlaceholderInfo
{
    public string Name { get; }

    public PlaceholderInfo(string name)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
    }

    public override bool Equals(object? obj)
    {
        return obj is PlaceholderInfo other && Name == other.Name;
    }

    public override int GetHashCode()
    {
        return Name.GetHashCode();
    }

    public override string ToString()
    {
        return Name;
    }
}
