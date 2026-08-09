// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

namespace Settings.UI.Library.Enumerations
{
    /// <summary>
    /// The anchor used to place the Screen Ruler toolbar on the monitor containing the mouse
    /// cursor whenever the toolbar is summoned (or transitions from hidden to visible). The
    /// numeric values are persisted in settings.json and must stay stable. Values 3-5 belonged to
    /// retired middle-row anchors and intentionally remain unused so existing bottom anchors keep
    /// resolving to the same positions.
    /// </summary>
    public enum MeasureToolToolbarPosition
    {
        TopLeft = 0,
        TopCenter = 1,
        TopRight = 2,
        BottomLeft = 6,
        BottomCenter = 7,
        BottomRight = 8,
    }
}
