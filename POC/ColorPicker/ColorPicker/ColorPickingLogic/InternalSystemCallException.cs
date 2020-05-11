using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorPicker.ColorPickingLogic
{
    class InternalSystemCallException : Exception
    {
        public InternalSystemCallException(String message) : base(message) { }
    }
}
