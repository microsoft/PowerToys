using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Helper
{
    public class WoxException : Exception
    {
        public WoxException(string msg)
            : base(msg)
        {

        }
    }
}
