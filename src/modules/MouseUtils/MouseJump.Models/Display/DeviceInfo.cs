// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

using MouseJump.Models.Drawing;

namespace MouseJump.Models.Display;

/// <summary>
/// Represents a device whose screens are rendered in the preview image.
/// </summary>
public sealed record DeviceInfo
{
    public DeviceInfo(string hostname, bool localhost, IEnumerable<ScreenInfo> screens)
    {
        this.Hostname = hostname ?? throw new ArgumentNullException(nameof(hostname));
        this.Localhost = localhost;
        this.Screens = (screens ?? throw new ArgumentNullException(nameof(screens))).ToList().AsReadOnly();
    }

    public string Hostname
    {
        get;
    }

    public bool Localhost
    {
        get;
    }

    public ReadOnlyCollection<ScreenInfo> Screens
    {
        get;
    }

    public RectangleInfo GetCombinedDisplayArea()
    {
        return RectangleInfo.Union(this.Screens.Select(screen => screen.DisplayArea));
    }
}
