using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Infrastructure.Hotkey
{
    interface IHotkey
    {
        bool RegisterHotkey(string hotkey, Action action);
    }
}
