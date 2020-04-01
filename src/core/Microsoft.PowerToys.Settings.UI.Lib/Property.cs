using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace Microsoft.PowerToys.Settings.UI.Lib
{
    public class BoolProperty
    {
        public bool value { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }

    public class IntProperty
    {
        public int value { get; set; }

        public override string ToString()
        {
            return JsonSerializer.Serialize(this);
        }
    }
}
