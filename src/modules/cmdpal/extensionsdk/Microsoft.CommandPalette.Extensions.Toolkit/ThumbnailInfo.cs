// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Windows.Foundation.Collections;

namespace Microsoft.CommandPalette.Extensions.Toolkit;

public partial class ThumbnailInfo : IconInfo, IIconInfo, IExtendedAttributesProvider
{
    private readonly ValueSet _properties;

    public IDictionary<string, object> GetProperties() => _properties;

    public ThumbnailDisplayMode Mode { get; }

    public ThumbnailInfo(string? icon, ThumbnailDisplayMode mode = ThumbnailDisplayMode.Thumbnail)
        : base(icon)
    {
        Mode = mode;
        _properties = BuildProperties();
    }

    public ThumbnailInfo(IconData light, IconData dark, ThumbnailDisplayMode mode = ThumbnailDisplayMode.Thumbnail)
        : base(light, dark)
    {
        Mode = mode;
        _properties = BuildProperties();
    }

    public ThumbnailInfo(IconData icon, ThumbnailDisplayMode mode = ThumbnailDisplayMode.Thumbnail)
        : base(icon)
    {
        Mode = mode;
        _properties = BuildProperties();
    }

    private ValueSet BuildProperties()
    {
        return new ValueSet
        {
            { "mode", (int)Mode },
        };
    }
}
