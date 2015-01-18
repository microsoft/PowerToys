using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace Wox.Plugin
{
    public delegate void WoxKeyDownEventHandler(WoxKeyDownEventArgs e);
    public delegate void AfterWoxQueryEventHandler(WoxQueryEventArgs e);

    /// <summary>
    /// Global keyboard events
    /// </summary>
    /// <param name="keyevent">WM_KEYDOWN = 256,WM_KEYUP = 257,WM_SYSKEYUP = 261,WM_SYSKEYDOWN = 260</param>
    /// <param name="vkcode"></param>
    /// <param name="state"></param>
    /// <returns>return true to continue handling, return false to intercept system handling</returns>
    public delegate bool WoxGlobalKeyboardEventHandler(int keyevent, int vkcode, SpecialKeyState state);

    public class WoxKeyDownEventArgs
    {
        public string Query { get; set; }
        public KeyEventArgs keyEventArgs { get; set; }
    }

    public class WoxQueryEventArgs
    {
        public Query Query { get; set; }
    }
}
