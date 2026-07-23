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

        event EventHandler<Windows.Foundation.Point> MousePositionChanged;

        // position and bool indicating zoom in or zoom out
        event EventHandler<Tuple<Windows.Foundation.Point, bool>> OnMouseWheel;

        event PrimaryMouseDownEventHandler OnPrimaryMouseDown;

        event SecondaryMouseUpEventHandler OnSecondaryMouseUp;

        event MiddleMouseDownEventHandler OnMiddleMouseDown;

        Windows.Foundation.Point CurrentPosition { get; }

        Color CurrentColor { get; }
    }
}
