using AutoHotkey.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Infrastructure.Hotkey
{
    public class AHKHotkey : IHotkey
    {
        public bool RegisterHotkey(string hotkey, Action action)
        {
            AutoHotkeyEngine ahk = AHKHotkeyEngineFactory.CreateOrGet("default");
            ahk.ExecRaw(string.Format("{0}::MsgBox, ssss!",hotkey));
            return true;
        }
    }
}
