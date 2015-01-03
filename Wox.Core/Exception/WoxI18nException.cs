using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Core.Exception
{
    public class WoxI18nException:WoxException
    {
        public WoxI18nException(string msg) : base(msg)
        {
        }
    }
}
