// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using PowerOCR.NativeMethods;

namespace PowerOCR.Models.Drawing;

/// <summary>
/// Immutable version of a System.Windows.Forms.Screen object so we don't need to
/// take a dependency on WinForms just for screen info.
/// </summary>
public sealed class ScreenInfo
{
    internal ScreenInfo(Core.HMONITOR handle, bool primary, Rectangle displayArea, Rectangle workingArea)
    {
        this.Handle = handle;
        this.Primary = primary;
        this.DisplayArea = displayArea;
        this.WorkingArea = workingArea;
    }

    public int Handle
    {
        get;
    }

    public bool Primary
    {
        get;
    }

    public Rectangle DisplayArea
    {
        get;
    }

    public Rectangle WorkingArea
    {
        get;
    }
}
