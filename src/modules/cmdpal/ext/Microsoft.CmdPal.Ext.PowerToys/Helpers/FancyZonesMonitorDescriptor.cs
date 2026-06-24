// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Globalization;

using FancyZonesEditorCommon.Data;

namespace PowerToysExtension.Helpers;

internal readonly record struct FancyZonesMonitorDescriptor(
    int Index,
    EditorParameters.NativeMonitorDataWrapper Data)
{
    public string Title => Data.Monitor;

    public string Subtitle
    {
        get
        {
            // MonitorWidth/Height are logical (DPI-scaled) pixels, calculate physical resolution
            var scaleFactor = Data.Dpi > 0 ? Data.Dpi / 96.0 : 1.0;
            var physicalWidth = (int)Math.Round(Data.MonitorWidth * scaleFactor);
            var physicalHeight = (int)Math.Round(Data.MonitorHeight * scaleFactor);
            var size = $"{physicalWidth}×{physicalHeight}";
            var scaling = Data.Dpi > 0 ? string.Format(CultureInfo.InvariantCulture, "{0}%", (int)Math.Round(scaleFactor * 100)) : "n/a";
            return $"{size} \u2022 {scaling}";
        }
    }
}
