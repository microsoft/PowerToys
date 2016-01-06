using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;

namespace Wox.Infrastructure.Hotkey
{
    public class HotkeyModel
    {
        public bool Alt { get; set; }
        public bool Shift { get; set; }
        public bool Win { get; set; }
        public bool Ctrl { get; set; }
        public Key CharKey { get; set; }


        Dictionary<Key, string> specialSymbolDictionary = new Dictionary<Key, string>
        {
            {Key.Space, "Space"},
            {Key.Oem3, "~"}
        };

        public ModifierKeys ModifierKeys
        {
            get
            {
                ModifierKeys modifierKeys = ModifierKeys.None;
                if (Alt)
                {
                    modifierKeys = ModifierKeys.Alt;
                }
                if (Shift)
                {
                    modifierKeys = modifierKeys | ModifierKeys.Shift;
                }
                if (Win)
                {
                    modifierKeys = modifierKeys | ModifierKeys.Windows;
                }
                if (Ctrl)
                {
                    modifierKeys = modifierKeys | ModifierKeys.Control;
                }
                return modifierKeys;
            }
        }

        public HotkeyModel(string hotkeyString)
        {
            Parse(hotkeyString);
        }

        public HotkeyModel(bool alt, bool shift, bool win, bool ctrl, Key key)
        {
            Alt = alt;
            Shift = shift;
            Win = win;
            Ctrl = ctrl;
            CharKey = key;
        }

        private void Parse(string hotkeyString)
        {
            if (string.IsNullOrEmpty(hotkeyString))
            {
                return;
            }
            List<string> keys = hotkeyString.Replace(" ", "").Split('+').ToList();
            if (keys.Contains("Alt"))
            {
                Alt = true;
                keys.Remove("Alt");
            }
            if (keys.Contains("Shift"))
            {
                Shift = true;
                keys.Remove("Shift");
            }
            if (keys.Contains("Win"))
            {
                Win = true;
                keys.Remove("Win");
            }
            if (keys.Contains("Ctrl"))
            {
                Ctrl = true;
                keys.Remove("Ctrl");
            }
            if (keys.Count > 0)
            {
                string charKey = keys[0];
                KeyValuePair<Key, string>? specialSymbolPair = specialSymbolDictionary.FirstOrDefault(pair => pair.Value == charKey);
                if (specialSymbolPair.Value.Value != null)
                {
                    CharKey = specialSymbolPair.Value.Key;
                }
                else
                {
                    try
                    {
                        CharKey = (Key) Enum.Parse(typeof (Key), charKey);
                    }
                    catch (ArgumentException)
                    {

                    }
                }
            }
        }

        public override string ToString()
        {
            string text = string.Empty;
            if (Ctrl)
            {
                text += "Ctrl + ";
            }
            if (Alt)
            {
                text += "Alt + ";
            }
            if (Shift)
            {
                text += "Shift + ";
            }
            if (Win)
            {
                text += "Win + ";
            }

            if (CharKey != Key.None)
            {
                text += specialSymbolDictionary.ContainsKey(CharKey)
                    ? specialSymbolDictionary[CharKey]
                    : CharKey.ToString();
            }
            else if (!string.IsNullOrEmpty(text))
            {
                text = text.Remove(text.Length - 3);
            }

            return text;
        }
    }
}
