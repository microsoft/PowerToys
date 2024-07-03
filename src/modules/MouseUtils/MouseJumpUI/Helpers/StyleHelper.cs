// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;
using MouseJumpUI.Common.Models.Drawing;
using MouseJumpUI.Common.Models.Styles;

namespace MouseJumpUI.Helpers;

internal static class StyleHelper
{
    /// <summary>
    /// Default v2 preview style
    /// </summary>
    public static readonly PreviewStyle DefaultPreviewStyle = new(
        canvasSize: new(
            width: 1600,
            height: 1200
        ),
        canvasStyle: new(
            marginStyle: MarginStyle.Empty,
            borderStyle: new(
                color: SystemColors.Highlight,
                all: 6,
                depth: 0
            ),
            paddingStyle: new(
                all: 4
            ),
            backgroundStyle: new(
                color1: Color.FromArgb(0xFF, 0x0D, 0x57, 0xD2),
                color2: Color.FromArgb(0xFF, 0x03, 0x44, 0xC0)
            )
        ),
        screenStyle: new(
            marginStyle: new(
                all: 4
            ),
            borderStyle: new(
                color: Color.FromArgb(0xFF, 0x22, 0x22, 0x22),
                all: 12,
                depth: 4
            ),
            paddingStyle: PaddingStyle.Empty,
            backgroundStyle: new(
                color1: Color.MidnightBlue,
                color2: Color.MidnightBlue
            )
        )
    );

    /// <summary>
    /// Legacy preview style
    /// </summary>
    public static readonly PreviewStyle LegacyPreviewStyle = new(
        canvasSize: new(
            width: 1600,
            height: 1200
        ),
        canvasStyle: new(
            marginStyle: MarginStyle.Empty,
            borderStyle: new(
                color: SystemColors.Highlight,
                all: 6,
                depth: 0
            ),
            paddingStyle: new(
                all: 0
            ),
            backgroundStyle: new(
                color1: Color.FromArgb(0xFF, 0x0D, 0x57, 0xD2),
                color2: Color.FromArgb(0xFF, 0x03, 0x44, 0xC0)
            )
        ),
        screenStyle: new(
            marginStyle: new(
                all: 0
            ),
            borderStyle: new(
                color: Color.FromArgb(0xFF, 0x22, 0x22, 0x22),
                all: 0,
                depth: 0
            ),
            paddingStyle: PaddingStyle.Empty,
            backgroundStyle: new(
                color1: Color.MidnightBlue,
                color2: Color.MidnightBlue
            )
        )
    );

    public static PreviewStyle WithCanvasSize(this PreviewStyle previewStyle, SizeInfo canvasSize)
    {
        ArgumentNullException.ThrowIfNull(previewStyle);
        ArgumentNullException.ThrowIfNull(canvasSize);
        return new PreviewStyle(
            canvasSize: canvasSize,
            canvasStyle: previewStyle.CanvasStyle,
            screenStyle: previewStyle.ScreenStyle);
    }
}
