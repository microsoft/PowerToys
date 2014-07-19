using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows.Input;

namespace Wox.Plugin
{
    public delegate void WoxKeyDownEventHandler(object sender, WoxKeyDownEventArgs e);

    public class WoxKeyDownEventArgs
    {
        public string Query { get; set; }
        public KeyEventArgs keyEventArgs { get; set; }
    }
}
