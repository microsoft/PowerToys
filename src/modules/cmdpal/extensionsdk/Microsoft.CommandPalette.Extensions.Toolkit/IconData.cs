// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Storage.Streams;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class IconData : IIconData
{
    public IRandomAccessStreamReference? Data { get; set; }

    public string? Icon { get; set; } = string.Empty;

    public IconData(string? icon)
    {
        Icon = icon;
    }

    public IconData(IRandomAccessStreamReference data)
    {
        Data = data;
    }

    internal IconData()
        : this(string.Empty)
    {
    }
}
