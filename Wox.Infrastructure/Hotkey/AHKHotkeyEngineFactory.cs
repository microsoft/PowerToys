using AutoHotkey.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Infrastructure.Hotkey
{
    internal class AHKHotkeyEngineFactory
    {
        private static List<KeyValuePair<string, AutoHotkeyEngine>> engines = new List<KeyValuePair<string, AutoHotkeyEngine>>();

        public static AutoHotkeyEngine CreateOrGet(string name)
        {
            AutoHotkeyEngine engine = Get(name);
            if (engine == null)
            {
                engine = Create(name);
            }
            return engine;
        }

        public static AutoHotkeyEngine Create(string name)
        {
            var ahk = new AutoHotkey.Interop.AutoHotkeyEngine();
            engines.Add(new KeyValuePair<string, AutoHotkeyEngine>(name, ahk));
            return ahk;
        }

        public static AutoHotkeyEngine Get(string name)
        {
            var engine = engines.FirstOrDefault(o => o.Key == name);
            if (engine.Key != null)
            {
                return engine.Value;
            }

            return null;
        }

        public static void Destroy(string name)
        {
            var engine = engines.FirstOrDefault(o => o.Key == name);
            if (engine.Key != null)
            {
                engine.Value.Terminate();
                engines.Remove(engine);
            }
        }
    }
}
