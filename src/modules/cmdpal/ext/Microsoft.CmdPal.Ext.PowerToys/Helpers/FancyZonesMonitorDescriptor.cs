// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

namespace PowerToysExtension.Helpers;

internal readonly record struct FancyZonesMonitorDescriptor(
    int Index,
    FancyZonesEditorMonitor Data)
{
    public string Title => $"Monitor {Index}";

    public string Subtitle
    {
        get
        {
            var size = $"{Data.MonitorWidth}×{Data.MonitorHeight}";
            var scaling = Data.Dpi > 0 ? string.Format(CultureInfo.InvariantCulture, "{0}%", (int)Math.Round(Data.Dpi * 100 / 96.0)) : "n/a";
            return $"{size} • {scaling} • {Data.Monitor}";
        }
    }
}
