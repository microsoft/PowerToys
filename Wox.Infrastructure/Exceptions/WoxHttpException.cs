using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wox.Infrastructure.Exceptions
{
    public class WoxHttpException :WoxException
    {
        public WoxHttpException(string msg) : base(msg)
        {
        }
    }
}
