// Copyright (c) Microsoft Corporation
// The Microsoft Corporation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Drawing;

namespace ColorPicker.Mouse
{
    public interface IMouseInfoProvider
    {
        event EventHandler<Color> MouseColorChanged;

        event EventHandler<System.Windows.Point> MousePositionChanged;

        // position and bool indicating zoom in or zoom out
        event EventHandler<Tuple<System.Windows.Point, bool>> OnMouseWheel;

        event MouseUpEventHandler OnMouseDown;

        event SecondaryMouseUpEventHandler OnSecondaryMouseUp;

        System.Windows.Point CurrentPosition { get; }

        Color CurrentColor { get; }
    }
}
