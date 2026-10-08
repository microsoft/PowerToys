// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Windows.Graphics;

namespace ShortcutGuide.Helpers
{
    internal readonly record struct ScreenLayout(
        IntPtr MonitorHandle,
        RectInt32 MonitorArea,
        RectInt32 UsableArea,
        float DpiScale,
        TaskbarEdge TaskbarEdge,
        bool IsTaskbarAutoHide,
        bool IsTaskbarVisible);
}
