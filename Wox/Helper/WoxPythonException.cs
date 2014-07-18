using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Helper
{
    public class WoxJsonPRCException : WoxException
    {
        public WoxJsonPRCException(string msg)
            : base(msg)
        {
        }
    }
}
