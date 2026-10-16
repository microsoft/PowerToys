// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Collections.ObjectModel;

using MouseJump.Models.Drawing;

namespace MouseJump.Models.Display;

/// <summary>
/// Represents the entire desktop or virtual screen for a single logical device.
/// </summary>
public sealed record DesktopInfo
{
    public DesktopInfo(IEnumerable<ScreenInfo> screens)
    {
        this.Screens = (screens ?? throw new ArgumentNullException(nameof(screens))).ToList().AsReadOnly();
    }

    public ReadOnlyCollection<ScreenInfo> Screens
    {
        get;
    }

    public RectangleInfo GetCombinedDisplayArea()
    {
        return (this.Screens.Count == 0)
            ? throw new InvalidOperationException($"{nameof(GetCombinedDisplayArea)} requires one or more screens.")
            : RectangleInfo.Union(this.Screens.Select(screen => screen.DisplayArea));
    }
}
