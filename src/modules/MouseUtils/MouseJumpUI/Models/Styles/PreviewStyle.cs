// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using MouseJumpUI.Models.Drawing;

namespace MouseJumpUI.Models.Styles;

public class PreviewStyle
{
    public PreviewStyle(
        SizeInfo canvasSize)
    {
        this.CanvasSize = canvasSize ?? throw new ArgumentNullException(nameof(canvasSize));
    }

    public SizeInfo CanvasSize
    {
        get;
    }
}
