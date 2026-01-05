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
            var size = $"{Data.MonitorWidth}×{Data.MonitorHeight}";
            var scaling = Data.Dpi > 0 ? string.Format(CultureInfo.InvariantCulture, "{0}%", (int)Math.Round(Data.Dpi * 100 / 96.0)) : "n/a";
            return $"{size} \u2022 {scaling}";
        }
    }
}
