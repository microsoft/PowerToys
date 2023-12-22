// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Drawing;

namespace Microsoft.PowerToys.Settings.UI.Library.Modules.MouseJump;

public partial class MouseJumpSettings
{
    public static readonly MouseJumpSettings DefaultSettings = new(
        version: "2.0",
        properties: new(
            activationShortcut: MouseJumpProperties.DefaultActivationShortcut,
            thumbnailSize: null,
            previewStyle: new(
                canvasSize: new(
                    width: 1600,
                    height: 1200
                ),
                canvasStyle: new(
                    borderStyle: new(
                        color: SystemColors.Highlight,
                        width: 6,
                        depth: 0
                    ),
                    paddingStyle: new(
                        width: 4
                    ),
                    backgroundStyle: new(
                        color1: Color.FromArgb(0xFF, 0x0D, 0x57, 0xD2),
                        color2: Color.FromArgb(0xFF, 0x03, 0x44, 0xC0)
                    )
                ),
                screenStyle: new(
                    marginStyle: new(
                        width: 4
                    ),
                    borderStyle: new(
                        color: Color.FromArgb(0xFF, 0x22, 0x22, 0x22),
                        width: 12,
                        depth: 4
                    ),
                    backgroundStyle: new(
                        color1: Color.MidnightBlue,
                        color2: Color.MidnightBlue
                    )
                )
            )
        )
    );
}
