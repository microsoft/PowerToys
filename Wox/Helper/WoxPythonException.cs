using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Helper
{
    public class WoxPythonException : WoxException
    {
        public WoxPythonException(string msg) : base(msg)
        {
        }
    }
}
