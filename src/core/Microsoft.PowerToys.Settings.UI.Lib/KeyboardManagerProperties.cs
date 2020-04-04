using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class KeyboardManagerProperties
    {
        // Bool property to notify Keyboard Manager module if the Edit ShortCut botton is pressed.
        public BoolProperty EditShortCut { get; set; }

        // Bool property to notify Keyboard Manager module if the Remap Keyboard botton is pressed.
        public BoolProperty RemapKeyboard { get; set; }

        public KeyboardManagerProperties()
        {
            this.EditShortCut = new BoolProperty();
            this.RemapKeyboard = new BoolProperty();
        }

        public string ToJsonString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
