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

        System.Windows.Point CurrentPosition { get; }
    }
}
